using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;
using MsgPack;
using MsgPack.Serialization;

namespace CoolRanch
{
    public class InfoResponseEventArgs : EventArgs
    {
        public IPEndPoint EndPoint;
        public byte[] Challenge;
        public Dictionary<string, object> Info;

        public InfoResponseEventArgs(IPEndPoint endpoint, byte[] challenge, Dictionary<string, object> info)
        {
            EndPoint = endpoint;
            Challenge = challenge;
            Info = info;
        }
    }

    public class SessionInfoExchanger
    {
        UdpClient _udp;
        ElDorado _game;
        HMAC _challengeHmac;

        private IPEndPoint _masterServer;
        private IPEndPoint _joinTarget;
        private Dictionary<IPEndPoint, byte[]> _challengeCache; // oh god what 

        public bool AllowJoins, Announcing;

        public event EventHandler<InfoResponseEventArgs> InfoResponseReceived;

        public SessionInfoExchanger(ElDorado game)
        {
            _game = game;
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, 11770));
            _udp.Client.IOControl(-1744830452, new byte[] { 0, 0, 0, 0 }, null);
            _challengeHmac = new HMACMD5();

            _masterServer =
                new IPEndPoint(Array.Find(Dns.GetHostAddresses("coolranch.ax.lt"),
                    a => a.AddressFamily == AddressFamily.InterNetwork), 8080);
            _challengeCache = new Dictionary<IPEndPoint, byte[]>();
        }

        public void ReceiveLoop()
        {
            var peer = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                var data = _udp.Receive(ref peer);
                Console.WriteLine("{0} -> {1}", peer, BitConverter.ToString(data));

                var reader = new BinaryReader(new MemoryStream(data));
                var magic = new string(reader.ReadChars(4));

                if (magic != "SIXP")
                    continue;

                var version = reader.ReadByte();
                var flags = reader.ReadByte();

                byte[] msg;

                if ((flags & 0x1) != 0)
                    throw new NotImplementedException();
                else
                    msg = reader.ReadBytes(1024);

                ProcessMessage(msg, peer);
            }
        }

        public void AnnounceLoop()
        {
            Console.WriteLine("Announcement loop started.");

            while (Announcing)
            {
                SendAssociationRequest(_masterServer);
                Thread.Sleep(120000);
            }

            Console.WriteLine("Announcement loop stopped.");
        }

        enum SixpMessageType : byte
        {
            ChallengeRequest,
            ChallengeResponse,
            AssociationRequest,
            InfoRequest,
            InfoResponse,
            JoinRequest,
            JoinResponse
        };

        void ProcessMessage(byte[] msg, IPEndPoint peer)
        {
            var ms = new MemoryStream(msg);
            var reader = new BinaryReader(ms);

            var type = (SixpMessageType)reader.ReadByte();
            switch (type)
            {
                case SixpMessageType.ChallengeRequest:
                {
                    var clientChallenge = reader.ReadBytes(4);
                    SendChallengeResponse(peer, clientChallenge);
                }
                    break;
                case SixpMessageType.ChallengeResponse:
                {
                    var clientChallenge = reader.ReadBytes(4);
                    var hostChallenge = reader.ReadBytes(4);

                    var verify = _challengeHmac.ComputeHash(peer.Address.GetAddressBytes()).Take(4);
                    if (!clientChallenge.SequenceEqual(verify))
                        return;

                    _challengeCache[peer] = hostChallenge;

                    if (Equals(peer, _joinTarget))
                        SendJoinRequest(peer, hostChallenge);
                    else
                        SendInfoRequest(peer, hostChallenge);
                }
                    break;
                case SixpMessageType.InfoRequest:
                {
                    var hostChallenge = reader.ReadBytes(4);
                    var clientChallenge = reader.ReadBytes(4);

                    var verify = _challengeHmac.ComputeHash(peer.Address.GetAddressBytes()).Take(4);
                    if (!hostChallenge.SequenceEqual(verify))
                        return;

                    if (Announcing)
                        SendInfoReponse(peer, clientChallenge);
                }
                    break;
                case SixpMessageType.InfoResponse:
                {
                    var clientChallenge = reader.ReadBytes(4);
                    var verify = _challengeHmac.ComputeHash(peer.Address.GetAddressBytes()).Take(4);
                    if (!clientChallenge.SequenceEqual(verify))
                        return;

                    var serializer = MessagePackSerializer.Get<Dictionary<string, object>>();
                    var info = serializer.Unpack(ms);
                    if (InfoResponseReceived != null)
                        InfoResponseReceived(this, new InfoResponseEventArgs(peer, _challengeCache[peer], info));
                    _challengeCache.Remove(peer);
                }
                    break;
                case SixpMessageType.JoinRequest:
                {
                    var hostChallenge = reader.ReadBytes(4);
                    var clientChallenge = reader.ReadBytes(4);

                    var verify = _challengeHmac.ComputeHash(peer.Address.GetAddressBytes()).Take(4);
                    if (!hostChallenge.SequenceEqual(verify))
                        return;

                    SendJoinResponse(peer, clientChallenge);
                }
                    break;
                case SixpMessageType.JoinResponse:
                {
                    var clientChallenge = reader.ReadBytes(4);
                    var verify = _challengeHmac.ComputeHash(peer.Address.GetAddressBytes()).Take(4);
                    if (!clientChallenge.SequenceEqual(verify))
                        return;

                    var port = reader.ReadUInt16();
                    var xnKid = new Guid(reader.ReadBytes(16));
                    var xnAddr = new Guid(reader.ReadBytes(16));

                    if (_game.IsRunning && peer.Equals(_joinTarget))
                        _game.InjectJoin(new IPEndPoint(peer.Address, port), xnKid, xnAddr);
                    _joinTarget = null;
                }
                    break;
            }
        }

        public void ConnectFromScratch(string hostname, int port)
        {
            IPAddress[] addresses;

            try
            {
                addresses = Dns.GetHostAddresses(hostname);
            }
            catch (Exception)
            {
                addresses = new IPAddress[]{};
            }

            if (addresses.Length == 0)
            {
                MessageBox.Show("Hostname not found, or IP address is invalid.", null, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _joinTarget = new IPEndPoint(Array.Find(addresses, a => a.AddressFamily == AddressFamily.InterNetwork), port);
            SendChallengeRequest(_joinTarget);
        }

        public void ConnectWithChallenge(IPEndPoint peer, byte[] hostChallenge)
        {
            _joinTarget = peer;
            SendJoinRequest(_joinTarget, hostChallenge);
        }

        public void ChallengeForInfo(IPEndPoint peer)
        {
            SendChallengeRequest(peer);
        }

        void SendMessage(IPEndPoint peer, SixpMessageType type, byte[] msg)
        {
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            writer.Write((byte)type);
            writer.Write(msg);
            var data = ms.ToArray();

            if (data.Length > 1024)
            {
                throw new NotImplementedException();
            }
            else
            {
                var header = Encoding.ASCII.GetBytes("SIXP\0\0");
                var packet = new byte[data.Length + header.Length];
                Array.Copy(header, packet, header.Length);
                Array.Copy(data, 0, packet, header.Length, data.Length);
                Console.WriteLine("{0} <- {1}", peer, BitConverter.ToString(packet));
                _udp.Send(packet, packet.Length, peer);
            }
        }

        void SendChallengeRequest(IPEndPoint peer)
        {
            var clientChallenge = _challengeHmac.ComputeHash(peer.Address.GetAddressBytes());

            var request = new byte[4];
            Array.Copy(clientChallenge, request, 4);
            SendMessage(peer, SixpMessageType.ChallengeRequest, request);
        }

        void SendChallengeResponse(IPEndPoint peer, byte[] clientChallenge)
        {
            var hostChallenge = _challengeHmac.ComputeHash(peer.Address.GetAddressBytes());

            var response = new byte[8];
            Array.Copy(clientChallenge, response, 4);
            Array.Copy(hostChallenge, 0, response, 4, 4);
            SendMessage(peer, SixpMessageType.ChallengeResponse, response);
        }

        void SendAssociationRequest(IPEndPoint peer)
        {
            var hostChallenge = _challengeHmac.ComputeHash(peer.Address.GetAddressBytes());
            var response = hostChallenge.Take(4).ToArray();
            SendMessage(peer, SixpMessageType.AssociationRequest, response);
        }

        void SendInfoRequest(IPEndPoint peer, byte[] hostChallenge)
        {
            var clientChallenge = _challengeHmac.ComputeHash(peer.Address.GetAddressBytes());

            var request = new byte[8];
            Array.Copy(hostChallenge, request, 4);
            Array.Copy(clientChallenge, 0, request, 4, 4);

            SendMessage(peer, SixpMessageType.InfoRequest, request);
        }

        void SendInfoReponse(IPEndPoint peer, byte[] clientChallenge)
        {
            if (!_game.IsRunning || !AllowJoins || !_game.IsHostingOnlineSession()) return;

            var serializer = MessagePackSerializer.Get<Dictionary<string, object>>();
            var info = _game.GetGameInfo();

            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            writer.Write(clientChallenge);
            serializer.Pack(ms, info);

            SendMessage(peer, SixpMessageType.InfoResponse, ms.ToArray());
        }

        void SendJoinRequest(IPEndPoint peer, byte[] hostChallenge)
        {
            var clientChallenge = _challengeHmac.ComputeHash(peer.Address.GetAddressBytes());

            var request = new byte[8];
            Array.Copy(hostChallenge, request, 4);
            Array.Copy(clientChallenge, 0, request, 4, 4);

            SendMessage(peer, SixpMessageType.JoinRequest, request);
        }

        void SendJoinResponse(IPEndPoint peer, byte[] clientChallenge)
        {
            if (!_game.IsRunning || !AllowJoins) return;

            var guids = _game.GetXnetParams();

            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            writer.Write(clientChallenge);
            writer.Write((ushort)11774);
            writer.Write(guids[0].ToByteArray());
            writer.Write(guids[1].ToByteArray());

            SendMessage(peer, SixpMessageType.JoinResponse, ms.ToArray());
        }
    }
}

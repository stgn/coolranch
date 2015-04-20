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
using MsgPack;

namespace CoolRanch
{
    public class SessionInfoExchanger
    {
        UdpClient Udp;
        ElDorado Game;
        HMAC ChallengeHMAC;
        bool Running;

        public SessionInfoExchanger(ElDorado Game)
        {
            this.Game = Game;
            Udp = new UdpClient(new IPEndPoint(IPAddress.Any, 11770));
            ChallengeHMAC = new HMACMD5();
            Running = true;
        }

        public void ReceiveLoop()
        {
            var peer = new IPEndPoint(IPAddress.Any, 0);

            while (Running)
            {
                var data = Udp.Receive(ref peer);
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
            while (true)
            {
                Thread.Sleep(120000);
            }
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
                        // TODO: not do this
                        var clientChallenge = reader.ReadBytes(4);
                        var hostChallenge = reader.ReadBytes(4);

                        var verify = ChallengeHMAC.ComputeHash(peer.Address.GetAddressBytes()).Take(4);
                        if (!clientChallenge.SequenceEqual(verify))
                            return;

                        SendJoinRequest(peer, hostChallenge);
                    }
                    break;
                case SixpMessageType.JoinRequest:
                    {
                        var hostChallenge = reader.ReadBytes(4);
                        var clientChallenge = reader.ReadBytes(4);

                        var verify = ChallengeHMAC.ComputeHash(peer.Address.GetAddressBytes()).Take(4);
                        if (!hostChallenge.SequenceEqual(verify))
                            return;

                        SendJoinResponse(peer, clientChallenge);
                    }
                    break;
                case SixpMessageType.JoinResponse:
                    {
                        var clientChallenge = reader.ReadBytes(4);
                        var verify = ChallengeHMAC.ComputeHash(peer.Address.GetAddressBytes()).Take(4);
                        if (!clientChallenge.SequenceEqual(verify))
                            return;

                        var port = reader.ReadUInt16();
                        var xnKid = new Guid(reader.ReadBytes(16));
                        var xnAddr = new Guid(reader.ReadBytes(16));

                        Game.InjectJoin(new IPEndPoint(peer.Address, port), xnKid, xnAddr);
                    }
                    break;
            }
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
                Udp.Send(packet, packet.Length, peer);
            }
        }

        public void SendChallengeRequest(IPEndPoint peer)
        {
            var clientChallenge = ChallengeHMAC.ComputeHash(peer.Address.GetAddressBytes());

            var request = new byte[4];
            Array.Copy(clientChallenge, request, 4);
            SendMessage(peer, SixpMessageType.ChallengeRequest, request);
        }

        void SendChallengeResponse(IPEndPoint peer, byte[] clientChallenge)
        {
            var hostChallenge = ChallengeHMAC.ComputeHash(peer.Address.GetAddressBytes());

            var response = new byte[8];
            Array.Copy(clientChallenge, response, 4);
            Array.Copy(hostChallenge, 0, response, 4, 4);
            SendMessage(peer, SixpMessageType.ChallengeResponse, response);
        }

        void SendJoinRequest(IPEndPoint peer, byte[] hostChallenge)
        {
            var clientChallenge = ChallengeHMAC.ComputeHash(peer.Address.GetAddressBytes());

            var request = new byte[8];
            Array.Copy(hostChallenge, request, 4);
            Array.Copy(clientChallenge, 0, request, 4, 4);

            SendMessage(peer, SixpMessageType.JoinRequest, request);
        }

        void SendJoinResponse(IPEndPoint peer, byte[] clientChallenge)
        {
            if (Game.IsRunning)
            {
                var guids = Game.GetXnetParams();

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
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Text;

namespace CoolRanch
{
    public class ElDorado
    {
        public bool IsRunning;
        ProcessSpy _gameProcess;

        public event EventHandler ProcessLaunched;
        public event EventHandler ProcessClosed;

        private int _syslinkDataBuffer;

        void SpyOnGameProcess(uint pid)
        {
            if (!IsRunning)
            {
                Console.WriteLine("Detected game process.");
                _gameProcess = new ProcessSpy(pid);
                IsRunning = true;

                if (ProcessLaunched != null)
                    ProcessLaunched(this, EventArgs.Empty);
            }
        }

        public void MonitorProcesses()
        {
            var processes = Process.GetProcessesByName("eldorado");
            if (processes.Length > 0)
                SpyOnGameProcess((uint)processes.OrderBy(p => p.StartTime).First().Id);

            var openQuery = new WqlEventQuery("__InstanceCreationEvent", new TimeSpan(0, 0, 1),
                "TargetInstance isa \"Win32_Process\"");
            var closeQuery = new WqlEventQuery("__InstanceDeletionEvent", new TimeSpan(0, 0, 1),
                "TargetInstance isa \"Win32_Process\"");

            var openWatcher = new ManagementEventWatcher(openQuery);
            openWatcher.EventArrived += openWatcher_EventArrived;
            openWatcher.Start();

            var closeWatcher = new ManagementEventWatcher(closeQuery);
            closeWatcher.EventArrived += closeWatcher_EventArrived;
            closeWatcher.Start();
        }

        void openWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            var instance = (ManagementBaseObject)e.NewEvent.Properties["TargetInstance"].Value;
            var name = (string)instance["Name"];
            var pid = (uint)instance["ProcessId"];

            if (name == "eldorado.exe" && !IsRunning)
                SpyOnGameProcess(pid);
        }

        void closeWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            var instance = (ManagementBaseObject)e.NewEvent.Properties["TargetInstance"].Value;
            var name = (string)instance["Name"];
            var pid = (uint)instance["ProcessId"];

            if (name == "eldorado.exe" && _gameProcess.ProcessId == pid)
            {
                Console.WriteLine("Game process closed.");
                IsRunning = false;
                if (ProcessClosed != null)
                    ProcessClosed(this, EventArgs.Empty);
            }
        }

        public bool IsHostingOnlineSession()
        {
            for(var i = 0; i < 4; i++)
                if (BitConverter.ToInt32(_gameProcess.Read(0x1a29d38 + i*8, 4), 0) == 1)
                    return true;
            return false;
        }

        public Guid[] GetXnetParams()
        {
            return new Guid[] {
                new Guid(_gameProcess.Read(0x2247b80, 16)),
                new Guid(_gameProcess.Read(0x2247b90, 16))
            };
        }

        public Dictionary<string, object> GetGameInfo()
        {
            return new Dictionary<string, object>()
            {
                {"name", Encoding.Unicode.GetString(_gameProcess.Read(0x01863B20, 32)).Replace("\0","")},
                {"map", Encoding.Unicode.GetString(_gameProcess.Read(0x01863ACA, 32)).Replace("\0","")},
                {"gametype", Encoding.Unicode.GetString(_gameProcess.Read(0x01863A9C, 32)).Replace("\0","")}
            };
        } 

        public void InjectJoin(IPEndPoint peer, Guid xnKid, Guid xnAddr)
        {
            Console.WriteLine("Joining {0} {1} at {2}", xnKid, xnAddr, peer);
            var rawXnKid = xnKid.ToByteArray();
            var rawXnAddr = xnAddr.ToByteArray();

            var syslinkData = new byte[374];
            var writer = new BinaryWriter(new MemoryStream(syslinkData));
            writer.Write(1);
            writer.Seek(154, SeekOrigin.Current);
            writer.Write(rawXnKid);
            writer.Write(rawXnAddr);
            writer.Seek(178, SeekOrigin.Current);
            writer.Write(peer.Address.GetAddressBytes().Reverse().ToArray());
            writer.Write((short)peer.Port);
            writer.Flush();

            if (_syslinkDataBuffer == 0)
                _syslinkDataBuffer = _gameProcess.Alloc(374);
            _gameProcess.Write(_syslinkDataBuffer, syslinkData);

            var syslinkLenPtr = new byte[8];
            writer = new BinaryWriter(new MemoryStream(syslinkLenPtr));
            writer.Write(1);
            writer.Write(_syslinkDataBuffer);
            writer.Flush();

            _gameProcess.Write(0x228e6d8, syslinkLenPtr);

            var joinData = new byte[88];
            writer = new BinaryWriter(new MemoryStream(joinData));
            writer.Seek(16, SeekOrigin.Current);
            writer.Write(ulong.MaxValue);
            writer.Write(1);
            writer.Write(rawXnKid);
            writer.Seek(16, SeekOrigin.Current);
            writer.Write(rawXnAddr);
            writer.Write(1);
            writer.Flush();

            _gameProcess.Write(0x2240b98, joinData);
        }
    }
}

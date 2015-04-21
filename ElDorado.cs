using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace CoolRanch
{
    class Kernel32
    {
        [DllImport("kernel32.dll")]
        public static extern int OpenProcess(int dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(int hObject);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        public static extern bool WriteProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        public static extern int VirtualAllocEx(int hProcess, int lpAddress, int dwSize, int flAllocationType, int flProtect);
    }

    class ProcessSpy
    {
        public uint ProcessId;
        readonly int _handle;

        public ProcessSpy(uint processId)
        {
            this.ProcessId = processId;
            _handle = Kernel32.OpenProcess(0x38, false, processId);
        }

        ~ProcessSpy()
        {
            Kernel32.CloseHandle(_handle);
        }

        public byte[] Read(int address, int size)
        {
            var buf = new byte[size];
            int bytesread = 0;
            Kernel32.ReadProcessMemory(_handle, address, buf, size, ref bytesread);
            return buf.Take(bytesread).ToArray();
        }

        public int Write(int address, byte[] data)
        {
            int byteswritten = 0;
            Kernel32.WriteProcessMemory(_handle, address, data, data.Length, ref byteswritten);
            return byteswritten;
        }

        public int Alloc(int size)
        {
            return Kernel32.VirtualAllocEx(_handle, 0, size, 0x3000, 0x40);
        }
    }

    public class ElDorado
    {
        public bool IsRunning = false;
        ProcessSpy _gameProcess;

        public event EventHandler ProcessLaunched;
        public event EventHandler ProcessClosed;

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
            Console.WriteLine("Joining {0} {1} at {2}", peer, xnKid, xnAddr);
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
            writer.BaseStream.Flush();

            var address = _gameProcess.Alloc(374);
            _gameProcess.Write(address, syslinkData);

            var syslinkLenPtr = new byte[8];
            writer = new BinaryWriter(new MemoryStream(syslinkLenPtr));
            writer.Write(1);
            writer.Write(address);
            writer.BaseStream.Flush();

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
            writer.BaseStream.Flush();

            _gameProcess.Write(0x2240b98, joinData);
        }
    }
}

using System.Linq;
using System.Runtime.InteropServices;

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
            var bytesRead = 0;
            Kernel32.ReadProcessMemory(_handle, address, buf, size, ref bytesRead);
            return buf.Take(bytesRead).ToArray();
        }

        public int Write(int address, byte[] data)
        {
            var bytesWritten = 0;
            Kernel32.WriteProcessMemory(_handle, address, data, data.Length, ref bytesWritten);
            return bytesWritten;
        }

        public int Alloc(int size)
        {
            return Kernel32.VirtualAllocEx(_handle, 0, size, 0x3000, 0x40);
        }
    }
}

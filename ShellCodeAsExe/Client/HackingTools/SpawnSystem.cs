using System;
using System.Runtime.InteropServices;

namespace ProcessSpawner
{
    public class ProcessSpawner
    {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CreateProcess(string appPath, string cmdLine, ref SecurityAttributes processAttr, ref SecurityAttributes threadAttr, bool inheritHandles, uint creationFlags, IntPtr environment, string currentDir, [In] ref StartupInfoEx startupInfo, out ProcessInfo processInfo);


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessAccess access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern UInt32 WaitForSingleObject(IntPtr handle, UInt32 milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateProcThreadAttribute(IntPtr attrList, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr prevValue, IntPtr returnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InitializeProcThreadAttributeList(IntPtr attrList, int attrCount, int flags, ref IntPtr size);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetHandleInformation(IntPtr handle, HandleFlags mask, HandleFlags flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool DuplicateHandle(IntPtr srcProcessHandle, IntPtr srcHandle, IntPtr targetProcessHandle, ref IntPtr targetHandle, uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint options);

        public static void SpawnSystem(int parentId, string cmd)
        {
            const int ProcThreadAttrParentProcess = 0x00020000;
            const uint ExtendedStartupInfoPresent = 0x00080000;
            const uint CreateNewConsole = 0x00000010;
            
            var procInfo = new ProcessInfo();
            var startupInfoEx = new StartupInfoEx();
            IntPtr valueProc = IntPtr.Zero;
            IntPtr srcProcessHandle = IntPtr.Zero;
            var size = IntPtr.Zero;

            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            startupInfoEx.AttributeList = Marshal.AllocHGlobal(size);
            InitializeProcThreadAttributeList(startupInfoEx.AttributeList, 1, 0, ref size);

            IntPtr parentHandle = OpenProcess(ProcessAccess.CreateProcess | ProcessAccess.DuplicateHandle, false, parentId);
            valueProc = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(valueProc, parentHandle);

            UpdateProcThreadAttribute(startupInfoEx.AttributeList, 0, (IntPtr)ProcThreadAttrParentProcess, valueProc, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero);

            var processSec = new SecurityAttributes();
            var threadSec = new SecurityAttributes();
            processSec.Length = Marshal.SizeOf(processSec);
            threadSec.Length = Marshal.SizeOf(threadSec);

            bool success = CreateProcess("cmd.exe", "/c " + cmd, ref processSec, ref threadSec, true, ExtendedStartupInfoPresent | CreateNewConsole, IntPtr.Zero, null, ref startupInfoEx, out procInfo);

            if (success)
            {
                WaitForSingleObject(procInfo.ProcessHandle, 0xFFFFFFFF);
                CloseHandle(procInfo.ProcessHandle);
                CloseHandle(procInfo.ThreadHandle);
            }
            else
            {
                Console.WriteLine("Failed to spawn process.");
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct StartupInfoEx
    {
        public StartupInfo Startup;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct StartupInfo
    {
        public Int32 Size;
        public string Reserved;
        public string Desktop;
        public string Title;
        public Int32 X;
        public Int32 Y;
        public Int32 XSize;
        public Int32 YSize;
        public Int32 XChars;
        public Int32 YChars;
        public Int32 FillAttribute;
        public Int32 Flags;
        public Int16 ShowWindow;
        public Int16 Reserved2;
        public IntPtr ReservedPtr;
        public IntPtr StdInput;
        public IntPtr StdOutput;
        public IntPtr StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessInfo
    {
        public IntPtr ProcessHandle;
        public IntPtr ThreadHandle;
        public int ProcessId;
        public int ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool InheritHandle;
    }

    [Flags]
    public enum ProcessAccess : uint
    {
        All = 0x001F0FFF,
        Terminate = 0x00000001,
        CreateThread = 0x00000002,
        VirtualMemoryOperation = 0x00000008,
        VirtualMemoryRead = 0x00000010,
        VirtualMemoryWrite = 0x00000020,
        DuplicateHandle = 0x00000040,
        CreateProcess = 0x000000080,
        SetQuota = 0x00000100,
        SetInformation = 0x00000200,
        QueryInformation = 0x00000400,
        QueryLimitedInformation = 0x00001000,
        Synchronize = 0x00100000
    }

    [Flags]
    enum HandleFlags : uint
    {
        None = 0,
        Inherit = 1,
        ProtectFromClose = 2
    }
}
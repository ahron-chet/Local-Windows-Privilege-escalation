using System;
using System.Runtime.InteropServices;

namespace Client.TaskLauncher
{
    public static class ProcessExecutor
    {
        private const int UnicodeEnvironmentFlag = 0x00000400;
        private const int NoWindowFlag = 0x08000000;
        private const int NewConsoleFlag = 0x00000010;
        private const uint InvalidSessionId = 0xFFFFFFFF;
        private static readonly IntPtr CurrentServerHandle = IntPtr.Zero;

        [DllImport("advapi32.dll", EntryPoint = "CreateProcessAsUser", SetLastError = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool CreateProcessAsUser(
            IntPtr token,
            string appName,
            string commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            bool inheritHandle,
            uint creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref STARTUPINFO startupInfo,
            out PROCESS_INFORMATION processInformation);

        [DllImport("advapi32.dll", EntryPoint = "DuplicateTokenEx")]
        private static extern bool DuplicateTokenEx(
            IntPtr existingTokenHandle,
            uint desiredAccess,
            IntPtr threadAttributes,
            int tokenType,
            int impersonationLevel,
            ref IntPtr duplicateTokenHandle);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool CreateEnvironmentBlock(ref IntPtr environment, IntPtr token, bool inherit);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyEnvironmentBlock(IntPtr environment);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr snapshot);

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("Wtsapi32.dll")]
        private static extern uint WTSQueryUserToken(uint sessionId, ref IntPtr token);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern int WTSEnumerateSessions(
            IntPtr server,
            int reserved,
            int version,
            ref IntPtr sessionInfo,
            ref int count);

        private enum SW
        {
            Hide = 0,
            ShowNormal = 1,
            Normal = 1,
            ShowMinimized = 2,
            ShowMaximized = 3,
            Maximize = 3,
            ShowNoActivate = 4,
            Show = 5,
            Minimize = 6,
            ShowMinNoActive = 7,
            ShowNA = 8,
            Restore = 9,
            ShowDefault = 10,
            Max = 10
        }

        private enum WTS_CONNECTSTATE_CLASS
        {
            Active,
            Connected,
            ConnectQuery,
            Shadow,
            Disconnected,
            Idle,
            Listen,
            Reset,
            Down,
            Init
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        private enum SECURITY_IMPERSONATION_LEVEL
        {
            Anonymous = 0,
            Identification = 1,
            Impersonation = 2,
            Delegation = 3,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCount;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        private enum TOKEN_TYPE
        {
            Primary = 1,
            Impersonation = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            public readonly UInt32 SessionID;
            [MarshalAs(UnmanagedType.LPStr)]
            public readonly String pWinStationName;
            public readonly WTS_CONNECTSTATE_CLASS State;
        }

        private static bool GetUserToken(ref IntPtr userToken)
        {
            bool result = false;
            IntPtr impersonationToken = IntPtr.Zero;
            uint activeSessionId = InvalidSessionId;
            IntPtr sessionInfo = IntPtr.Zero;
            int sessionCount = 0;

            if (WTSEnumerateSessions(CurrentServerHandle, 0, 1, ref sessionInfo, ref sessionCount) != 0)
            {
                int arrayElementSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                IntPtr current = sessionInfo;

                for (int i = 0; i < sessionCount; i++)
                {
                    var session = (WTS_SESSION_INFO)Marshal.PtrToStructure((IntPtr)current, typeof(WTS_SESSION_INFO));
                    current += arrayElementSize;

                    if (session.State == WTS_CONNECTSTATE_CLASS.Active)
                    {
                        activeSessionId = session.SessionID;
                    }
                }
            }

            if (activeSessionId == InvalidSessionId)
            {
                activeSessionId = WTSGetActiveConsoleSessionId();
            }

            if (WTSQueryUserToken(activeSessionId, ref impersonationToken) != 0)
            {
                result = DuplicateTokenEx(impersonationToken, 0, IntPtr.Zero,
                    (int)SECURITY_IMPERSONATION_LEVEL.Impersonation, (int)TOKEN_TYPE.Primary,
                    ref userToken);
                CloseHandle(impersonationToken);
            }

            return result;
        }

        public static bool LaunchTask(string appPath, string cmdLine = null, string workDir = null, bool visible = true)
        {
            IntPtr userToken = IntPtr.Zero;
            var startupInfo = new STARTUPINFO();
            var processInfo = new PROCESS_INFORMATION();
            IntPtr environment = IntPtr.Zero;

            startupInfo.cb = Marshal.SizeOf(typeof(STARTUPINFO));

            try
            {
                if (!GetUserToken(ref userToken))
                {
                    throw new Exception("LaunchTask: GetUserToken failed.");
                }

                uint creationFlags = UnicodeEnvironmentFlag | (uint)(visible ? NewConsoleFlag : NoWindowFlag);
                startupInfo.wShowWindow = (short)(visible ? SW.Show : SW.Hide);
                startupInfo.lpDesktop = "winsta0\\default";

                if (!CreateEnvironmentBlock(ref environment, userToken, false))
                {
                    throw new Exception("LaunchTask: CreateEnvironmentBlock failed.");
                }

                if (!CreateProcessAsUser(userToken,
                    appPath,
                    cmdLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    creationFlags,
                    environment,
                    workDir,
                    ref startupInfo,
                    out processInfo))
                {
                    throw new Exception("LaunchTask: CreateProcessAsUser failed.");
                }
            }
            finally
            {
                CloseHandle(userToken);

                if (environment != IntPtr.Zero)
                {
                    DestroyEnvironmentBlock(environment);
                }

                CloseHandle(processInfo.hThread);
                CloseHandle(processInfo.hProcess);

            }

            return true;
        }
    }
}


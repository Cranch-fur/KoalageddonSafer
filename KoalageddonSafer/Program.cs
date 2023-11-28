using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace KoalageddonSafer
{
    class Steam
    {
        const string steamRegistryKey = @"SOFTWARE\Valve\Steam";
        public static string steamClientPath;

        public static void GetSteamInstallDir()
        {
            RegistryKey steamData = Registry.CurrentUser.OpenSubKey(steamRegistryKey);
            if (steamData != null)
            {
                if (steamData.GetValue("SteamPath") is string steamPathValue)
                {
                    if (Directory.Exists(steamPathValue))
                    {
                        string steamPath = new CultureInfo("en-US", false).TextInfo.ToTitleCase(steamPathValue);

                        Program.Message(Program.E_MessageType.PRINT, $"Steam Client Found On Path: \"{steamPath}\"");
                        steamClientPath = steamPath;

                        steamData.Close();
                        return;
                    }

                    Program.Message(Program.E_MessageType.ERROR, $"Folder Specified In \"SteamPath\" SubKey from \"{steamData}\" Doesn't Exist!");
                    steamData.Close();
                }
                Program.Message(Program.E_MessageType.ERROR, $"Failed to Obtain \"SteamPath\" SubKey from \"{steamData}\"!");
                steamData.Close();
            }

            Program.Message(Program.E_MessageType.ERROR, $"Failed to Obtain \"Steam\" Key from \"{steamRegistryKey}\"!");
        }

        public static bool GetIsKoalageddonInstalled()
        {
            bool versionFileExists = File.Exists($"{steamClientPath}\\version.dll");
#if DEBUG
            Program.Message(Program.E_MessageType.DEBUG, $"GetIsKoalageddonInstalled() => {versionFileExists}");
#endif

            return versionFileExists;
        }
    }

    class Processes
    {
        static public void Monitor()
        {
            string[] targetNames = { "EasyAntiCheat", "_EAC", "_EOS", "BEService", "_BE" };
            Program.Message(Program.E_MessageType.PRINT, "Program began working...");

            while (true)
            {
                var matchingProcesses = Process.GetProcesses()
                    .Where(p => targetNames.Any(s => p.ProcessName.Contains(s)));

                if (matchingProcesses.Any())
                {
#if DEBUG
                    string foundProcessesMessage = "Monitor() => Found Matching Processes:\n";
                    foreach (var process in matchingProcesses)
                    {
                        foundProcessesMessage = foundProcessesMessage + $"{process.ProcessName} ";
                    }
                    Program.Message(Program.E_MessageType.DEBUG, foundProcessesMessage);
#endif
                    if (Steam.GetIsKoalageddonInstalled())
                    {
                        Program.Message(Program.E_MessageType.PRINT, "Anti-Cheat Protected Game Detected While Koalageddon Is Still Active!");
                        SoundPlayer soundPlayer = new SoundPlayer(Properties.Resources.sfx_warning);
                        soundPlayer.Play();

                        foreach (var process in matchingProcesses)
                        {
                            process.Kill();
                        }
                        DestroyGameServices();
                        soundPlayer.Dispose();
                    }
                }

                Thread.Sleep(1000);
            }
        }

        static public void ConsoleState()
        {
            while (true)
            {
                WinConsole.E_Appearance ConsoleState = WinConsole.GetAppearance();
#if DEBUG
                Program.Message(Program.E_MessageType.DEBUG, $"Console Window State: {ConsoleState}");
#endif
                if (WinConsole.GetAppearance() == WinConsole.E_Appearance.MINIMIZED)
                {
                    WinConsole.ChangeAppearance(WinConsole.E_Appearance.HIDE);
                    Program.notifyIcon.Visible = true;
                }
                else
                {
                    Program.notifyIcon.Visible = false;
                }

                Thread.Sleep(250);
            }
        }

        static void DestroyGameServices()
        {
            string[] targetNames = { "steam", "EpicGamesLauncher" };
            var matchingProcesses = Process.GetProcesses()
                    .Where(p => targetNames.Any(s => p.ProcessName.Contains(s)));

            if (matchingProcesses.Any())
            {
#if DEBUG
                string foundProcessesMessage = "DestroyGameServices() => Found Matching Processes:\n";
                foreach (var process in matchingProcesses)
                {
                    foundProcessesMessage = foundProcessesMessage + $"{process.ProcessName} ";
                }
                Program.Message(Program.E_MessageType.DEBUG, foundProcessesMessage);
#endif

                foreach (var process in matchingProcesses)
                {
                    process.Kill();
                }
            }
            
        }
    }

    class WinConsole
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);


        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int X;
            public int Y;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        public enum E_Appearance
        {
            HIDE,
            NORMAL,
            MINIMIZED,
            MAXIMIZED,
            NOACTIVE,
            SHOW,
            MINIMIZE,
            MINNOACTIVE,
            NA,
            RESTORE,
            DEFAULT,
            FORCEMINIMIZE
        }

        public static E_Appearance GetAppearance()
        {
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);

            GetWindowPlacement(GetConsoleWindow(), ref placement);

            int showCmd = placement.showCmd;
            return (E_Appearance)showCmd;
        }
        public static void ChangeAppearance(E_Appearance newAppearance)
        {
            ShowWindow(GetConsoleWindow(), (int)newAppearance);
        }
        public static void SetTitle(string title)
        {
            Console.Title = title;
        }
        public static void SetIcon(Icon icon)
        {
            SendMessage(GetConsoleWindow(), 0x80, IntPtr.Zero, icon.Handle);
        }
    }

    class Program
    {
        public static NotifyIcon notifyIcon;

        public enum E_MessageType
        {
            PRINT,
            DEBUG,
            ERROR
        }
        public static void Message(E_MessageType type, string text)
        {
            switch (type)
            {
                case E_MessageType.PRINT:
                    Console.Write($"[MESSAGE] {text}");
                    break;

                case E_MessageType.DEBUG:
                    Console.Write($"[DEBUG] {text}");
                    break;

                case E_MessageType.ERROR:
                    Console.Write($"[ERROR] {text}\nPress ENTER to continue...");
                    Console.ReadLine();

                    Environment.Exit(0);
                    break;
            }
            Console.WriteLine("");

        }
        static bool GetIsRunningAdmin()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            bool isRunningAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
#if DEBUG
            Program.Message(Program.E_MessageType.DEBUG, $"GetIsRunningAdmin() => {isRunningAdmin}");
#endif
            return isRunningAdmin;
        }

        [STAThread]
        static void Main(string[] args)
        {
            WinConsole.SetTitle(AppDomain.CurrentDomain.FriendlyName
                .Replace(".exe", string.Empty)
                .Replace(".com", string.Empty));
            if (GetIsRunningAdmin() == false)
            {
                Message(E_MessageType.ERROR, "Launch Program As Administrator!");
                return;
            }
            WinConsole.SetIcon(Properties.Resources.icon);

            // NotifyIcon, Hidden By Default
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = Properties.Resources.icon;
            notifyIcon.Visible = true;

            notifyIcon.DoubleClick += (s, a) => WinConsole.ChangeAppearance(WinConsole.E_Appearance.NORMAL);

            var contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add("Show", (s, e) => WinConsole.ChangeAppearance(WinConsole.E_Appearance.NORMAL));
            contextMenu.MenuItems.Add("Exit", (s, e) => Environment.Exit(0));
            notifyIcon.ContextMenu = contextMenu;
            // ================================

            Steam.GetSteamInstallDir();

            Thread monitorThread = new Thread(Processes.Monitor);
            monitorThread.Start();

            Thread consoleStateThread = new Thread(Processes.ConsoleState);
            consoleStateThread.Start();

            Application.Run();
        }
    }
}

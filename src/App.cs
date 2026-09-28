using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32.SafeHandles;
using ErAudioTool.CLI;
using ErAudioTool.UI;

namespace ErAudioTool
{
    public class App : Application
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 0x00000003;

        [STAThread]
        public static int Main(string[] args)
        {
            // If arguments were provided and not explicitly requesting GUI, run in CLI mode
            if (args != null && args.Length > 0 && !HasGuiArg(args))
            {
                RedirectConsoleOutput();
                return CommandLineRunner.Run(args);
            }

            // Launch WPF GUI
            var app = new App();
            var window = new MainWindow();
            return app.Run(window);
        }

        private static bool HasGuiArg(string[] args)
        {
            foreach (var a in args)
            {
                if (string.Equals(a, "--gui", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "-g", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void RedirectConsoleOutput()
        {
            try
            {
                if (AttachConsole(ATTACH_PARENT_PROCESS))
                {
                    IntPtr handle = CreateFile("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                    if (handle != IntPtr.Zero && handle != new IntPtr(-1))
                    {
                        var safeHandle = new SafeFileHandle(handle, true);
                        var fs = new FileStream(safeHandle, FileAccess.Write);
                        var writer = new StreamWriter(fs, System.Text.Encoding.Default) { AutoFlush = true };
                        Console.SetOut(writer);
                        Console.SetError(writer);
                    }
                }
            }
            catch { }
        }
    }
}

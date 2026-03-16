using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace LockComputer.Watchdog
{
    internal class Program
    {
        private const string AppExeName = "LockComputer.exe";
        private const string AppProcessName = "LockComputer";

        private static readonly TimeSpan LegitShutdownWindow = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

        private static DateTime? _intentionalExitUntil;

        static void Main(string[] args)
        {
            using var mutex = new Mutex(
                initiallyOwned: true,
                name: @"Global\LockComputer.Watchdog",
                createdNew: out bool createdNew);

            if (!createdNew)
            {
                Log("Another watchdog instance already running. Exiting.");
                return;
            }

            Log("Watchdog started. BaseDir=" + AppContext.BaseDirectory);

            while (true)
            {
                try
                {
                    if (WasLegitShutdownRecently())
                    {
                        Log("Detected recent shutdown.flag -> skipping restart this cycle.");
                    }
                    else
                    {
                        EnsureAppRunning();
                    }
                }
                catch (Exception ex)
                {
                    Log("ERROR: " + ex);
                }

                Thread.Sleep(CheckInterval);
            }
        }

        private static void EnsureAppRunning()
        {
            if (Process.GetProcessesByName(AppProcessName).Any())
                return;

            if (IsInsideIntentionalExitWindow())
            {
                Log("Inside intentional exit window -> not restarting app.");
                return;
            }

            if (TryConsumeIntentionalExitFlag())
            {
                Log("Intentional exit detected from exit.flag -> not restarting app.");
                return;
            }

            var exePath = Path.Combine(AppContext.BaseDirectory, AppExeName);

            if (!File.Exists(exePath))
            {
                Log($"ERROR: Missing {AppExeName} in watchdog folder. Expected: {exePath}");
                return;
            }

            Log("App not running -> starting. Path=" + exePath);

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
                UseShellExecute = true
            };

            var p = Process.Start(psi);

            Log("Process.Start returned: " + (p == null ? "null" : $"PID={p.Id}"));
        }

        private static bool IsInsideIntentionalExitWindow()
        {
            if (!_intentionalExitUntil.HasValue)
                return false;

            if (DateTime.Now <= _intentionalExitUntil.Value)
                return true;

            Log("Intentional exit window expired.");
            _intentionalExitUntil = null;
            return false;
        }

        private static bool TryConsumeIntentionalExitFlag()
        {
            var flagPath = Path.Combine(GetSharedDir(), "exit.flag");

            Log("Checking exit flag at: " + flagPath);

            if (!File.Exists(flagPath))
            {
                Log("exit.flag not found.");
                return false;
            }

            try
            {
                var text = File.ReadAllText(flagPath).Trim();
                Log("exit.flag content: " + text);

                if (!DateTime.TryParseExact(
                        text,
                        "O",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var when))
                {
                    Log("exit.flag parse FAILED.");
                    File.Delete(flagPath);
                    return false;
                }

                var age = DateTime.Now - when;
                Log($"exit.flag age: {age.TotalSeconds:0}s");

                if (age < TimeSpan.Zero)
                {
                    Log("exit.flag timestamp is in the future. Deleting.");
                    File.Delete(flagPath);
                    return false;
                }

                if (age > LegitShutdownWindow)
                {
                    Log("exit.flag expired. Deleting.");
                    File.Delete(flagPath);
                    return false;
                }

                _intentionalExitUntil = when + LegitShutdownWindow;

                File.Delete(flagPath);
                Log($"exit.flag accepted and deleted. Intentional exit window until {_intentionalExitUntil:yyyy-MM-dd HH:mm:ss}.");

                return true;
            }
            catch (Exception ex)
            {
                Log("exit.flag read/parse ERROR: " + ex);
                return false;
            }
        }

        private static bool WasLegitShutdownRecently()
        {
            try
            {
                var flagPath = Path.Combine(AppContext.BaseDirectory, "shutdown.flag");

                if (!File.Exists(flagPath))
                    return false;

                var text = File.ReadAllText(flagPath).Trim();

                if (!DateTime.TryParse(
                        text,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var when))
                {
                    File.Delete(flagPath);
                    return false;
                }

                var age = DateTime.Now - when;
                var isRecent = age >= TimeSpan.Zero && age <= LegitShutdownWindow;

                File.Delete(flagPath);

                return isRecent;
            }
            catch
            {
                return false;
            }
        }

        private static string GetSharedDir()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "LockComputer");

            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void Log(string msg)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "watchdog.log");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}");
            }
            catch
            {
                // ignore
            }
        }
    }
}
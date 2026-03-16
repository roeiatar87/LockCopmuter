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
        private const string AppExeName = "LockCopmuter.exe";
        private const string AppProcessName = "LockCopmuter"; // בלי .exe
        private static readonly TimeSpan LegitShutdownWindow = TimeSpan.FromSeconds(90); // 90 שניות "חוקיות"

        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

        static void Main(string[] args)
        {


            // 1) Mutex כדי שלא יהיו 2 watchdogים במקביל
            using var mutex = new Mutex(initiallyOwned: true, name: @"Global\LockCopmuter.Watchdog", createdNew: out bool createdNew);
            if (!createdNew)
            {
                Log("Another watchdog instance already running. Exiting.");
                return;
            }

            Log("Watchdog started. BaseDir=" + AppContext.BaseDirectory);

            // 2) לולאה אינסופית: בדיקה כל 5 שניות
            while (true)
            {
                try
                {
                    // ✅ אם הייתה יציאה חוקית לאחרונה – לא להרים מחדש עכשיו
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
            // אם האפליקציה כבר רצה - לא עושים כלום
            if (Process.GetProcessesByName(AppProcessName).Any())
                return;

            // ✅ אם הייתה יציאה מכוונת (עם סיסמה) - לא מפעילים מחדש
            if (IsIntentionalExit())
            {
                Log("Intentional exit detected → Not restarting app.");
                return;
            }

            var exePath = Path.Combine(AppContext.BaseDirectory, AppExeName);
            if (!File.Exists(exePath))
            {
                Log($"ERROR: Missing {AppExeName} in watchdog folder. Expected: {exePath}");
                return;
            }

            Log("App not running → starting. Path=" + exePath);

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
                UseShellExecute = true
            };

            var p = Process.Start(psi);
            Log("Process.Start returned: " + (p == null ? "null" : $"PID={p.Id}"));
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

        private static bool WasLegitShutdownRecently()
        {
            try
            {
                var flagPath = Path.Combine(AppContext.BaseDirectory, "shutdown.flag");
                if (!File.Exists(flagPath))
                    return false;

                var text = File.ReadAllText(flagPath).Trim();
                if (!DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var when))
                {
                    File.Delete(flagPath);
                    return false;
                }

                var age = DateTime.Now - when;
                if (age >= TimeSpan.Zero && age <= TimeSpan.FromSeconds(90))
                {
                    File.Delete(flagPath); // אופציונלי, אבל מומלץ
                    return true;
                }

                File.Delete(flagPath);
                return false;
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
                "LockCopmuter");

            Directory.CreateDirectory(dir);
            return dir;
        }

        private static bool IsIntentionalExit()
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

                // חשוב: InvariantCulture בשביל פורמט "O"
                if (!DateTime.TryParseExact(
                        text,
                        "O",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var when))
                {
                    Log("exit.flag parse FAILED.");
                    return false;
                }

                var age = DateTime.Now - when;
                Log($"exit.flag age: {age.TotalSeconds:0}s");

                // חלון זמן "מורשה" (אפשר לשנות)
                return age < TimeSpan.FromMinutes(2);
            }
            catch (Exception ex)
            {
                Log("exit.flag read/parse ERROR: " + ex);
                return false;
            }
        }


    }
}

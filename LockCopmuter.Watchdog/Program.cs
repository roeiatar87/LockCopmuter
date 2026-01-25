using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace LockCopmuter.Watchdog
{
    internal class Program
    {
        // ✅ תעדכן לשם המדויק שיוצא לך ב-bin
        private const string AppExeName = "LockCopmuter.exe";
        private const string AppProcessName = "LockCopmuter"; // בלי .exe

        static void Main(string[] args)
        {
            try
            {
                Log("Watchdog started. BaseDir=" + AppContext.BaseDirectory);

                if (Process.GetProcessesByName(AppProcessName).Any())
                {
                    Log("App already running. Exiting.");
                    return;
                }

                using var mutex = new Mutex(false, @"Global\LockCopmuter.Watchdog");
                if (!mutex.WaitOne(TimeSpan.FromSeconds(2)))
                {
                    Log("Mutex busy. Exiting.");
                    return;
                }

                var exePath = FindExePath(AppExeName);
                if (exePath == null)
                {
                    Log($"ERROR: Could not find {AppExeName} near watchdog.");
                    return;
                }

                Log("Found app path: " + exePath);

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath)!,
                    UseShellExecute = true
                };

                var p = Process.Start(psi);
                Log("Process.Start returned: " + (p == null ? "null" : $"PID={p.Id}"));

                // אופציונלי: מחכים רגע כדי לראות אם קרס מיד
                Thread.Sleep(1500);
                var running = Process.GetProcessesByName(AppProcessName).Any();
                Log("After 1.5s running=" + running);
            }
            catch (Exception ex)
            {
                Log("FATAL: " + ex);
            }
        }

        private static string? FindExePath(string exeName)
        {
            // 1) אותה תיקייה
            var baseDir = AppContext.BaseDirectory;
            var p1 = Path.Combine(baseDir, exeName);
            if (File.Exists(p1)) return p1;

            // 2) תיקיית אב
            var parent = Directory.GetParent(baseDir)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent))
            {
                var p2 = Path.Combine(parent, exeName);
                if (File.Exists(p2)) return p2;
            }

            // 3) חיפוש רקורסיבי (עד עומק סביר) ליד ה-watchdog
            try
            {
                var root = parent ?? baseDir;
                var found = Directory
                    .EnumerateFiles(root, exeName, SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (found != null) return found;
            }
            catch { /* ignore */ }

            return null;
        }

        private static void Log(string msg)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "watchdog.log");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}");
            }
            catch { /* ignore */ }
        }
    }
}

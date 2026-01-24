using System.IO;

namespace LockCopmuter.Services;

public interface ILogger
{
    void Info(string message);
    void Error(string message, Exception? ex = null);
    string LogPath { get; }
}

public class FileLogger : ILogger
{
    public string LogPath { get; }

    private readonly object _lock = new();

    public FileLogger()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "LockCopmuter");
        Directory.CreateDirectory(dir);

        LogPath = Path.Combine(dir, "app.log");
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message} | {ex}");

    private void Write(string level, string msg)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}";
        lock (_lock)
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
    }
}

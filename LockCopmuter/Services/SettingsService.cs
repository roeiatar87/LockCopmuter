using System.IO;
using System.Text.Json;
using LockCopmuter.Models;

namespace LockCopmuter.Services;

public interface ISettingsService
{
    ScheduleSettings Load();
    void Save(ScheduleSettings settings);
    string SettingsPath { get; }
}

public class SettingsService : ISettingsService
{
    public string SettingsPath { get; }

    private static readonly JsonSerializerOptions Opt = new()
    {
        WriteIndented = true
    };

    public SettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "LockCopmuter");
        Directory.CreateDirectory(dir);

        SettingsPath = Path.Combine(dir, "settings.json");
    }

    public ScheduleSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            var s = ScheduleSettings.CreateDefault();
            Save(s);
            return s;
        }

        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<ScheduleSettings>(json, Opt) ?? ScheduleSettings.CreateDefault();
    }

    public void Save(ScheduleSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, Opt);
        File.WriteAllText(SettingsPath, json);
    }
}

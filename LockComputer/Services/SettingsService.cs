using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using LockComputer.Models;

namespace LockComputer.Services;

public interface ISettingsService
{
    ScheduleSettings Load();
    void Save(ScheduleSettings settings);
    string SettingsPath { get; }
}

public class SettingsService : ISettingsService
{
    public string SettingsPath { get; }

    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions Opt = new()
    {
        WriteIndented = true
    };

    public SettingsService(ILogger logger)
    {
        _logger = logger;

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "LockCopmuter");
        Directory.CreateDirectory(dir);

        SettingsPath = Path.Combine(dir, "settings.json");
    }

    public ScheduleSettings Load()
    {
        // אין קובץ -> ברירת מחדל + שמירה
        if (!File.Exists(SettingsPath))
        {
            var s0 = ScheduleSettings.CreateDefault();
            s0.Normalize();
            Save(s0);
            _logger.Info($"Settings not found. Created default at: {SettingsPath}");
            return s0;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var s = JsonSerializer.Deserialize<ScheduleSettings>(json, Opt) ?? ScheduleSettings.CreateDefault();

            // לוג מצב "לפני Normalize" (כדי לראות כפילויות!)
            LogSunday("BEFORE Normalize", s);

            // Normalize (מנקה כפילויות/חוסרים)
            s.Normalize();

            // לוג מצב "אחרי Normalize"
            LogSunday("AFTER Normalize", s);

            // אם Normalize שינה משהו, ננקה את הקובץ פעם אחת (אבל לא כל פעם)
            // השוואה פשוטה: Serialize מחדש והשוואה מול המקור (יעיל מספיק לקובץ קטן)
            var normalizedJson = JsonSerializer.Serialize(s, Opt);
            if (!string.Equals(NormalizeNewlines(json), NormalizeNewlines(normalizedJson), StringComparison.Ordinal))
            {
                File.WriteAllText(SettingsPath, normalizedJson);
                _logger.Info("Settings normalized and written back to file (one-time cleanup).");
            }

            return s;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to load settings.json. Backing up and falling back to default.", ex);

            // JSON פגום -> גיבוי + ברירת מחדל
            try
            {
                var backup = SettingsPath + ".corrupt." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak";
                File.Copy(SettingsPath, backup, overwrite: true);
                _logger.Info($"Backed up corrupt settings to: {backup}");
            }
            catch (Exception bx)
            {
                _logger.Error("Failed to backup corrupt settings.", bx);
            }

            var s0 = ScheduleSettings.CreateDefault();
            s0.Normalize();
            Save(s0);
            return s0;
        }
    }

    public void Save(ScheduleSettings settings)
    {
        settings.Normalize();

        var json = JsonSerializer.Serialize(settings, Opt);

        // כתיבה אטומית: קודם temp ואז Replace
        var tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, json);

        try
        {
            if (File.Exists(SettingsPath))
                File.Replace(tmp, SettingsPath, SettingsPath + ".bak", ignoreMetadataErrors: true);
            else
                File.Move(tmp, SettingsPath);

            _logger.Info($"Settings saved: {SettingsPath}");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to save settings.", ex);

            // fallback
            try
            {
                if (File.Exists(SettingsPath))
                    File.Delete(SettingsPath);

                File.Move(tmp, SettingsPath);
                _logger.Info("Settings saved with fallback move.");
            }
            catch (Exception ex2)
            {
                _logger.Error("Fallback save also failed.", ex2);
                throw;
            }
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }

    private void LogSunday(string label, ScheduleSettings s)
    {
        try
        {
            var count = s.Rules.Count(r => r.Day == DayOfWeek.Sunday);
            var ranges = string.Join(", ",
                s.Rules.Where(r => r.Day == DayOfWeek.Sunday)
                       .SelectMany(r => r.Ranges)
                       .Select(x => $"{x.Start:hh\\:mm}-{x.End:hh\\:mm}")
            );

            _logger.Info($"{label}: Sunday rules={count}; Sunday ranges=[{ranges}]");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to log Sunday rules/ranges.", ex);
        }
    }

    private static string NormalizeNewlines(string s)
        => s.Replace("\r\n", "\n");
}

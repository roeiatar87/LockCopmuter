using System.Diagnostics;
using System.Windows;
using LockCopmuter.Models;
using LockCopmuter.Services;
using LockCopmuter.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace LockCopmuter.ViewModels;

public class MainViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IPasswordService _passwordService;
    private readonly ILogger _logger;

    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand OpenLogCommand { get; }
    public RelayCommand CloseWindowCommand { get; }

    public string StatusText { get; }
    public string NextRestrictionText { get; }
    public string PasswordStatusText { get; }
    public string TipText { get; }

    public string StatusPillText { get; }
    public string StatusPillBackground { get; }

    public MainViewModel(ISettingsService settingsService, IPasswordService passwordService, ILogger logger)
    {
        _settingsService = settingsService;
        _passwordService = passwordService;
        _logger = logger;

        var s = _settingsService.Load();

        PasswordStatusText = _passwordService.HasPassword(s) ? "מוגדרת" : "לא מוגדרת";
        StatusText = "האפליקציה פועלת ברקע ומבצעת אכיפה לפי הזמנים שהוגדרו.";
        TipText = "טיפ: מומלץ להגדיר סיסמה, ולהשתמש במשתמש רגיל לילד כדי למנוע סגירה/שינוי.";

        var next = FindNextRestriction(s, DateTime.Now);
        NextRestrictionText = next ?? "לא נמצא חלון חסימה בקרוב";

        // סטטוס “פעיל/פתוח”
        StatusPillText = "פעיל";
        StatusPillBackground = "#16A34A";

        OpenSettingsCommand = new RelayCommand(OpenSettings);
        OpenLogCommand = new RelayCommand(OpenLog);
        CloseWindowCommand = new RelayCommand(() => Application.Current.MainWindow?.Hide());
    }

    private void OpenLog()
    {
        try
        {
            var logPath = _logger.LogPath;
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{logPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error("OpenLog failed", ex);
            MessageBox.Show("לא הצלחתי לפתוח לוג.", "LockCopmuter", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenSettings()
    {
        var s = _settingsService.Load();

        if (_passwordService.HasPassword(s))
        {
            var dlg = new PasswordPromptWindow(
                "גישה להגדרות נעולה",
                "הזן סיסמה כדי להמשיך"
            );
            if (dlg.ShowDialog() != true) return;

            if (!_passwordService.Verify(s, dlg.Password))
            {
                MessageBox.Show("סיסמה שגויה.", "נעול", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var vm = new SettingsViewModel(_settingsService, _logger, _passwordService);
        var win = new SettingsWindow(vm);
        win.Show();
        win.Activate();
    }

    private static string? FindNextRestriction(ScheduleSettings s, DateTime now)
    {
        for (int dayOffset = 0; dayOffset <= 2; dayOffset++)
        {
            var date = now.Date.AddDays(dayOffset);
            var dow = date.DayOfWeek;

            var rule = s.Rules.FirstOrDefault(r => r.Day == dow && r.Enabled);
            if (rule == null) continue;

            foreach (var r in rule.Ranges.OrderBy(x => x.Start))
            {
                var start = date + r.Start;
                var end = date + r.End;

                if (dayOffset == 0 && end <= now)
                    continue; // כבר עבר

                // 🔴 כרגע בתוך חסימה
                if (dayOffset == 0 && start <= now)
                    return $"פעיל עכשיו עד {end:HH\\:mm}";

                // 🟢 חסימה עתידית
                string prefix = dayOffset switch
                {
                    0 => "היום",
                    1 => "מחר",
                    _ => DayToHebrew(dow)
                };

                return $"{prefix} {start:HH\\:mm}–{end:HH\\:mm}";
            }
        }

        return null;
    }


    private static string DayToHebrew(DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => "ראשון",
        DayOfWeek.Monday => "שני",
        DayOfWeek.Tuesday => "שלישי",
        DayOfWeek.Wednesday => "רביעי",
        DayOfWeek.Thursday => "חמישי",
        DayOfWeek.Friday => "שישי",
        DayOfWeek.Saturday => "שבת",
        _ => ""
    };
}

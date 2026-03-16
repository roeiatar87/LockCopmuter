using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using LockComputer.Models;
using LockComputer.Services;
using LockComputer.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace LockComputer.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settingsService;
    private readonly IPasswordService _passwordService;
    private readonly ILogger _logger;

    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand OpenLogCommand { get; }
    public RelayCommand CloseWindowCommand { get; }

    public string StatusText { get; } =
        "האפליקציה פועלת ברקע ומבצעת אכיפה לפי הזמנים שהוגדרו.";

    public string TipText { get; } =
        "טיפ: מומלץ להגדיר סיסמה, ולהשתמש במשתמש רגיל לילד כדי למנוע סגירה/שינוי.";

    private string _nextRestrictionText = "טוען…";
    public string NextRestrictionText
    {
        get => _nextRestrictionText;
        set { _nextRestrictionText = value; OnPropertyChanged(); }
    }

    private string _passwordStatusText = "";
    public string PasswordStatusText
    {
        get => _passwordStatusText;
        set { _passwordStatusText = value; OnPropertyChanged(); }
    }

    public string StatusPillText { get; } = "פעיל";
    public string StatusPillBackground { get; } = "#16A34A";

    public MainViewModel(ISettingsService settingsService, IPasswordService passwordService, ILogger logger)
    {
        _settingsService = settingsService;
        _passwordService = passwordService;
        _logger = logger;

        OpenSettingsCommand = new RelayCommand(OpenSettings);
        OpenLogCommand = new RelayCommand(OpenLog);
        CloseWindowCommand = new RelayCommand(() => Application.Current.MainWindow?.Hide());

        Refresh();
    }

    public void Refresh()
    {
        var s = _settingsService.Load();
        s.Normalize();

        var now = DateTime.Now;

        var todayRule = s.Rules.Where(r => r.Enabled && r.Day == now.DayOfWeek).LastOrDefault();
        var todayRanges = todayRule == null
            ? "<no rule>"
            : string.Join(", ", todayRule.Ranges.Select(x => $"{x.Start:hh\\:mm}-{x.End:hh\\:mm}"));

        _logger.Info($"NOW={now:yyyy-MM-dd HH:mm} DOW={now.DayOfWeek} TodayRanges=[{todayRanges}] IsRestrictedNow={s.IsRestrictedNow(now)}");

        PasswordStatusText = _passwordService.HasPassword(s) ? "מוגדרת" : "לא מוגדרת";

        // ✅ 1) קודם כל: אם אנחנו בתוך חלון חסימה פעיל – להציג את זה
        var active = GetActiveRestrictionWindow(s, now);
        if (active != null)
        {
            var (start, end) = active.Value;

            // תצוגה ברורה גם אם חוצה יום
            var endText = end.Date == start.Date
                ? $"{end:HH:mm}"
                : $"{end:HH:mm} (למחרת)";

            NextRestrictionText = $"פעיל עכשיו {start:HH:mm}–{endText}";
            _logger.Info($"Active NOW: start={start:yyyy-MM-dd HH:mm} end={end:yyyy-MM-dd HH:mm}");
            return;
        }

        // ✅ 2) אם לא פעיל עכשיו – לחשב את החלון הבא
        var next = FindNextRestriction(s, now, daysForward: 7);
        _logger.Info("NextRestrictionText computed = " + (next ?? "<null>"));
        NextRestrictionText = next ?? "לא נמצא חלון חסימה בקרוב";
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
        s.Normalize();

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
        win.Closed += (_, __) => Refresh();
        win.Show();
        win.Activate();
    }

    /// <summary>
    /// ✅ מחזיר חלון חסימה פעיל "עכשיו" כולל:
    /// - חלון רגיל של היום
    /// - חלון שחוצה חצות והתחיל אתמול (לדוגמה 17:00–15:00 ואתה בבוקר/בלילה)
    /// חריגים מבטלים (אם היום חריג – אין חסימה גם אם התחילה אתמול).
    /// </summary>
    private static (DateTime start, DateTime end)? GetActiveRestrictionWindow(ScheduleSettings s, DateTime now)
    {
        var today = now.Date;

        // אם היום חריג – אין חסימה היום (כולל "המשך" של אתמול שחצה חצות)
        if (s.IsExceptionDay(today))
            return null;

        // 1) בדיקה של טווחים של היום
        {
            var ruleToday = s.Rules.Where(r => r.Enabled && r.Day == today.DayOfWeek).LastOrDefault();
            if (ruleToday != null)
            {
                foreach (var r in ruleToday.Ranges.OrderBy(x => x.Start))
                {
                    var start = today + r.Start;
                    var end = today + r.End;
                    if (r.End <= r.Start) end = today.AddDays(1) + r.End;

                    if (start <= now && now < end)
                        return (start, end);
                }
            }
        }

        // 2) בדיקה של "המשך" מחלון שחצה חצות והתחיל אתמול
        //    זה רלוונטי במיוחד אם עכשיו אחרי חצות ועדיין בתוך החלק של הבוקר/צהריים.
        {
            var yesterday = today.AddDays(-1);

            // אם אתמול היה חריג – החלון לא היה אמור להתחיל
            if (!s.IsExceptionDay(yesterday))
            {
                var ruleY = s.Rules.Where(r => r.Enabled && r.Day == yesterday.DayOfWeek).LastOrDefault();
                if (ruleY != null)
                {
                    foreach (var r in ruleY.Ranges.OrderBy(x => x.Start))
                    {
                        if (r.End > r.Start) continue; // רק טווחים שחוצים חצות

                        var start = yesterday + r.Start;
                        var end = today + r.End; // מסתיים היום

                        if (start <= now && now < end)
                            return (start, end);
                    }
                }
            }
        }

        return null;
    }

    // מחפש את החלון הבא (לא פעיל עכשיו)
    private string? FindNextRestriction(ScheduleSettings s, DateTime now, int daysForward)
    {
        for (int dayOffset = 0; dayOffset <= daysForward; dayOffset++)
        {
            var date = now.Date.AddDays(dayOffset);

            if (s.IsExceptionDay(date))
                continue;

            var dow = date.DayOfWeek;

            var rule = s.Rules
                .Where(r => r.Day == dow && r.Enabled)
                .LastOrDefault();

            if (rule == null) continue;

            foreach (var r in rule.Ranges.OrderBy(x => x.Start))
            {
                var start = date + r.Start;
                var end = date + r.End;

                if (r.End <= r.Start)
                    end = date.AddDays(1) + r.End;

                if (dayOffset == 0 && end <= now)
                    continue;

                var prefix = dayOffset switch
                {
                    0 => "היום",
                    1 => "מחר",
                    _ => DayToHebrew(dow)
                };

                _logger.Info($"Next picked: date={date:yyyy-MM-dd} dow={dow} start={start:HH:mm} end={end:HH:mm} (endDate={end:yyyy-MM-dd})");

                var rangeText = (r.End <= r.Start)
                    ? $"{start:HH:mm}–{end:HH:mm} (למחרת)"
                    : $"{start:HH:mm}–{end:HH:mm}";

                return $"{prefix} {rangeText}";
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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

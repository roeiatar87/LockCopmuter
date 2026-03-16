using System.Windows;
using LockComputer.Models;
using LockComputer.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace LockComputer.Services;

public class WarningUi : IWarningUi
{
    private WarningWindow? _win;

    private readonly ISettingsService _settingsService;
    private readonly IPasswordService _passwordService;

    public DateTime? SuppressedUntil { get; private set; }

    public WarningUi(ISettingsService settingsService, IPasswordService passwordService)
    {
        _settingsService = settingsService;
        _passwordService = passwordService;
    }

    public void ShowWarning(DateTime targetTime, int minutesBefore)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            EnsureWindow();

            // minutesBefore == 0 => זו נעילה עד targetTime
            if (minutesBefore == 0)
            {
                _win!.Configure(
                    targetTime: targetTime,
                    actionText: "נעילת מחשב",
                    allowCancel: true,
                    suffixText: "כדי לבטל את הנעילה האוטומטית - נדרשת סיסמה."
                );
                return;
            }

            // אחרת => אזהרה עד תחילת חסימה עתידית
            _win!.Configure(
                targetTime: targetTime,
                actionText: "תחילת חסימה",
                allowCancel: false
            );
        });
    }

    public void CloseWarning()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_win != null)
            {
                _win.Close();
                _win = null;
            }
        });
    }

    private void EnsureWindow()
    {
        if (_win == null)
        {
            _win = new WarningWindow();
            _win.CancelWithPasswordRequested += (_, __) => TryCancelUntilEndOfCurrentWindow();
            _win.Closed += (_, __) => _win = null;
            _win.Show();
        }
        else
        {
            if (!_win.IsVisible) _win.Show();
            _win.Activate();
        }
    }

    private void TryCancelUntilEndOfCurrentWindow()
    {
        var s = _settingsService.Load();

        if (!_passwordService.HasPassword(s))
        {
            MessageBox.Show("לא מוגדרת סיסמה. לא ניתן לבטל חסימה.", "LockComputer",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new PasswordPromptWindow(
            "ביטול חסימה",
            "הזן סיסמה כדי לבטל את הנעילה האוטומטית עד סוף חלון החסימה"
        );

        if (dlg.ShowDialog() != true) return;

        if (!_passwordService.Verify(s, dlg.Password))
        {
            MessageBox.Show("סיסמה שגויה.", "נעול", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var now = DateTime.Now;
        var end = FindCurrentRestrictionEnd(s, now);

        SuppressedUntil = end ?? now.AddMinutes(30);
        _win?.Close();
    }

    private static DateTime? FindCurrentRestrictionEnd(ScheduleSettings s, DateTime now)
    {
        if (s.IsExceptionDay(now.Date)) return null;

        var rule = s.Rules.FirstOrDefault(r => r.Enabled && r.Day == now.DayOfWeek);
        if (rule == null) return null;

        foreach (var r in rule.Ranges.OrderBy(x => x.Start))
        {
            var start = now.Date + r.Start;
            var end = now.Date + r.End;

            if (r.End <= r.Start)
                end = now.Date.AddDays(1) + r.End;

            if (start <= now && now < end)
                return end;
        }

        return null;
    }
}
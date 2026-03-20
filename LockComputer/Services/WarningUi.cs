using System;
using System.Linq;
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

    public event EventHandler? CountdownFinished;

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

            var settings = _settingsService.Load();
            var actionText = GetActionText(settings.EnforcementMode);

            if (minutesBefore == 0)
            {
                _win!.Configure(
                    targetTime: targetTime,
                    actionText: actionText,
                    allowCancel: true,
                    suffixText: "כדי לבטל את האכיפה האוטומטית - נדרשת סיסמה."
                );
                return;
            }

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
            _win.CountdownFinished += (_, __) => CountdownFinished?.Invoke(this, EventArgs.Empty);
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

        var cancelText = GetCancelActionText(s.EnforcementMode);

        var dlg = new PasswordPromptWindow(
            "ביטול חסימה",
            $"הזן סיסמה כדי {cancelText} עד סוף חלון החסימה"
        );

        if (dlg.ShowDialog() != true)
            return;

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

    private static string GetActionText(EnforcementMode mode) => mode switch
    {
        EnforcementMode.CloseRoblox => "סגירת Roblox",
        _ => "נעילת מחשב"
    };

    private static string GetCancelActionText(EnforcementMode mode) => mode switch
    {
        EnforcementMode.CloseRoblox => "לבטל את סגירת Roblox האוטומטית",
        _ => "לבטל את נעילת המחשב האוטומטית"
    };

    private static DateTime? FindCurrentRestrictionEnd(ScheduleSettings s, DateTime now)
    {
        if (s.IsExceptionDay(now.Date))
            return null;

        var rule = s.Rules.FirstOrDefault(r => r.Enabled && r.Day == now.DayOfWeek);
        if (rule == null)
            return null;

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
using System.Windows;
using LockCopmuter.Views;
using Application = System.Windows.Application;

namespace LockCopmuter.Services;

public class WarningUi : IWarningUi
{
    private WarningWindow? _win;

    public void ShowWarning(DateTime targetTime, int minutesBefore)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            EnsureWindow();

            // minutesBefore == 0 => זה כיבוי עד targetTime (שמגיע מ-EnforcementService)
            if (minutesBefore == 0)
            {
                _win!.Configure(
                    targetTime: targetTime, // חשוב: לא Now+30
                    actionText: "כיבוי",
                    suffixText: "כדי לבטל – נדרשת סיסמה."
                );
                return;
            }

            // אחרת => אזהרה עד תחילת חסימה
            _win!.Configure(
                targetTime: targetTime,
                actionText: "תחילת חסימה"
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
            _win.Closed += (_, __) => _win = null;
            _win.Show();
        }
        else
        {
            if (!_win.IsVisible) _win.Show();
            _win.Activate();
        }
    }
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace LockCopmuter.Views;

public partial class WarningWindow : Window, INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    private DateTime _target;
    private string _actionText = "כיבוי"; // ברירת מחדל
    private string _suffixText = "";      // תוספת אופציונלית

    private string _countdownText = "";
    public string CountdownText
    {
        get => _countdownText;
        set { _countdownText = value; OnPropertyChanged(); }
    }

    public WarningWindow()
    {
        InitializeComponent();
        DataContext = this;

        _timer.Tick += (_, __) => UpdateText();
        Closing += (_, __) => _timer.Stop();
    }

    /// <summary>
    /// יעד ספירה לאחור + טקסט פעולה (למשל "כיבוי", "נעילה") + טקסט תוספת אופציונלי.
    /// </summary>
    public void Configure(DateTime targetTime, string actionText, string? suffixText = null)
    {
        _target = targetTime;
        _actionText = string.IsNullOrWhiteSpace(actionText) ? "כיבוי" : actionText;
        _suffixText = suffixText ?? "";

        UpdateText();
        _timer.Start();
    }

    private void UpdateText()
    {
        var remaining = _target - DateTime.Now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

        // סה"כ שניות -> mm:ss אמיתי
        var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        var mm = totalSeconds / 60;
        var ss = totalSeconds % 60;

        var suffix = string.IsNullOrWhiteSpace(_suffixText) ? "" : $" {_suffixText}";

        CountdownText = $"נותרו {mm:00}:{ss:00} עד {_actionText} ({_target:HH:mm}).{suffix}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

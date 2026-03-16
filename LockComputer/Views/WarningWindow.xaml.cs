using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace LockComputer.Views;

public partial class WarningWindow : Window, INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    private DateTime _target;
    private string _actionText = "כיבוי"; // ברירת מחדל
    private string _suffixText = "";      // תוספת אופציונלית

    public event EventHandler? CancelWithPasswordRequested;

    private Visibility _cancelVisibility = Visibility.Collapsed;
    public Visibility CancelVisibility
    {
        get => _cancelVisibility;
        set { _cancelVisibility = value; OnPropertyChanged(); }
    }

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
    /// יעד ספירה לאחור + טקסט פעולה (למשל "כיבוי", "תחילת חסימה") + האם לאפשר ביטול עם סיסמה + טקסט תוספת אופציונלי.
    /// </summary>
    public void Configure(DateTime targetTime, string actionText, bool allowCancel, string? suffixText = null)
    {
        _target = targetTime;
        _actionText = string.IsNullOrWhiteSpace(actionText) ? "כיבוי" : actionText;
        _suffixText = suffixText ?? "";

        CancelVisibility = allowCancel ? Visibility.Visible : Visibility.Collapsed;

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

    // כפתור "אישור" (סוגר חלון)
    private void Ok_Click(object sender, RoutedEventArgs e) => Close();

    // כפתור "בטל (סיסמה)"
    private void CancelWithPassword_Click(object sender, RoutedEventArgs e)
        => CancelWithPasswordRequested?.Invoke(this, EventArgs.Empty);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace LockComputer.Views;

public partial class WarningWindow : Window, INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    private DateTime _target;
    private string _actionText = "אכיפה";
    private string _suffixText = "";
    private bool _countdownFinishedRaised;

    public event EventHandler? CancelWithPasswordRequested;
    public event EventHandler? CountdownFinished;

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

    public void Configure(DateTime targetTime, string actionText, bool allowCancel, string? suffixText = null)
    {
        _target = targetTime;
        _actionText = string.IsNullOrWhiteSpace(actionText) ? "אכיפה" : actionText;
        _suffixText = suffixText ?? "";
        _countdownFinishedRaised = false;

        CancelVisibility = allowCancel ? Visibility.Visible : Visibility.Collapsed;

        UpdateText();
        _timer.Start();
    }

    private void UpdateText()
    {
        var remaining = _target - DateTime.Now;

        if (remaining <= TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;

            CountdownText = BuildCountdownText(0);

            if (!_countdownFinishedRaised)
            {
                _countdownFinishedRaised = true;
                _timer.Stop();
                CountdownFinished?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        CountdownText = BuildCountdownText(totalSeconds);
    }

    private string BuildCountdownText(int totalSeconds)
    {
        var mm = totalSeconds / 60;
        var ss = totalSeconds % 60;

        var suffix = string.IsNullOrWhiteSpace(_suffixText) ? "" : $" {_suffixText}";
        return $"נותרו {mm:00}:{ss:00} עד {_actionText} ({_target:HH:mm}).{suffix}";
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();

    private void CancelWithPassword_Click(object sender, RoutedEventArgs e)
        => CancelWithPasswordRequested?.Invoke(this, EventArgs.Empty);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LockCopmuter.Models;
using LockCopmuter.Services;

namespace LockCopmuter.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger _log;
    private readonly IPasswordService _passwordService;

    public ScheduleSettings Settings { get; }
    public ObservableCollection<DayRule> Rules => Settings.Rules;

    private DayRule? _selectedDay;
    public DayRule? SelectedDay
    {
        get => _selectedDay;
        set
        {
            _selectedDay = value;
            OnPropertyChanged();
            AddRangeCommand.RaiseCanExecuteChanged();
            RemoveRangeCommand.RaiseCanExecuteChanged();
        }
    }
    public IReadOnlyList<int> Hours { get; } = Enumerable.Range(0, 24).ToList();
    public IReadOnlyList<int> Minutes { get; } = Enumerable.Range(0, 60).Where(m => m % 5 == 0).ToList();


    private TimeRange? _selectedRange;
    public TimeRange? SelectedRange
    {
        get => _selectedRange;
        set
        {
            _selectedRange = value;
            OnPropertyChanged();
            RemoveRangeCommand.RaiseCanExecuteChanged();
        }
    }

    private DateTime _newExceptionDate = DateTime.Today;
    public DateTime NewExceptionDate
    {
        get => _newExceptionDate;
        set { _newExceptionDate = value; OnPropertyChanged(); }
    }

    private DateTime _newRangeStart = DateTime.Today;
    public DateTime NewRangeStart
    {
        get => _newRangeStart;
        set { _newRangeStart = value; OnPropertyChanged(); }
    }

    private DateTime _newRangeEnd = DateTime.Today;
    public DateTime NewRangeEnd
    {
        get => _newRangeEnd;
        set { _newRangeEnd = value; OnPropertyChanged(); }
    }

    private DateTime? _selectedExceptionDate;
    public DateTime? SelectedExceptionDate
    {
        get => _selectedExceptionDate;
        set { _selectedExceptionDate = value; OnPropertyChanged(); RemoveExceptionDateCommand.RaiseCanExecuteChanged(); }
    }

    private DateRange? _selectedExceptionRange;
    public DateRange? SelectedExceptionRange
    {
        get => _selectedExceptionRange;
        set { _selectedExceptionRange = value; OnPropertyChanged(); RemoveExceptionRangeCommand.RaiseCanExecuteChanged(); }
    }

    public RelayCommand AddExceptionDateCommand { get; }
    public RelayCommand RemoveExceptionDateCommand { get; }
    public RelayCommand AddExceptionRangeCommand { get; }
    public RelayCommand RemoveExceptionRangeCommand { get; }


    public RelayCommand AddRangeCommand { get; }
    public RelayCommand RemoveRangeCommand { get; }
    public RelayCommand SaveCommand { get; }

    public SettingsViewModel(ISettingsService settingsService, ILogger log, IPasswordService passwordService)
    {
        _settingsService = settingsService;
        _log = log;
        _passwordService = passwordService;

        Settings = _settingsService.Load();

        AddRangeCommand = new RelayCommand(AddRange, () => SelectedDay != null);
        RemoveRangeCommand = new RelayCommand(RemoveRange, () => SelectedDay != null && SelectedRange != null);
        SaveCommand = new RelayCommand(Save);

        SelectedDay = Rules.FirstOrDefault();

        _log.Info("Settings window opened.");

        AddExceptionDateCommand = new RelayCommand(AddExceptionDate);
        RemoveExceptionDateCommand = new RelayCommand(RemoveExceptionDate, () => SelectedExceptionDate != null);

        AddExceptionRangeCommand = new RelayCommand(AddExceptionRange);
        RemoveExceptionRangeCommand = new RelayCommand(RemoveExceptionRange, () => SelectedExceptionRange != null);

    }


    private void AddRange()
    {
        if (SelectedDay == null) return;
        SelectedDay.Ranges.Add(new TimeRange { Start = new TimeSpan(15, 0, 0), End = new TimeSpan(17, 0, 0) });
    }

    private void RemoveRange()
    {
        if (SelectedDay == null || SelectedRange == null) return;
        SelectedDay.Ranges.Remove(SelectedRange);
        SelectedRange = null;
    }

    private void Save()
    {
        // ולידציה בסיסית מאוד
        foreach (var day in Rules)
        {
            foreach (var r in day.Ranges)
            {
                if (r.End <= r.Start)
                    throw new InvalidOperationException($"ביום {day.Day}: שעת סיום חייבת להיות אחרי שעת התחלה.");
            }
        }

        _settingsService.Save(Settings);
        _log.Info("Settings saved.");
    }

    public void TrySetPassword(string newPassword, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
            throw new InvalidOperationException("סיסמה חייבת להיות לפחות 4 תווים.");

        if (newPassword != confirmPassword)
            throw new InvalidOperationException("אימות סיסמה לא תואם.");

        _passwordService.SetPassword(Settings, newPassword);
        _settingsService.Save(Settings);
        _log.Info("Password updated.");
    }

    private void AddExceptionDate()
    {
        var d = NewExceptionDate.Date;

        if (!Settings.ExceptionDates.Any(x => x.Date == d))
            Settings.ExceptionDates.Add(d);
    }

    private void RemoveExceptionDate()
    {
        if (SelectedExceptionDate == null) return;
        var d = SelectedExceptionDate.Value.Date;

        var existing = Settings.ExceptionDates.FirstOrDefault(x => x.Date == d);
        if (existing != default)
            Settings.ExceptionDates.Remove(existing);

        SelectedExceptionDate = null;
    }

    private void AddExceptionRange()
    {
        var s = NewRangeStart.Date;
        var e = NewRangeEnd.Date;

        if (e < s)
            throw new InvalidOperationException("טווח לא תקין: תאריך סיום קטן מתאריך התחלה.");

        Settings.ExceptionRanges.Add(new DateRange { StartDate = s, EndDate = e });
    }

    private void RemoveExceptionRange()
    {
        if (SelectedExceptionRange == null) return;
        Settings.ExceptionRanges.Remove(SelectedExceptionRange);
        SelectedExceptionRange = null;
    }


    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

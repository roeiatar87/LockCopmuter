using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LockCopmuter.Models;

public class TimeRange : INotifyPropertyChanged
{
    private TimeSpan _start;
    public TimeSpan Start
    {
        get => _start;
        set { _start = value; OnPropertyChanged(); OnPropertyChanged(nameof(StartHour)); OnPropertyChanged(nameof(StartMinute)); }
    }

    private TimeSpan _end;
    public TimeSpan End
    {
        get => _end;
        set { _end = value; OnPropertyChanged(); OnPropertyChanged(nameof(EndHour)); OnPropertyChanged(nameof(EndMinute)); }
    }

    // Start helpers
    public int StartHour
    {
        get => Start.Hours;
        set => Start = new TimeSpan(value, Start.Minutes, 0);
    }

    public int StartMinute
    {
        get => Start.Minutes;
        set => Start = new TimeSpan(Start.Hours, value, 0);
    }

    // End helpers
    public int EndHour
    {
        get => End.Hours;
        set => End = new TimeSpan(value, End.Minutes, 0);
    }

    public int EndMinute
    {
        get => End.Minutes;
        set => End = new TimeSpan(End.Hours, value, 0);
    }

    public override string ToString() => $"{Start:hh\\:mm}-{End:hh\\:mm}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

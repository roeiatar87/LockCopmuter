// =========================
// Models/TimeRange.cs
// =========================
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LockComputer.Models;

public class TimeRange : INotifyPropertyChanged
{
    private TimeSpan _start;
    public TimeSpan Start
    {
        get => _start;
        set
        {
            _start = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StartHour));
            OnPropertyChanged(nameof(StartMinute));
        }
    }

    private TimeSpan _end;
    public TimeSpan End
    {
        get => _end;
        set
        {
            _end = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EndHour));
            OnPropertyChanged(nameof(EndMinute));
        }
    }

    public int StartHour
    {
        get => Start.Hours;
        set => Start = new TimeSpan(Clamp(value, 0, 23), Start.Minutes, 0);
    }

    public int StartMinute
    {
        get => Start.Minutes;
        set => Start = new TimeSpan(Start.Hours, Clamp(value, 0, 59), 0);
    }

    public int EndHour
    {
        get => End.Hours;
        set => End = new TimeSpan(Clamp(value, 0, 23), End.Minutes, 0);
    }

    public int EndMinute
    {
        get => End.Minutes;
        set => End = new TimeSpan(End.Hours, Clamp(value, 0, 59), 0);
    }

    private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

    public override string ToString() => $"{Start:hh\\:mm}-{End:hh\\:mm}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

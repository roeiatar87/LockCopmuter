// =========================
// Models/DateRange.cs
// =========================
using System;

namespace LockComputer.Models;

public class DateRange : IEquatable<DateRange>
{
    public DateTime StartDate { get; set; } // רק תאריך (Date)
    public DateTime EndDate { get; set; }   // רק תאריך (Date)

    /// <summary>נרמול: מבטיח ש-StartDate <= EndDate</summary>
    public void Normalize()
    {
        if (EndDate.Date < StartDate.Date)
            (StartDate, EndDate) = (EndDate, StartDate);
    }

    public bool Contains(DateTime dt)
    {
        var d = dt.Date;
        var start = StartDate.Date;
        var end = EndDate.Date;

        if (end < start)
            (start, end) = (end, start);

        return d >= start && d <= end;
    }

    public override string ToString() => $"{StartDate:dd/MM/yyyy} – {EndDate:dd/MM/yyyy}";

    public override bool Equals(object? obj) => Equals(obj as DateRange);

    public bool Equals(DateRange? other)
    {
        if (other is null) return false;

        return StartDate.Date == other.StartDate.Date
            && EndDate.Date == other.EndDate.Date;
    }

    public override int GetHashCode()
        => HashCode.Combine(StartDate.Date, EndDate.Date);
}

using System;

namespace LockCopmuter.Models;

public class DateRange
{
    public DateTime StartDate { get; set; } // רק תאריך (Date)
    public DateTime EndDate { get; set; }   // רק תאריך (Date)

    public bool Contains(DateTime dt)
    {
        var d = dt.Date;
        return d >= StartDate.Date && d <= EndDate.Date;
    }

    public override string ToString() => $"{StartDate:dd/MM/yyyy} – {EndDate:dd/MM/yyyy}";
}

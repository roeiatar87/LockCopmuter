using System.Collections.ObjectModel;

namespace LockCopmuter.Models;

public class DayRule
{
    public DayOfWeek Day { get; set; }
    public bool Enabled { get; set; } = true;
    public ObservableCollection<TimeRange> Ranges { get; set; } = new();

    public string DayHebrew => Day switch
    {
        DayOfWeek.Sunday => "ראשון",
        DayOfWeek.Monday => "שני",
        DayOfWeek.Tuesday => "שלישי",
        DayOfWeek.Wednesday => "רביעי",
        DayOfWeek.Thursday => "חמישי",
        DayOfWeek.Friday => "שישי",
        DayOfWeek.Saturday => "שבת",
        _ => Day.ToString()
    };
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LockCopmuter.Helpers
{
    public static class DayNameHelper
    {
        public static string ToHebrew(DayOfWeek day) => day switch
        {
            DayOfWeek.Sunday => "ראשון",
            DayOfWeek.Monday => "שני",
            DayOfWeek.Tuesday => "שלישי",
            DayOfWeek.Wednesday => "רביעי",
            DayOfWeek.Thursday => "חמישי",
            DayOfWeek.Friday => "שישי",
            DayOfWeek.Saturday => "שבת",
            _ => ""
        };
    }
}

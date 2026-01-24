using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LockCopmuter.Models
{
    public class ScheduleSettings
    {
        public int WarningMinutesBefore { get; set; } = 5;
        public bool IsTestMode { get; set; } = false;
        public ObservableCollection<DateTime> ExceptionDates { get; set; } = new();
        public ObservableCollection<DateRange> ExceptionRanges { get; set; } = new();
        public ObservableCollection<DayRule> Rules { get; set; } = new();
        public string PasswordHashBase64 { get; set; } = "";
        public string PasswordSaltBase64 { get; set; } = "";

        public static ScheduleSettings CreateDefault()
        {
            var s = new ScheduleSettings();

            foreach (DayOfWeek d in Enum.GetValues(typeof(DayOfWeek)))
            {
                var rule = new DayRule { Day = d, Enabled = true };
                rule.Ranges.Add(new TimeRange { Start = new TimeSpan(15, 0, 0), End = new TimeSpan(17, 0, 0) });
                s.Rules.Add(rule);
            }

            return s;
        }

        public bool IsRestrictedNow(DateTime now)
        {
            if (IsExceptionDay(now))
                return false;

            foreach (var rule in Rules)
            {
                if (!rule.Enabled) continue;
                if (rule.Day != now.DayOfWeek) continue;

                var t = now.TimeOfDay;
                foreach (var r in rule.Ranges)
                {
                    if (t >= r.Start && t < r.End)
                        return true;
                }
            }
            return false;
        }
        public bool IsExceptionDay(DateTime now)
        {
            var d = now.Date;

            if (ExceptionDates.Any(x => x.Date == d))
                return true;

            if (ExceptionRanges.Any(r => r.Contains(d)))
                return true;

            return false;
        }


    }
}

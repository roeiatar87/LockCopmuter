// =========================
// Models/ScheduleSettings.cs
// =========================
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace LockComputer.Models
{
    public class ScheduleSettings
    {
        public int WarningMinutesBefore { get; set; } = 5;
        public bool IsTestMode { get; set; } = false;

        public EnforcementMode EnforcementMode { get; set; } = EnforcementMode.LockComputer;

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

            s.Normalize();
            return s;
        }

        /// <summary>
        /// מנרמל נתונים:
        /// - Rule אחד לכל יום (אם יש כפילויות – שומר את האחרון)
        /// - מוסיף ימים חסרים
        /// - מנקה כפילויות בחריגים
        /// - מנרמל DateRange (Start<=End)
        /// </summary>
        public void Normalize()
        {
            // 1) Rule אחד לכל יום
            var normalizedRules = Enum.GetValues(typeof(DayOfWeek))
                .Cast<DayOfWeek>()
                .Select(day =>
                {
                    var last = Rules.LastOrDefault(r => r.Day == day);
                    return last ?? new DayRule { Day = day, Enabled = true };
                })
                .ToList();

            Rules = new ObservableCollection<DayRule>(normalizedRules);

            // 2) ExceptionDates – רק תאריכים ייחודיים
            ExceptionDates = new ObservableCollection<DateTime>(
                ExceptionDates.Select(d => d.Date).Distinct()
            );

            // 3) ExceptionRanges – Normalize + Distinct (עובד בגלל Equals/GetHashCode)
            ExceptionRanges = new ObservableCollection<DateRange>(
                ExceptionRanges
                    .Select(r => { r.Normalize(); return r; })
                    .Distinct()
            );
        }

        public bool IsRestrictedNow(DateTime now)
        {
            if (IsExceptionDay(now))
                return false;

            var rule = Rules.LastOrDefault(r => r.Enabled && r.Day == now.DayOfWeek);
            if (rule == null) return false;

            var t = now.TimeOfDay;

            foreach (var r in rule.Ranges)
            {
                // ✅ תומך גם בטווח שחוצה חצות (למשל 22:00–06:00)
                if (r.End <= r.Start)
                {
                    if (t >= r.Start || t < r.End)
                        return true;
                }
                else
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

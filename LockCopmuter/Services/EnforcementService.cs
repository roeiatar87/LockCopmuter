using LockCopmuter.Models;

namespace LockCopmuter.Services;

public interface IEnforcementService
{
    void Enforce();
}

public interface IWarningUi
{
    DateTime? SuppressedUntil { get; }
    void ShowWarning(DateTime targetTime, int minutesBefore);
    void CloseWarning();
}


public class EnforcementService : IEnforcementService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    private readonly IWarningUi _warningUi;
    private DateTime? _shutdownAt; // מתי לבצע כיבוי (כדי שהספירה לא תתאפס)
    private DateTime? _lastWarningForStart;       // כדי לא לפתוח אזהרה שוב ושוב (לפני התחלה)
    private bool _shutdownIssued;                 // שלא ננסה לכבות שוב ושוב

    private const int ShutdownGraceSeconds = 30;  // כמה זמן להציג אזהרה בתוך חלון חסימה לפני כיבוי

    public EnforcementService(ISettingsService settingsService, ILogger logger, IWarningUi warningUi)
    {
        _settingsService = settingsService;
        _logger = logger;
        _warningUi = warningUi;
    }

    public void Enforce()
    {
        var settings = _settingsService.Load();
        var now = DateTime.Now;

        // אם ההורה ביטל עם סיסמה – לא מבצעים כיבוי עד סוף החלון הנוכחי
        if (_warningUi.SuppressedUntil.HasValue && now < _warningUi.SuppressedUntil.Value)
        {
            // אם אנחנו בתוך חסימה אבל מבוטל – לא מציגים אזהרה ולא מכבים
            if (settings.IsRestrictedNow(now))
            {
                _warningUi.CloseWarning();
                return;
            }
        }


        // 1) בתוך חסימה
        if (settings.IsRestrictedNow(now))
        {
            // קובעים פעם אחת יעד כיבוי (כדי שהספירה לא תתאפס)
            _shutdownAt ??= now.AddSeconds(ShutdownGraceSeconds);

            // מצב בדיקה - לא מכבים, אבל נציג ספירה קבועה עד היעד
            if (settings.IsTestMode)
            {
                _logger.Info("TEST MODE: Restricted time detected – would shutdown after grace (skipped).");
                _warningUi.ShowWarning(_shutdownAt.Value, 0);
                return;
            }

            // להציג אזהרה עד מועד הכיבוי הקבוע
            _warningUi.ShowWarning(_shutdownAt.Value, 0);

            // אם הגיע הזמן – לכבות פעם אחת
            if (!_shutdownIssued && now >= _shutdownAt.Value)
            {
                _shutdownIssued = true;
                _warningUi.CloseWarning();
                _logger.Info("Restricted time detected – shutting down now (after grace).");
                ShutdownNow();
            }

            return;
        }
        else
        {
            // יצאנו מחסימה – מאפסים כדי שבפעם הבאה יהיה יעד חדש
            _shutdownAt = null;
            _shutdownIssued = false;
        }


        // 2) לא בתוך חסימה: לנקות מצב "בתוך חסימה"
        _shutdownIssued = false;

        // 3) אזהרה לפני תחילת חסימה
        var next = FindNextRestrictionStart(settings, now, daysForward: 2);
        if (next == null)
        {
            _warningUi.CloseWarning();
            return;
        }

        var start = next.Value;
        var warnMinutes = Math.Max(1, settings.WarningMinutesBefore);

        var warnFrom = start.AddMinutes(-warnMinutes);
        var warnTo = start;

        if (now >= warnFrom && now < warnTo)
        {
            // רק לוג פעם אחת לכל חלון
            if (_lastWarningForStart != start)
            {
                _lastWarningForStart = start;
                _logger.Info($"Warning window opened. Restriction starts at {start:yyyy-MM-dd HH:mm}.");
            }

            // כאן targetTime הוא תחילת חסימה עתידית
            _warningUi.ShowWarning(start, warnMinutes);
        }
        else
        {
            _warningUi.CloseWarning();
        }
    }

    private static DateTime? FindNextRestrictionStart(ScheduleSettings settings, DateTime now, int daysForward)
    {
        for (int d = 0; d <= daysForward; d++)
        {
            var date = now.Date.AddDays(d);
            if (settings.IsExceptionDay(date))
                continue;

            var rule = settings.Rules.FirstOrDefault(r => r.Enabled && r.Day == date.DayOfWeek);
            if (rule == null) continue;

            foreach (var r in rule.Ranges.OrderBy(x => x.Start))
            {
                var start = date + r.Start;
                var end = date + r.End;

                if (d == 0 && end <= now) continue;

                if (start <= now && now < end) return start;
                if (start > now) return start;
            }
        }

        return null;
    }

    private void ShutdownNow()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "shutdown",
            Arguments = "/s /t 0",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }
}

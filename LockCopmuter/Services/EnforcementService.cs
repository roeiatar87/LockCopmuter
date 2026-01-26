using System;
using System.Linq;
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

    private DateTime? _shutdownAt;             // יעד כיבוי קבוע (כדי שהספירה לא תתאפס)
    private DateTime? _lastWarningForStart;    // כדי לא לפתוח לוג/אזהרה שוב ושוב לפני התחלה
    private bool _shutdownIssued;              // שלא ננסה לכבות שוב ושוב

    private const int ShutdownGraceSeconds = 30;

    public EnforcementService(ISettingsService settingsService, ILogger logger, IWarningUi warningUi)
    {
        _settingsService = settingsService;
        _logger = logger;
        _warningUi = warningUi;
    }

    public void Enforce()
    {
        var settings = _settingsService.Load();
        settings.Normalize(); // ✅ מגן מכפילויות Rules (כמו יום ראשון) + Normalize חריגים

        var now = DateTime.Now;

        // אם ההורה ביטל עם סיסמה – לא מבצעים אזהרה/כיבוי בתוך חסימה עד הזמן שנקבע
        if (_warningUi.SuppressedUntil.HasValue && now < _warningUi.SuppressedUntil.Value)
        {
            if (settings.IsRestrictedNow(now))
            {
                // בתוך חסימה אבל "מוחרג זמנית" – לא אזהרה ולא כיבוי
                _warningUi.CloseWarning();

                // חשוב: לא לקבע shutdownAt בזמן suppression כדי שלא "יתפוס" לנו יעד ישן
                _shutdownAt = null;
                _shutdownIssued = false;

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
                _warningUi.ShowWarning(_shutdownAt.Value, minutesBefore: 0);
                return;
            }

            // להציג אזהרה עד מועד הכיבוי הקבוע
            _warningUi.ShowWarning(_shutdownAt.Value, minutesBefore: 0);

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

        // יצאנו מחסימה – מאפסים כדי שבפעם הבאה יהיה יעד חדש
        _shutdownAt = null;
        _shutdownIssued = false;

        // 2) אזהרה לפני תחילת חסימה
        var nextStart = FindNextRestrictionStart(settings, now, daysForward: 2);
        if (nextStart == null)
        {
            _warningUi.CloseWarning();
            _lastWarningForStart = null;
            return;
        }

        var start = nextStart.Value;
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

            // כאן targetTime הוא תחילת חסימה עתידית (ספירה עד start)
            _warningUi.ShowWarning(start, warnMinutes);
        }
        else
        {
            _warningUi.CloseWarning();

            // אם אנחנו מחוץ לחלון האזהרה, אין צורך “לנעול” את ה-start הקודם
            // כדי שאם תתקרב שוב לטווח הבא (או ערכו זמנים), נקבל לוג תקין.
            if (now < warnFrom || now >= warnTo)
                _lastWarningForStart = null;
        }
    }

    private static DateTime? FindNextRestrictionStart(ScheduleSettings settings, DateTime now, int daysForward)
    {
        for (int d = 0; d <= daysForward; d++)
        {
            var date = now.Date.AddDays(d);

            if (settings.IsExceptionDay(date))
                continue;

            // ✅ אחרי Normalize אמור להיות Rule אחד לכל יום, ועדיין בטוח לבחור "האחרון"
            var rule = settings.Rules
                .Where(r => r.Enabled && r.Day == date.DayOfWeek)
                .LastOrDefault();

            if (rule == null) continue;

            foreach (var r in rule.Ranges.OrderBy(x => x.Start))
            {
                var start = date + r.Start;
                var end = date + r.End;

                // ✅ חציית חצות (למשל 22:00–06:00)
                if (r.End <= r.Start)
                    end = date.AddDays(1) + r.End;

                // אם זה היום והחלון כבר נגמר – הבא
                if (d == 0 && end <= now)
                    continue;

                // אם אנחנו כבר בתוך חלון – ה"start" של החלון הנוכחי
                if (start <= now && now < end)
                    return start;

                // חלון עתידי
                if (start > now)
                    return start;
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

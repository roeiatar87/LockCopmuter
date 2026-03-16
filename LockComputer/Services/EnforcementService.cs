using System;
using System.Linq;
using System.Runtime.InteropServices;
using LockComputer.Models;

namespace LockComputer.Services;

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

    private DateTime? _lockAt;
    private DateTime? _lastWarningForStart;

    private bool _initialLockIssued;
    private bool _isInRelockCountdown;
    private DateTime? _relockAt;

    private const int LockGraceSeconds = 30;
    private const int RelockGraceSeconds = 10;

    public EnforcementService(ISettingsService settingsService, ILogger logger, IWarningUi warningUi)
    {
        _settingsService = settingsService;
        _logger = logger;
        _warningUi = warningUi;
    }

    public void Enforce()
    {
        var settings = _settingsService.Load();
        settings.Normalize();

        var now = DateTime.Now;

        if (_warningUi.SuppressedUntil.HasValue && now < _warningUi.SuppressedUntil.Value)
        {
            if (settings.IsRestrictedNow(now))
            {
                _warningUi.CloseWarning();

                _lockAt = null;
                _relockAt = null;
                _initialLockIssued = false;
                _isInRelockCountdown = false;

                return;
            }
        }

        if (settings.IsRestrictedNow(now))
        {
            if (settings.IsTestMode)
            {
                _lockAt ??= now.AddSeconds(LockGraceSeconds);
                _logger.Info("TEST MODE: Restricted time detected - would lock after grace (skipped).");
                _warningUi.ShowWarning(_lockAt.Value, minutesBefore: 0);
                return;
            }

            // נעילה ראשונה
            if (!_initialLockIssued)
            {
                _lockAt ??= now.AddSeconds(LockGraceSeconds);
                _warningUi.ShowWarning(_lockAt.Value, minutesBefore: 0);

                if (now >= _lockAt.Value)
                {
                    _initialLockIssued = true;
                    _lockAt = null;
                    _warningUi.CloseWarning();
                    _logger.Info("Restricted time detected - locking now (after initial grace).");
                    LockNow();
                }

                return;
            }

            // אם כבר הייתה נעילה ראשונה, מתחילים בכל פעם מחדש ספירת relock
            if (!_isInRelockCountdown)
            {
                _relockAt = now.AddSeconds(RelockGraceSeconds);
                _isInRelockCountdown = true;
                _logger.Info($"Restricted time still active - relock warning opened. Relocking at {_relockAt:yyyy-MM-dd HH:mm:ss}.");
            }

            _warningUi.ShowWarning(_relockAt!.Value, minutesBefore: 0);

            if (now >= _relockAt.Value)
            {
                _warningUi.CloseWarning();
                _isInRelockCountdown = false;
                _relockAt = null;

                _logger.Info("Restricted time still active - locking again after relock warning.");
                LockNow();
            }

            return;
        }

        _lockAt = null;
        _relockAt = null;
        _initialLockIssued = false;
        _isInRelockCountdown = false;

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
            if (_lastWarningForStart != start)
            {
                _lastWarningForStart = start;
                _logger.Info($"Warning window opened. Restriction starts at {start:yyyy-MM-dd HH:mm}.");
            }

            _warningUi.ShowWarning(start, warnMinutes);
        }
        else
        {
            _warningUi.CloseWarning();

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

            var rule = settings.Rules
                .Where(r => r.Enabled && r.Day == date.DayOfWeek)
                .LastOrDefault();

            if (rule == null) continue;

            foreach (var r in rule.Ranges.OrderBy(x => x.Start))
            {
                var start = date + r.Start;
                var end = date + r.End;

                if (r.End <= r.Start)
                    end = date.AddDays(1) + r.End;

                if (d == 0 && end <= now)
                    continue;

                if (start <= now && now < end)
                    return start;

                if (start > now)
                    return start;
            }
        }

        return null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    private void LockNow()
    {
        LockWorkStation();
    }
}
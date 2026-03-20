using System;
using System.Diagnostics;
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
    event EventHandler? CountdownFinished;
    void ShowWarning(DateTime targetTime, int minutesBefore);
    void CloseWarning();
}

public class EnforcementService : IEnforcementService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    private readonly IWarningUi _warningUi;

    private DateTime? _enforceAt;
    private DateTime? _lastWarningForStart;
    private bool _initialEnforcementIssued;

    private const int LockGraceSeconds = 30;
    private const int RobloxGraceSeconds = 10;

    public EnforcementService(ISettingsService settingsService, ILogger logger, IWarningUi warningUi)
    {
        _settingsService = settingsService;
        _logger = logger;
        _warningUi = warningUi;

        _warningUi.CountdownFinished += OnWarningCountdownFinished;
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
                ResetEnforcementState();
                return;
            }
        }

        if (settings.IsRestrictedNow(now))
        {
            switch (settings.EnforcementMode)
            {
                case EnforcementMode.CloseRoblox:
                    EnforceCloseRoblox(settings, now);
                    return;

                case EnforcementMode.LockComputer:
                default:
                    EnforceLockComputer(settings, now);
                    return;
            }
        }

        ResetEnforcementState();

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

    private void OnWarningCountdownFinished(object? sender, EventArgs e)
    {
        try
        {
            var settings = _settingsService.Load();
            settings.Normalize();

            var now = DateTime.Now;

            if (!settings.IsRestrictedNow(now))
            {
                _logger.Info("Warning countdown finished, but restriction is no longer active.");
                ResetEnforcementState();
                _warningUi.CloseWarning();
                return;
            }

            if (_warningUi.SuppressedUntil.HasValue && now < _warningUi.SuppressedUntil.Value)
            {
                _logger.Info("Warning countdown finished, but enforcement is currently suppressed.");
                ResetEnforcementState();
                _warningUi.CloseWarning();
                return;
            }

            switch (settings.EnforcementMode)
            {
                case EnforcementMode.CloseRoblox:
                    if (!IsRobloxRunning())
                    {
                        _logger.Info("Warning countdown finished, but Roblox is no longer running.");
                        ResetEnforcementState();
                        _warningUi.CloseWarning();
                        return;
                    }

                    _logger.Info("Warning countdown finished - closing Roblox immediately.");
                    _warningUi.CloseWarning();
                    _enforceAt = null;
                    _initialEnforcementIssued = false;
                    CloseRobloxNow();
                    break;

                case EnforcementMode.LockComputer:
                default:
                    _logger.Info("Warning countdown finished - locking computer immediately.");
                    _warningUi.CloseWarning();
                    _enforceAt = null;
                    _initialEnforcementIssued = true;
                    LockNow();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Immediate enforcement after warning countdown failed.", ex);
        }
    }

    private void EnforceLockComputer(ScheduleSettings settings, DateTime now)
    {
        if (settings.IsTestMode)
        {
            _enforceAt ??= now.AddSeconds(LockGraceSeconds);
            _logger.Info("TEST MODE: Restricted time detected - would enforce after grace (skipped).");
            _warningUi.ShowWarning(_enforceAt.Value, minutesBefore: 0);
            return;
        }

        if (!_initialEnforcementIssued)
        {
            _enforceAt ??= now.AddSeconds(LockGraceSeconds);
            _warningUi.ShowWarning(_enforceAt.Value, minutesBefore: 0);

            if (now >= _enforceAt.Value)
            {
                _initialEnforcementIssued = true;
                _enforceAt = null;
                _warningUi.CloseWarning();
                _logger.Info("Restricted time detected - locking computer now.");
                LockNow();
            }

            return;
        }

        _enforceAt ??= now.AddSeconds(LockGraceSeconds);
        _warningUi.ShowWarning(_enforceAt.Value, minutesBefore: 0);

        if (now >= _enforceAt.Value)
        {
            _enforceAt = null;
            _warningUi.CloseWarning();
            _logger.Info("Restricted time still active - locking computer again.");
            LockNow();
        }
    }

    private void EnforceCloseRoblox(ScheduleSettings settings, DateTime now)
    {
        if (!IsRobloxRunning())
        {
            _warningUi.CloseWarning();
            _enforceAt = null;
            _initialEnforcementIssued = false;
            return;
        }

        if (settings.IsTestMode)
        {
            _enforceAt ??= now.AddSeconds(RobloxGraceSeconds);
            _logger.Info("TEST MODE: Roblox detected during restriction - would close after grace (skipped).");
            _warningUi.ShowWarning(_enforceAt.Value, minutesBefore: 0);
            return;
        }

        _enforceAt ??= now.AddSeconds(RobloxGraceSeconds);
        _warningUi.ShowWarning(_enforceAt.Value, minutesBefore: 0);

        if (now >= _enforceAt.Value)
        {
            _warningUi.CloseWarning();
            _enforceAt = null;
            _initialEnforcementIssued = false;

            _logger.Info("Roblox detected during restriction - closing now.");
            CloseRobloxNow();
        }
    }

    private void ResetEnforcementState()
    {
        _enforceAt = null;
        _initialEnforcementIssued = false;
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

    private static bool IsRobloxRunning()
    {
        var processNames = new[]
        {
            "RobloxPlayerBeta",
            "RobloxGameClient"
        };

        return processNames.Any(name => Process.GetProcessesByName(name).Any());
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    private void LockNow()
    {
        LockWorkStation();
    }

    private void CloseRobloxNow()
    {
        try
        {
            var processNames = new[]
            {
                "RobloxPlayerBeta",
                "RobloxGameClient"
            };

            var anyFound = false;

            foreach (var name in processNames)
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    anyFound = true;

                    try
                    {
                        if (process.HasExited)
                            continue;

                        process.CloseMainWindow();

                        if (!process.WaitForExit(500) && !process.HasExited)
                            process.Kill(true);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Failed to close Roblox process '{name}'.", ex);
                    }
                }
            }

            if (anyFound)
                _logger.Info("Roblox processes were closed due to active restriction.");
            else
                _logger.Info("Restriction active, but no Roblox process was found.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to close Roblox.", ex);
        }
    }
}
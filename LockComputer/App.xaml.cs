using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Win32;
using Application = System.Windows.Application;
using LockComputer.Models;
using LockComputer.Services;
using LockComputer.ViewModels;
using LockComputer.Views;
using MessageBox = System.Windows.MessageBox;

namespace LockComputer;

public partial class App : Application
{
    private NotifyIcon? _tray;
    private MainWindow? _mainWindow;
    private Mutex? _mutex;

    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private readonly IPasswordService _passwordService;

    private ScheduleSettings? _settings;
    private SettingsWindow? _settingsWindow;

    private DispatcherTimer? _enforcementTimer;
    private readonly IEnforcementService _enforcementService;

    public App()
    {
        _logger = new FileLogger();
        _passwordService = new PasswordService();
        _settingsService = new SettingsService(_logger);

        var warningUi = new WarningUi(_settingsService, _passwordService);
        _enforcementService = new EnforcementService(_settingsService, _logger, warningUi);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, @"Global\LockComputer.App", out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        ClearExitFlagIfExists();

        _settings = _settingsService.Load();
        _logger.Info("App started (tray mode).");

        SetupTray();
        StartEnforcementTimer();

        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    private void ClearExitFlagIfExists()
    {
        try
        {
            var sharedDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "LockComputer");

            var flagPath = Path.Combine(sharedDir, "exit.flag");

            if (File.Exists(flagPath))
            {
                File.Delete(flagPath);
                _logger.Info("Cleared stale exit.flag on startup: " + flagPath);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to clear exit.flag on startup", ex);
        }
    }

    private void SetupTray()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");

        _tray = new NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconPath),
            Visible = true,
            Text = "LockComputer"
        };

        var menu = new ContextMenuStrip();

        menu.Items.Add("פתח חלון", null, (_, __) =>
        {
            Dispatcher.Invoke(ShowMainWindow);
        });

        _tray.DoubleClick += (_, __) =>
        {
            Dispatcher.Invoke(ShowMainWindow);
        };

        menu.Items.Add("הגדרות", null, (_, __) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (!EnsureUnlocked()) return;

                if (_settingsWindow == null)
                {
                    var vm = new SettingsViewModel(_settingsService, _logger, _passwordService);
                    _settingsWindow = new SettingsWindow(vm);
                    _settingsWindow.Closed += (_, __) => _settingsWindow = null;
                    _settingsWindow.Show();
                }
                else
                {
                    if (!_settingsWindow.IsVisible) _settingsWindow.Show();
                    _settingsWindow.Activate();
                }
            });
        });

        menu.Items.Add("יציאה", null, (_, __) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (!EnsureUnlocked()) return;

                try
                {
                    var sharedDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "LockComputer");

                    Directory.CreateDirectory(sharedDir);

                    var flagPath = Path.Combine(sharedDir, "exit.flag");
                    File.WriteAllText(
                        flagPath,
                        DateTime.Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

                    _logger.Info("Wrote exit.flag to: " + flagPath);
                }
                catch (Exception ex)
                {
                    _logger.Error("Failed to write exit.flag", ex);
                }

                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                    _tray = null;
                }

                Shutdown();
            });
        });

        _tray.ContextMenuStrip = menu;
    }

    private void ShowMainWindow()
    {
        _logger.Info("ShowMainWindow invoked.");

        if (_mainWindow == null)
        {
            _logger.Info("Creating MainWindow + MainViewModel.");
            var vm = new MainViewModel(_settingsService, _passwordService, _logger);
            _mainWindow = new MainWindow(vm);
            _mainWindow.Closed += (_, __) => _mainWindow = null;

            _mainWindow.Show();

            vm.Refresh();
            _logger.Info("MainViewModel.Refresh invoked (new window).");
        }
        else
        {
            _logger.Info("MainWindow already exists. Showing/Activating.");

            if (!_mainWindow.IsVisible) _mainWindow.Show();
            _mainWindow.Activate();

            if (_mainWindow.DataContext is MainViewModel vm)
            {
                vm.Refresh();
                _logger.Info("MainViewModel.Refresh invoked (existing window).");
            }
            else
            {
                _logger.Info("MainWindow.DataContext is not MainViewModel (unexpected).");
            }
        }
    }

    private bool EnsureUnlocked()
    {
        var settings = _settingsService.Load();

        if (!_passwordService.HasPassword(settings))
            return true;

        var dlg = new PasswordPromptWindow();
        if (dlg.ShowDialog() != true)
            return false;

        var ok = _passwordService.Verify(settings, dlg.Password);
        if (!ok)
        {
            _logger.Info("Password prompt failed.");
            MessageBox.Show("סיסמה שגויה.", "נעול",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        return ok;
    }

    private void StartEnforcementTimer()
    {
        _enforcementTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };

        _enforcementTimer.Tick += (_, __) =>
        {
            try
            {
                _enforcementService.Enforce();
            }
            catch (Exception ex)
            {
                _logger.Error("Enforcement error", ex);
            }
        };

        _enforcementTimer.Start();

        _enforcementService.Enforce();
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    _logger.Info("Session unlocked - enforcing immediately.");
                    _enforcementService.Enforce();
                }
                catch (Exception ex)
                {
                    _logger.Error("Immediate enforcement after unlock failed", ex);
                }
            });
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;

        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        try
        {
            _mutex?.ReleaseMutex();
        }
        catch
        {
        }

        _mutex?.Dispose();
        _mutex = null;

        base.OnExit(e);
    }
}
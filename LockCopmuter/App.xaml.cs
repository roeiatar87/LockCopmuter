using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using LockCopmuter.Models;
using LockCopmuter.Services;
using LockCopmuter.ViewModels;
using LockCopmuter.Views;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using System.IO;
namespace LockCopmuter;

public partial class App : Application
{
    private NotifyIcon? _tray;
    private MainWindow? _mainWindow;
    private Mutex? _mutex;

    private readonly ISettingsService _settingsService = new SettingsService();
    private readonly ILogger _logger = new FileLogger();
    private readonly IPasswordService _passwordService = new PasswordService();

    private ScheduleSettings? _settings;
    private SettingsWindow? _settingsWindow;

    private DispatcherTimer? _enforcementTimer;
    private readonly IEnforcementService _enforcementService;

    public App()
    {
        var warningUi = new WarningUi(_settingsService, _passwordService);
        _enforcementService = new EnforcementService(_settingsService, _logger, warningUi);
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        // ✅ 1) Singleton: לא לאפשר 2 מופעים
        _mutex = new Mutex(true, @"Global\LockCopmuter.App", out bool createdNew);
        if (!createdNew)
        {
            // כבר רץ מופע אחר
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _settings = _settingsService.Load();
        _logger.Info("App started (tray mode).");

        SetupTray();
        StartEnforcementTimer();
    }


    private void SetupTray()
    {
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "app.ico");

        _tray = new NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconPath),
            Visible = true,
            Text = "LockCopmuter"
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

                // ✅ 1) סימון סגירה חוקית עבור ה-Watchdog
                try
                {
                    var sharedDir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "LockCopmuter");

                    System.IO.Directory.CreateDirectory(sharedDir);

                    var flagPath = System.IO.Path.Combine(sharedDir, "exit.flag");
                    System.IO.File.WriteAllText(
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
        if (_mainWindow == null)
        {
            var vm = new MainViewModel(_settingsService, _passwordService, _logger);
            _mainWindow = new MainWindow(vm);
            _mainWindow.Closed += (_, __) => _mainWindow = null;
            _mainWindow.Show();
        }
        else
        {
            if (!_mainWindow.IsVisible) _mainWindow.Show();
            _mainWindow.Activate();
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


    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        try
        {
            _mutex?.ReleaseMutex();
        }
        catch { /* ignore */ }

        _mutex?.Dispose();
        _mutex = null;

        base.OnExit(e);
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

        // בדיקה מיידית בהפעלה
        _enforcementService.Enforce();
    }

}

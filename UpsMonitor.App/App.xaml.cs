using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using UpsMonitor.Core;
using UpsMonitor.Hid;
using UpsMonitor.Infrastructure;

namespace UpsMonitor.App;

public partial class App : Application
{
    internal const string ShowWindowMessageName = "UpsMonitor_PowerGuard_ShowMainWindow";
    private const string MutexName = @"Local\UpsMonitor_PowerGuard_SingleInstance";
    private static Mutex? _singleInstanceMutex;

    private MainViewModel? _viewModel;
    private FileUpsEventSink? _eventSink;
    private SqliteTelemetryStore? _historyStore;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private static readonly IntPtr HwndBroadcast = new(0xffff);

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                File.AppendAllText("error.log", $"[{DateTime.Now}] Unhandled: {args.Exception}\n");
            }
            catch { }
        };

        bool createdNew;
        try
        {
            _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);
        }
        catch (AbandonedMutexException)
        {
            createdNew = true;
        }

        if (!createdNew)
        {
            var messageId = RegisterWindowMessage(ShowWindowMessageName);
            if (messageId != 0)
            {
                PostMessage(HwndBroadcast, messageId, IntPtr.Zero, IntPtr.Zero);
            }

            Shutdown();
            return;
        }

        var paths = new AppPaths();
        var configurationStore = new JsonConfigurationStore(paths);
        AppConfiguration configuration;
        Exception? configurationError = null;
        try
        {
            configuration = await configurationStore.LoadAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            configuration = new AppConfiguration();
            configurationError = exception;
        }

        ThemeManager.ApplyTheme(this, configuration.Ui.Theme);
        LocalizationManager.ApplyLanguage(this, configuration.Ui.Language);

        _eventSink = new FileUpsEventSink(paths);
        _historyStore = new SqliteTelemetryStore(paths, configuration.History);
        var historyInitTask = _historyStore.InitializeAsync();

        IUpsEventSink eventSink = new CompositeUpsEventSink(_eventSink, _historyStore);
        var engine = new UpsMonitorEngine(
            new WindowsHidUpsProvider(),
            eventSink,
            configuration.Monitoring.PollIntervalMs,
            TimeSpan.FromSeconds(configuration.Monitoring.RuntimeLowSeconds),
            _historyStore,
            alertThresholds: configuration.Alerts.ToAlertThresholds());

        _viewModel = new MainViewModel(engine, configurationStore, configuration, paths, _historyStore);
        if (configurationError is not null)
        {
            _viewModel.SetStartupError(LocalizationManager.Format("ConfigurationLoadErrorFormat", configurationError.Message));
        }

        _ = historyInitTask.ContinueWith(task =>
        {
            if (task.IsFaulted && task.Exception is { } ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                _viewModel.SetStartupError(LocalizationManager.Format("HistoryStorageErrorFormat", msg));
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());

        var window = new MainWindow { DataContext = _viewModel };
        MainWindow = window;

        var startMinimized = e.Args.Any(arg => arg.Equals("--tray", StringComparison.OrdinalIgnoreCase)
                                            || arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
                             || (configuration.Ui.StartMinimized && configuration.Ui.MinimizeToTray);

        if (startMinimized)
        {
            // Ensure HWND is created so HwndSource and TrayIcon are initialized
            _ = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        }
        else
        {
            window.Show();
        }

        _viewModel.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            var cleanupTask = Task.Run(async () =>
            {
                if (_viewModel is not null)
                {
                    await _viewModel.DisposeAsync().ConfigureAwait(false);
                }

                if (_historyStore is not null)
                {
                    await _historyStore.DisposeAsync().ConfigureAwait(false);
                }
            });

            cleanupTask.Wait(TimeSpan.FromSeconds(1.5));
        }
        catch
        {
        }
        finally
        {
            if (_singleInstanceMutex is not null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch
                {
                }

                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            _eventSink?.Dispose();
            base.OnExit(e);
        }
    }
}

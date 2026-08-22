using System.IO;
using System.Windows;
using UpsMonitor.Core;
using UpsMonitor.Hid;
using UpsMonitor.Infrastructure;

namespace UpsMonitor.App;

public partial class App : Application
{
    private MainViewModel? _viewModel;
    private FileUpsEventSink? _eventSink;
    private SqliteTelemetryStore? _historyStore;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.ApplySystemTheme(this);

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

        LocalizationManager.ApplyLanguage(this, configuration.Ui.Language);

        _eventSink = new FileUpsEventSink(paths);
        Exception? historyError = null;
        try
        {
            _historyStore = new SqliteTelemetryStore(paths, configuration.History);
            await _historyStore.InitializeAsync();
        }
        catch (Exception exception)
        {
            historyError = exception;
            if (_historyStore is not null)
            {
                await _historyStore.DisposeAsync();
                _historyStore = null;
            }
        }

        IUpsEventSink eventSink = _historyStore is null
            ? _eventSink
            : new CompositeUpsEventSink(_eventSink, _historyStore);
        var engine = new UpsMonitorEngine(
            new WindowsHidUpsProvider(),
            eventSink,
            configuration.Monitoring.PollIntervalMs,
            TimeSpan.FromSeconds(configuration.Monitoring.RuntimeLowSeconds),
            _historyStore);

        _viewModel = new MainViewModel(engine, configurationStore, configuration, paths, _historyStore);
        if (configurationError is not null)
        {
            _viewModel.SetStartupError(LocalizationManager.Format("ConfigurationLoadErrorFormat", configurationError.Message));
        }

        if (historyError is not null)
        {
            _viewModel.SetStartupError(LocalizationManager.Format("HistoryStorageErrorFormat", historyError.Message));
        }

        var window = new MainWindow { DataContext = _viewModel };
        MainWindow = window;
        window.Show();
        _viewModel.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _historyStore?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _eventSink?.Dispose();
        base.OnExit(e);
    }
}

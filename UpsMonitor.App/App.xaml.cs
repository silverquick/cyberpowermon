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
        var engine = new UpsMonitorEngine(
            new WindowsHidUpsProvider(),
            _eventSink,
            configuration.Monitoring.PollIntervalMs,
            TimeSpan.FromSeconds(configuration.Monitoring.RuntimeLowSeconds));

        _viewModel = new MainViewModel(engine, configurationStore, configuration, paths);
        if (configurationError is not null)
        {
            _viewModel.SetStartupError(LocalizationManager.Format("ConfigurationLoadErrorFormat", configurationError.Message));
        }

        var window = new MainWindow { DataContext = _viewModel };
        MainWindow = window;
        window.Show();
        _viewModel.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _eventSink?.Dispose();
        base.OnExit(e);
    }
}

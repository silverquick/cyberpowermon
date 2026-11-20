using System.Text.RegularExpressions;
using UpsMonitor.App;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

internal static class SettingsHealthLifecycleTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static SettingsEffects Effects => new(_ => { }, _ => { }, (_, _) => { }, _ => { }, _ => { }, _ => { }, () => { }, () => { });
    private static UpsSnapshot Snapshot => new() { Device = HistoryViewModelTests.Device, Timestamp = DateTimeOffset.Now,
        IsConnected = true, BatteryPercent = 100, PercentLoad = 40, RuntimeRemaining = TimeSpan.FromMinutes(20) };

    public static void DisposalDuringSave() => DisposalDuringSaveAsync().GetAwaiter().GetResult();
    private static async Task DisposalDuringSaveAsync()
    {
        var save = new TaskCompletionSource();
        var config = new AppConfiguration();
        var callbacks = 0;
        using var battery = new BatteryHealthViewModel(config, _ => save.Task, null,
            action => { callbacks++; action(); return Task.CompletedTask; }, _ => callbacks++, _ => callbacks++, () => callbacks++);
        battery.SetSnapshot(Snapshot, UpsTelemetryValidator.Normalize(Snapshot));
        battery.OfficialRecordPercent = 80; battery.OfficialRecordTime = "";
        var task = battery.RecordOfficialHealthAsync();
        battery.PropertyChanged += (_, _) => callbacks++;
        battery.Dispose(); save.SetResult(); await task;
        Check(callbacks == 0, "record completion after dispose does not update UI or reapply snapshot");
        save = new();
        using var settings = new SettingsViewModel(config, _ => save.Task,
            new(_ => callbacks++, _ => callbacks++, (_, _) => callbacks++, _ => callbacks++, _ => callbacks++, _ => callbacks++, () => callbacks++, () => callbacks++),
            battery, _ => callbacks++, "en-US", _ => Task.FromResult(true));
        task = settings.SaveSettingsAsync(); settings.PropertyChanged += (_, _) => callbacks++;
        settings.Dispose(); save.SetResult(); await task;
        Check(callbacks == 0, "settings completion after dispose does not update engine or UI");
    }

    public static void NotificationWebhookAndDisposal() => NotificationAsync().GetAwaiter().GetResult();
    private static async Task NotificationAsync()
    {
        var config = new AppConfiguration();
        using var battery = new BatteryHealthViewModel(config, _ => Task.CompletedTask, null, action => { action(); return Task.CompletedTask; }, _ => { }, _ => { }, () => { });
        var response = new TaskCompletionSource<bool>();
        using var settings = new SettingsViewModel(config, _ => Task.CompletedTask, Effects, battery, _ => { }, "en-US", _ => response.Task);
        var notifications = new List<UpsEventSeverity>();
        settings.TestNotificationRequested += notifications.Add;
        settings.TestNotificationCommand.Execute(null);
        Check(notifications.SequenceEqual(new[] { UpsEventSeverity.Warning }), "notification test uses Warning");
        settings.WebhookUrl = ""; settings.TestWebhookCommand.Execute(null);
        Check(settings.WebhookStatus == LocalizationManager.Get("WebhookUrlRequired"), "empty webhook validation");
        settings.WebhookUrl = "https://synthetic.invalid/";
        settings.TestWebhookCommand.Execute(null);
        Check(settings.WebhookStatus == LocalizationManager.Get("WebhookTesting"), "pending webhook status");
        response.SetResult(true);
        await Until(() => settings.TestWebhookCommand.CanExecute(null));
        Check(settings.WebhookStatus == LocalizationManager.Get("WebhookTestSuccess"), "webhook result");
        response = new(); settings.TestWebhookCommand.Execute(null);
        var changed = 0; settings.PropertyChanged += (_, _) => changed++;
        settings.Dispose(); response.SetResult(false);
        await Until(() => settings.TestWebhookCommand.CanExecute(null));
        settings.TestNotificationCommand.Execute(null);
        Check(changed == 0 && notifications.Count == 1, "disposed settings ignores callbacks and notification test");
    }

    public static void RecordsContextModelsAndValidation() => RecordsAsync().GetAwaiter().GetResult();
    private static async Task RecordsAsync()
    {
        var config = new AppConfiguration(); var statuses = new List<string>(); var errors = new List<Exception>();
        var saves = 0;
        using var vm = new BatteryHealthViewModel(config, _ => { saves++; return Task.CompletedTask; }, null,
            action => { action(); return Task.CompletedTask; }, statuses.Add, errors.Add, () => { });
        vm.SetSnapshot(Snapshot, UpsTelemetryValidator.Normalize(Snapshot));
        vm.OfficialRecordPercent = double.NaN; await vm.RecordOfficialHealthAsync();
        Check(saves == 0 && statuses[^1] == LocalizationManager.Get("OfficialRecordValueInvalid"), "nonfinite official percent rejected");
        vm.OfficialRecordPercent = 80; vm.OfficialRecordDate = DateTime.Today.AddDays(2); vm.OfficialRecordTime = "12:00";
        await vm.RecordOfficialHealthAsync(); Check(saves == 0, "future record rejected");
        vm.OfficialRecordDate = DateTime.Today.AddDays(-10); vm.OfficialRecordTime = "";
        await vm.RecordOfficialHealthAsync();
        var profile = config.BatteryHealth.Profiles.Single();
        Check(profile.OfficialHealthRecords.Single().RecordedAt.LocalDateTime.TimeOfDay == TimeSpan.Zero, "past date resolves midnight");
        profile.OfficialHealthRecords.Clear();
        for (var i = 0; i < 8; i++) profile.OfficialHealthRecords.Add(new()
        { RecordedAt = DateTimeOffset.Now.AddDays(-20 + i * 2), HealthPercent = 95 - i, Source = VendorBatteryHealthCategory.Good, ContextBackfillAttempted = true });
        vm.RefreshLanguage();
        Check(vm.OfficialRecords.Count == 8 && vm.OfficialModelRows.Count == 3, "record list and all three model rows");
        Check(vm.OfficialRecordCountText == LocalizationManager.Format("OfficialRecordCountFormat", 8, 8), "24 hour group count for distinct days");
        var dates = vm.OfficialRecords.Select(row => row.DateText).ToArray();
        var language = LocalizationManager.Language;
        try
        {
            LocalizationManager.Language = "ja-JP"; vm.RefreshLanguage();
            Check(vm.OfficialRecords.Select(row => row.DateText).SequenceEqual(dates) && vm.OfficialModelRows.Count == 3, "language refresh keeps records and models");
        }
        finally { LocalizationManager.Language = language; }
        vm.SelectedBaselineKind = BatteryRuntimeBaselineKind.KnownHealthAnchor; vm.KnownHealthPercent = 0;
        var before = saves; await vm.RecordRuntimeBaselineAsync(); Check(saves == before, "invalid baseline anchor rejected");
        vm.KnownHealthPercent = 70; vm.KnownHealthSource = " synthetic "; await vm.RecordRuntimeBaselineAsync();
        Check(profile.AnchorHealthPercent == 70 && profile.AnchorSource == "synthetic", "known baseline anchor");
        var callbacks = statuses.Count;
        vm.Dispose(); await vm.RecordOfficialHealthAsync(); await vm.SaveBatteryInstalledDateAsync(DateTime.Today); await vm.BackfillOfficialContextsAsync();
        Check(statuses.Count == callbacks && errors.Count == 0, "disposed battery rejects operations");
    }

    public static void ContextBackfillAndFailedRecordSave() => HistoryViewModelTests.WithStore(async store =>
    {
        var snapshot = Snapshot with { Timestamp = DateTimeOffset.Now.AddMinutes(-10), ActivePower = 200, InputVoltage = 100 };
        await store.WriteAsync(snapshot, default); await store.FlushAsync();
        var config = new AppConfiguration();
        var profile = new BatteryHealthProfile { DeviceId = UpsDeviceIdentity.Create(snapshot.Device!) };
        config.BatteryHealth.Profiles.Add(profile);
        var record = new OfficialHealthRecord { RecordedAt = snapshot.Timestamp, HealthPercent = 75 };
        profile.OfficialHealthRecords.Add(record);
        var saves = 0;
        using var vm = new BatteryHealthViewModel(config, _ => { saves++; return Task.CompletedTask; }, store,
            action => { action(); return Task.CompletedTask; }, _ => { }, e => throw e, () => { });
        vm.SetSnapshot(snapshot, UpsTelemetryValidator.Normalize(snapshot));
        await vm.BackfillOfficialContextsAsync();
        Check(record.ContextBackfillAttempted && record.Context is not null && saves == 1, "SQLite history context backfilled and persisted");
        await vm.BackfillOfficialContextsAsync(); Check(saves == 1, "backfill once");
        var errors = new List<Exception>();
        using var failed = new BatteryHealthViewModel(config, _ => Task.FromException(new IOException("synthetic failure")), null,
            action => { action(); return Task.CompletedTask; }, _ => { }, errors.Add, () => { });
        failed.SetSnapshot(snapshot, UpsTelemetryValidator.Normalize(snapshot));
        failed.OfficialRecordPercent = 66; failed.OfficialRecordNote = "keep edit";
        failed.RecordOfficialHealthCommand.Execute(null);
        await Until(() => failed.RecordOfficialHealthCommand.CanExecute(null));
        Check(errors.Count == 1 && failed.OfficialRecordPercent == 66 && failed.OfficialRecordNote == "keep edit", "record save failure retains editor");
    }).GetAwaiter().GetResult();

    public static void BindingsAndNativeWiring()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var xaml = File.ReadAllText(Path.Combine(root, "UpsMonitor.App/MainWindow.xaml"));
        foreach (var (prefix, type) in new[] { ("Settings", typeof(SettingsViewModel)), ("BatteryHealth", typeof(BatteryHealthViewModel)) })
        {
            var bindings = Regex.Matches(xaml, @"\{Binding " + prefix + @"\.([\w.]+)");
            Check(bindings.Count > 0, "child bindings present");
            foreach (Match binding in bindings)
            {
                var current = type;
                foreach (var segment in binding.Groups[1].Value.Split('.'))
                { var property = current.GetProperty(segment); Check(property is not null, "binding exists: " + binding.Value); current = property!.PropertyType; }
            }
            foreach (var property in type.GetProperties())
                Check(!Regex.IsMatch(xaml, @"\{Binding " + property.Name + @"(?=[,}])"), "no stale root binding: " + property.Name);
        }
        var source = File.ReadAllText(Path.Combine(root, "UpsMonitor.App/MainViewModel.cs"));
        Check(source.Contains("StartupManager.SetRunOnStartup, _engine.SetPollInterval, _engine.SetRuntimeLowThreshold") &&
            source.Contains("_engine.SetAlertThresholds") && source.Contains("ThemeManager.ApplyTheme") && source.Contains("LocalizationManager.ApplyLanguage"), "native effects wired to real services");
        Check(source.Contains("BatteryHealth.SetSnapshot(snapshot, telemetry)") && source.Contains("BatteryHealth.Initialize()") && source.Contains("Settings.Dispose()") && source.Contains("BatteryHealth.Dispose()"), "snapshot initialization and disposal");
        Check(source.Contains("Settings.RefreshLanguage(LocalizationManager.CurrentLanguageCode)"), "language event refreshes settings labels and selection");
        foreach (var language in new[] { "ja-JP", "en-US" })
        {
            var resources = System.Xml.Linq.XDocument.Load(Path.Combine(root, "UpsMonitor.App/Resources", $"Strings.{language}.xaml"))
                .Root!.Elements().Select(e => e.Attribute(System.Xml.Linq.XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value).ToHashSet();
            foreach (var file in new[] { "SettingsViewModel", "BatteryHealthViewModel" })
            foreach (Match key in Regex.Matches(File.ReadAllText(Path.Combine(root, "UpsMonitor.App", file + ".cs")), "(?:L|Format)\\(\"(\\w+)\""))
                Check(resources.Contains(key.Groups[1].Value), "resource key exists: " + key.Groups[1].Value);
        }
    }

    private static async Task Until(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!ready()) { if (DateTime.UtcNow > deadline) throw new TimeoutException(); await Task.Delay(10); }
    }
}

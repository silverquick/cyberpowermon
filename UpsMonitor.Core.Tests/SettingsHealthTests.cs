using UpsMonitor.App;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

internal static class SettingsHealthTests
{
    public static void SourceParity()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var source = File.ReadAllText(Path.Combine(root, "UpsMonitor.App/MainViewModel.cs")).Replace("\r", "");
        var adapter = File.ReadAllText(Path.Combine(root, "UpsMonitor.Core.Tests/SettingsHealthTestAdapter.cs")).Replace("\r", "");
        foreach (System.Text.RegularExpressions.Match member in System.Text.RegularExpressions.Regex.Matches(adapter,
            @"(?ms)^    // Original member: \w+\n(.*?)(?=^    // Original member:|^}\n\z)"))
        {
            var text = member.Groups[1].Value
                .Replace("_save(_configuration)", "_configurationStore.SaveAsync(_configuration)")
                .Replace("_effects.ApplyTheme(value)", "ThemeManager.ApplyTheme(Application.Current, value)")
                .Replace("_effects.ApplyLanguage(value)", "LocalizationManager.ApplyLanguage(Application.Current, value)")
                .Replace("_effects.SetRunOnStartup", "StartupManager.SetRunOnStartup")
                .Replace("_effects.RefreshPeriodCost()", "History.RefreshPeriodCost()")
                .Replace("_effects.Set", "_engine.Set").Replace("_dispatch", "_dispatcher.InvokeAsync");
            Check(source.Contains(text), "adapter copied production member " + member.Value.Split('\n')[0]);
        }
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static SettingsEffects Effects(List<string> calls) => new(
        theme => calls.Add("theme:" + theme), language => calls.Add("language:" + language),
        (enabled, minimized) => calls.Add($"startup:{enabled}:{minimized}"),
        interval => calls.Add("poll:" + interval), threshold => calls.Add("runtime:" + threshold.TotalSeconds),
        thresholds => calls.Add("alerts:" + thresholds.HighLoadPercent), () => calls.Add("cost"), () => calls.Add("snapshot"));
    private static UpsSnapshot Snapshot => new()
    {
        Timestamp = DateTimeOffset.Now, Device = HistoryViewModelTests.Device, IsConnected = true,
        BatteryPercent = 100, PercentLoad = 40, RuntimeRemaining = TimeSpan.FromMinutes(20),
        ActivePower = 200, BatteryVoltage = 26, InputVoltage = 100, Charging = false,
    };

    public static void SaveFailureAndSuccess() => Work().GetAwaiter().GetResult();
    private static async Task Work()
    {
        var calls = new List<string>();
        var config = new AppConfiguration();
        var fail = true;
        var vm = new SettingsHealthTestAdapter(config, _ =>
        { calls.Add("save"); return fail ? Task.FromException(new IOException("synthetic failure")) : Task.CompletedTask; }, Effects(calls));
        vm.SetSnapshot(Snapshot);
        vm.PollIntervalMs = 1500; vm.RuntimeLowSeconds = 180;
        vm.HighLoadWarningPercent = 85; vm.BatteryWarningThresholdPercent = 75;
        try { await vm.Save(); throw new InvalidOperationException("expected save failure"); }
        catch (IOException) { }
        Check(vm.PollIntervalMs == 1500 && vm.RuntimeLowSeconds == 180 && vm.BatteryWarningThresholdPercent == 75, "failure preserves edits");
        Check(calls.SequenceEqual(new[] { "save" }), "engine only changes after successful save");
        fail = false; calls.Clear(); await vm.Save();
        Check(calls.SequenceEqual(new[] { "save", "poll:1500", "runtime:180", "alerts:85", "snapshot" }), "save then engine and snapshot");
        Check(config.Monitoring.PollIntervalMs == 1500 && config.BatteryHealth.WarningThresholdPercent == 75, "saved configuration");
        vm.PollIntervalMs = 1; calls.Clear(); await vm.Save();
        Check(calls.Count == 0 && vm.SettingsStatus == LocalizationManager.Get("PollIntervalValidation"), "invalid settings do not save");
    }

    public static void ImmediateChoicesAndLanguage()
    {
        var calls = new List<string>(); var config = new AppConfiguration();
        var vm = new SettingsHealthTestAdapter(config, _ => Task.CompletedTask, Effects(calls));
        vm.SelectedTheme = "dark"; vm.SelectedLanguageCode = "ja-JP";
        vm.RunOnStartup = true; vm.StartMinimized = true; vm.ElectricityRatePerKwh = 40;
        Check(config.Ui.Theme == "dark" && config.Ui.Language == "ja-JP" && calls.Contains("theme:dark") && calls.Contains("language:ja-JP"), "theme/language immediate effects");
        Check(calls.Contains("startup:True:True") && calls.Contains("cost"), "startup and period cost update immediately");
        var language = LocalizationManager.Language;
        try
        {
            vm.SelectedBaselineKind = BatteryRuntimeBaselineKind.KnownHealthAnchor;
            vm.SelectedVendorHealthCategory = VendorBatteryHealthCategory.Poor;
            var theme = vm.ThemeOptions;
            foreach (var lang in new[] { "en-US", "ja-JP" })
            {
                LocalizationManager.Language = lang; vm.Language();
                Check(vm.SelectedTheme == "dark" && ReferenceEquals(theme, vm.ThemeOptions), "theme selection/list identity retained");
                Check(vm.SelectedBaselineKind == BatteryRuntimeBaselineKind.KnownHealthAnchor && vm.SelectedVendorHealthCategory == VendorBatteryHealthCategory.Poor, "baseline selections retained");
            }
        }
        finally { LocalizationManager.Language = language; }
    }

    public static void OfficialRecordsAndBaseline() => RecordsAsync().GetAwaiter().GetResult();
    private static async Task RecordsAsync()
    {
        var config = new AppConfiguration(); var calls = new List<string>();
        var vm = new SettingsHealthTestAdapter(config, _ => { calls.Add("save"); return Task.CompletedTask; }, Effects(calls));
        vm.OfficialRecordPercent = 80; vm.OfficialRecordDate = DateTime.Today; vm.OfficialRecordTime = "";
        await vm.Record(); Check(config.BatteryHealth.Profiles.Count == 0, "record requires UPS");
        vm.SetSnapshot(Snapshot);
        vm.OfficialRecordTime = "bad"; await vm.Record(); Check(config.BatteryHealth.Profiles.Count == 0, "invalid time rejects record");
        vm.OfficialRecordTime = "";
        for (var i = 0; i < 3; i++)
        {
            vm.OfficialRecordPercent = 80 - i; vm.OfficialRecordNote = " synthetic "; await vm.Record();
        }
        var profile = config.BatteryHealth.Profiles.Single();
        Check(profile.OfficialHealthRecords.Count == 3 && vm.OfficialRecords.Count == 3, "records stored and displayed");
        Check(profile.OfficialHealthRecords.All(r => r.Context?.LoadPercent == 40 && r.Note == "synthetic"), "live record context and note");
        Check(vm.OfficialRecordCountText == LocalizationManager.Format("OfficialRecordCountFormat", 3, 1), "24 hour grouping count");
        await vm.Delete(profile.OfficialHealthRecords[0]); Check(profile.OfficialHealthRecords.Count == 2 && vm.OfficialRecords.Count == 2, "delete record");
        vm.SelectedBaselineKind = BatteryRuntimeBaselineKind.NewBattery; await vm.Baseline();
        Check(profile.RuntimeBaselines.Single().Runtime == TimeSpan.FromMinutes(20) && profile.AnchorHealthPercent == 100 && profile.BatteryInstalledAt is not null, "new battery baseline");
        await vm.ClearBaseline(); Check(profile.RuntimeBaselines.Count == 0 && profile.AnchorHealthPercent is null && profile.BatteryInstalledAt is not null, "clear baseline retains purchase date");
        await vm.Installed(DateTime.Today.AddDays(-100)); Check(profile.BatteryInstalledAt?.Date == DateTime.Today.AddDays(-100), "purchase date saved");
    }
}

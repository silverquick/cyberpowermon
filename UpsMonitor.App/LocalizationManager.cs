using System.Globalization;
using System.Windows;

namespace UpsMonitor.App;

internal static class LocalizationManager
{
    private const string Japanese = "ja-JP";
    private const string English = "en-US";
    private static ResourceDictionary? _activeDictionary;
    private static readonly IReadOnlyDictionary<(ushort Page, ushort Usage), string> JapaneseUsageNames =
        new Dictionary<(ushort, ushort), string>
        {
            [(0x84, 0x02)] = "現在状態",
            [(0x84, 0x04)] = "UPS",
            [(0x84, 0x10)] = "バッテリーシステム",
            [(0x84, 0x12)] = "バッテリー",
            [(0x84, 0x14)] = "充電器",
            [(0x84, 0x18)] = "出力系統",
            [(0x84, 0x1A)] = "入力",
            [(0x84, 0x1C)] = "出力",
            [(0x84, 0x20)] = "コンセント",
            [(0x84, 0x24)] = "電源概要",
            [(0x84, 0x30)] = "電圧",
            [(0x84, 0x31)] = "電流",
            [(0x84, 0x32)] = "周波数",
            [(0x84, 0x33)] = "皮相電力",
            [(0x84, 0x34)] = "有効電力",
            [(0x84, 0x35)] = "負荷率",
            [(0x84, 0x36)] = "温度",
            [(0x84, 0x40)] = "定格電圧",
            [(0x84, 0x42)] = "定格周波数",
            [(0x84, 0x43)] = "定格皮相電力",
            [(0x84, 0x44)] = "定格有効電力",
            [(0x84, 0x53)] = "低電圧切替値",
            [(0x84, 0x54)] = "高電圧切替値",
            [(0x84, 0x55)] = "再起動前待機",
            [(0x84, 0x56)] = "起動前待機",
            [(0x84, 0x57)] = "停止前待機",
            [(0x84, 0x58)] = "セルフテスト",
            [(0x84, 0x5A)] = "警告音制御",
            [(0x84, 0x60)] = "存在",
            [(0x84, 0x61)] = "正常",
            [(0x84, 0x62)] = "内部障害",
            [(0x84, 0x63)] = "電圧範囲外",
            [(0x84, 0x64)] = "周波数範囲外",
            [(0x84, 0x65)] = "過負荷",
            [(0x84, 0x66)] = "過充電",
            [(0x84, 0x67)] = "過温度",
            [(0x84, 0x68)] = "停止要求",
            [(0x84, 0x69)] = "停止切迫",
            [(0x84, 0x6E)] = "昇圧",
            [(0x84, 0x6F)] = "降圧",
            [(0x84, 0x70)] = "初期化済み",
            [(0x84, 0x71)] = "テスト済み",
            [(0x84, 0x72)] = "電源待機中",
            [(0x84, 0x73)] = "通信切断",
            [(0x84, 0xFD)] = "製造元文字列",
            [(0x84, 0xFE)] = "製品文字列",
            [(0x84, 0xFF)] = "シリアル文字列",
            [(0x85, 0x29)] = "残容量しきい値",
            [(0x85, 0x2A)] = "残り時間しきい値",
            [(0x85, 0x2C)] = "容量モード",
            [(0x85, 0x42)] = "残容量しきい値未満",
            [(0x85, 0x43)] = "残り時間しきい値超過",
            [(0x85, 0x44)] = "充電中",
            [(0x85, 0x45)] = "放電中",
            [(0x85, 0x46)] = "満充電",
            [(0x85, 0x47)] = "完全放電",
            [(0x85, 0x4B)] = "交換必要",
            [(0x85, 0x60)] = "満充電までの時間",
            [(0x85, 0x61)] = "空になるまでの時間",
            [(0x85, 0x62)] = "平均電流",
            [(0x85, 0x63)] = "最大誤差",
            [(0x85, 0x64)] = "相対充電率",
            [(0x85, 0x65)] = "絶対充電率",
            [(0x85, 0x66)] = "残容量",
            [(0x85, 0x67)] = "満充電容量",
            [(0x85, 0x68)] = "残り運転時間",
            [(0x85, 0x69)] = "平均残り時間",
            [(0x85, 0x6A)] = "平均満充電時間",
            [(0x85, 0x6B)] = "充放電回数",
            [(0x85, 0x83)] = "設計容量",
            [(0x85, 0x85)] = "製造日",
            [(0x85, 0x86)] = "シリアル番号",
            [(0x85, 0x87)] = "製造元名",
            [(0x85, 0x88)] = "デバイス名",
            [(0x85, 0x89)] = "バッテリー種類",
            [(0x85, 0x8A)] = "製造元データ",
            [(0x85, 0x8B)] = "充電可能",
            [(0x85, 0x8C)] = "警告容量しきい値",
            [(0x85, 0x8D)] = "容量分解能1",
            [(0x85, 0x8E)] = "容量分解能2",
            [(0x85, 0x8F)] = "OEM情報",
            [(0x85, 0xD0)] = "商用電源あり",
            [(0x85, 0xD1)] = "バッテリーあり",
            [(0x85, 0xD2)] = "電源障害",
        };

    private static readonly IReadOnlyDictionary<string, string> JapaneseCollectionNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Battery"] = "バッテリー",
            ["BatterySystem"] = "バッテリーシステム",
            ["Charger"] = "充電器",
            ["Input"] = "入力",
            ["Output"] = "出力",
            ["Outlet"] = "コンセント",
            ["OutletSystem"] = "出力系統",
            ["PowerSummary"] = "電源概要",
            ["PresentStatus"] = "現在状態",
            ["UPS"] = "UPS",
        };

    internal static event EventHandler? LanguageChanged;

    internal static string CurrentLanguageCode { get; private set; } = English;

    internal static bool IsJapanese => CurrentLanguageCode == Japanese;

    internal static string ResolveLanguage(string? configuredLanguage)
    {
        if (string.Equals(configuredLanguage, Japanese, StringComparison.OrdinalIgnoreCase))
        {
            return Japanese;
        }

        if (string.Equals(configuredLanguage, English, StringComparison.OrdinalIgnoreCase))
        {
            return English;
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? Japanese : English;
    }

    internal static void ApplyLanguage(Application application, string? language)
    {
        var resolved = ResolveLanguage(language);
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"Resources/Strings.{resolved}.xaml", UriKind.Relative),
        };

        if (_activeDictionary is not null)
        {
            application.Resources.MergedDictionaries.Remove(_activeDictionary);
        }

        application.Resources.MergedDictionaries.Add(dictionary);
        _activeDictionary = dictionary;
        CurrentLanguageCode = resolved;

        var culture = CultureInfo.GetCultureInfo(resolved);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    internal static string Get(string key) =>
        Application.Current.TryFindResource(key) as string ?? key;

    internal static string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

    internal static string LocalizeTelemetryValue(string value) => value switch
    {
        "True" => Get("ValueTrue"),
        "False" => Get("ValueFalse"),
        "Disabled" => Get("AlarmDisabled"),
        "Enabled" => Get("AlarmEnabled"),
        "Muted" => Get("AlarmMuted"),
        "Done - passed" => Get("TestPassed"),
        "Done - warning" => Get("TestWarning"),
        "Done - error" => Get("TestError"),
        "Aborted" => Get("TestAborted"),
        "In progress" => Get("TestInProgress"),
        "No test initiated" => Get("TestNotStarted"),
        "PbAcid" => Get("ChemistryLeadAcid"),
        _ => value,
    };

    internal static string LocalizeUsageName(ushort usagePage, ushort usage, string fallback)
    {
        if (!IsJapanese || !JapaneseUsageNames.TryGetValue((usagePage, usage), out var localized))
        {
            return fallback;
        }

        return $"{localized} ({fallback})";
    }

    internal static string LocalizeCollectionPath(string path)
    {
        if (!IsJapanese || string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return string.Join(
            " / ",
            path.Split(" / ", StringSplitOptions.TrimEntries)
                .Select(segment => JapaneseCollectionNames.GetValueOrDefault(segment, segment)));
    }
}

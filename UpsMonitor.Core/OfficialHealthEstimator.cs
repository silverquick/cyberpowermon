namespace UpsMonitor.Core;

/// <summary>公式値の推定モデル。値は config に保存されるため既存の順序を変えず末尾に追加する。</summary>
public enum OfficialHealthModel
{
    /// <summary>直近の記録の傾きで外挿(従来方式)。</summary>
    RecentLinear,
    /// <summary>購入日 100% を通る原点固定の直線。</summary>
    InstallAnchoredLinear,
    /// <summary>購入日 100% を通る加速曲線 100 - a×年数^p。</summary>
    AcceleratingPower,
}

/// <summary>
/// 公式値を記録した瞬間の状況と直前の統計。後から公式値と負荷等の関係を検証するための保存用。
/// データが得られない項目は null。
/// </summary>
public sealed record OfficialHealthContext
{
    public double? LoadPercent { get; set; }
    public double? ActivePowerWatts { get; set; }
    public double? RuntimeSeconds { get; set; }
    public double? BatteryVoltage { get; set; }
    public double? InputVoltage { get; set; }
    public double? TemperatureCelsius { get; set; }
    public bool? Charging { get; set; }
    public double? Load10MinuteMean { get; set; }
    public double? Load10MinuteMax { get; set; }
    public double? Load24HourMean { get; set; }
    public double? Load24HourMax { get; set; }
    public double? BatteryVoltage24HourMean { get; set; }
    /// <summary>最後に停電等でバッテリー運転した後の日数。履歴に無ければ null。</summary>
    public double? DaysSinceLastDischarge { get; set; }
    /// <summary>履歴DBで確認できた最古の時刻。DaysSinceLastDischarge が null のとき保持範囲の目安になる。</summary>
    public DateTimeOffset? HistoryStartedAt { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasAnyValue =>
        LoadPercent.HasValue || ActivePowerWatts.HasValue || RuntimeSeconds.HasValue
        || BatteryVoltage.HasValue || InputVoltage.HasValue || TemperatureCelsius.HasValue
        || Charging.HasValue || Load10MinuteMean.HasValue || Load10MinuteMax.HasValue
        || Load24HourMean.HasValue || Load24HourMax.HasValue || BatteryVoltage24HourMean.HasValue
        || DaysSinceLastDischarge.HasValue;
}

/// <summary>
/// 公式ツール(CyberPower BHI 等)が示したバッテリー健全性の記録。
/// PredictedPercent は記録追加前の採用モデルの推定値で、予測誤差の算出に使う。
/// </summary>
public sealed record OfficialHealthRecord
{
    public DateTimeOffset RecordedAt { get; set; }
    public double HealthPercent { get; set; }
    public VendorBatteryHealthCategory Source { get; set; }
    public double? PredictedPercent { get; set; }
    public string? Note { get; set; }
    /// <summary>記録追加時点で利用可能だった全モデルの予測値。旧記録は null。</summary>
    public Dictionary<OfficialHealthModel, double>? ModelPredictions { get; set; }
    /// <summary>記録時の状況。旧記録や未取得は null。</summary>
    public OfficialHealthContext? Context { get; set; }
    /// <summary>記録時または起動時に履歴取得が正常完了したか。true なら結果が空でも再試行しない。</summary>
    public bool ContextBackfillAttempted { get; set; }
}

public sealed record OfficialHealthModelResult(
    OfficialHealthModel Model,
    double? Percent,
    int BacktestCount,
    double? MeanAbsoluteError)
{
    public bool IsAvailable => Percent.HasValue;
}

public sealed record OfficialHealthEstimate
{
    public required double Percent { get; init; }
    public required BatteryHealthConfidence Confidence { get; init; }
    /// <summary>最後の実記録から評価時刻までの日数。実記録が無ければ null。</summary>
    public double? DaysSinceLastRecord { get; init; }
    /// <summary>採用モデルの傾き(pt/年)。外挿時は低下方向のみ・-40 以上にクリップ済み。</summary>
    public double SlopePointsPerYear { get; init; }
    public OfficialHealthRecord? LastRecord { get; init; }
    public OfficialHealthRecord? PreviousRecord { get; init; }
    public int RecordCount { get; init; }
    public OfficialHealthModel Model { get; init; }
    /// <summary>全モデル(利用不可は Percent が null)。</summary>
    public IReadOnlyList<OfficialHealthModelResult> Models { get; init; } = [];
    /// <summary>利用可能な全モデルの値の最小・最大。利用可能が1つなら採用値と同じ。</summary>
    public double MinPercent { get; init; }
    public double MaxPercent { get; init; }
    public double SpreadPercent => MaxPercent - MinPercent;
}

public static class OfficialHealthEstimator
{
    public const double MaximumDeclinePointsPerYear = 40;
    public const double SlopeWindowDays = 365;
    public const double RoundingToleranceUnits = 0.5;
    public const int MinimumBacktestsForSelection = 3;
    private const double DaysPerYear = 365;
    private const double PowerExponentMinimum = 1.0;
    private const double PowerExponentMaximum = 3.0;
    private const double PowerExponentStep = 0.01;

    private static readonly OfficialHealthModel[] AllModels =
    [
        OfficialHealthModel.RecentLinear,
        OfficialHealthModel.InstallAnchoredLinear,
        OfficialHealthModel.AcceleratingPower,
    ];

    /// <summary>MAE が同値のときの優先順(先頭ほど優先)。</summary>
    private static readonly OfficialHealthModel[] TiePriority =
    [
        OfficialHealthModel.InstallAnchoredLinear,
        OfficialHealthModel.RecentLinear,
        OfficialHealthModel.AcceleratingPower,
    ];

    /// <summary>|誤差| が丸め(整数表示)の範囲内か。</summary>
    public static bool IsWithinRounding(double error) => Math.Abs(error) <= RoundingToleranceUnits + 1e-9;

    public static OfficialHealthEstimate? Estimate(
        IReadOnlyList<OfficialHealthRecord> records,
        DateTimeOffset? installedAt,
        DateTimeOffset at)
    {
        var valid = SortedValid(records);
        var values = new Dictionary<OfficialHealthModel, ModelValue>();
        foreach (var model in AllModels)
        {
            if (EvaluateModel(model, valid, installedAt, at) is { } value)
            {
                values[model] = value;
            }
        }

        // M1 は購入日だけでも評価できる。M1 が無ければ記録も購入日も無い。
        if (!values.ContainsKey(OfficialHealthModel.RecentLinear))
        {
            return null;
        }

        var backtests = CachedBacktest(valid, installedAt);
        var selected = SelectModel(values.Keys, backtests);
        var selectedValue = values[selected];

        double? daysSince = null;
        var confidence = BatteryHealthConfidence.Low;
        if (valid.Length > 0)
        {
            daysSince = Math.Max(0, (at - valid[^1].RecordedAt).TotalDays);
            confidence = daysSince <= 14
                ? BatteryHealthConfidence.High
                : daysSince <= 60 ? BatteryHealthConfidence.Medium : BatteryHealthConfidence.Low;
            if (valid.Length == 1 && confidence == BatteryHealthConfidence.High)
            {
                confidence = BatteryHealthConfidence.Medium;
            }
        }

        var results = AllModels
            .Select(model => new OfficialHealthModelResult(
                model,
                values.TryGetValue(model, out var value) ? value.Percent : null,
                backtests.TryGetValue(model, out var stat) ? stat.Count : 0,
                backtests.TryGetValue(model, out stat) && stat.Count > 0 ? stat.Mae : null))
            .ToArray();
        var available = results.Where(item => item.Percent.HasValue).Select(item => item.Percent!.Value).ToArray();

        return new OfficialHealthEstimate
        {
            Percent = selectedValue.Percent,
            Confidence = confidence,
            DaysSinceLastRecord = daysSince,
            SlopePointsPerYear = selectedValue.SlopePerYear,
            LastRecord = valid.Length > 0 ? valid[^1] : null,
            PreviousRecord = valid.Length > 1 ? valid[^2] : null,
            RecordCount = valid.Length,
            Model = selected,
            Models = results,
            MinPercent = available.Min(),
            MaxPercent = available.Max(),
        };
    }

    /// <summary>at より前の記録のみで推定する(新記録追加前の予測値)。</summary>
    public static OfficialHealthEstimate? Predict(
        IReadOnlyList<OfficialHealthRecord> records,
        DateTimeOffset? installedAt,
        DateTimeOffset at) =>
        Estimate(records.Where(item => item.RecordedAt < at).ToArray(), installedAt, at);

    /// <summary>
    /// 各記録 i(i≥1)を、それ以前の記録(+購入日)だけで各モデルが予測した場合の件数と MAE。
    /// 予測できなかったモデルは含まれない。
    /// </summary>
    public static IReadOnlyList<OfficialHealthModelResult> BacktestModels(
        IReadOnlyList<OfficialHealthRecord> records,
        DateTimeOffset? installedAt)
    {
        var stats = Backtest(SortedValid(records), installedAt);
        return AllModels
            .Select(model => stats.TryGetValue(model, out var stat)
                ? new OfficialHealthModelResult(model, null, stat.Count, stat.Mae)
                : new OfficialHealthModelResult(model, null, 0, null))
            .ToArray();
    }

    /// <summary>
    /// newRecord より前の記録だけで PredictedPercent と ModelPredictions を決め、時刻順の位置に挿入する。
    /// 前の記録も設置日も無ければ PredictedPercent は null。
    /// </summary>
    public static OfficialHealthRecord AddRecord(
        IList<OfficialHealthRecord> records,
        DateTimeOffset? installedAt,
        OfficialHealthRecord newRecord)
    {
        var prediction = Predict(records.ToArray(), installedAt, newRecord.RecordedAt);
        newRecord.PredictedPercent = prediction?.Percent;
        newRecord.ModelPredictions = prediction is null
            ? null
            : prediction.Models
                .Where(item => item.Percent.HasValue)
                .ToDictionary(item => item.Model, item => item.Percent!.Value);
        var index = records.Count;
        while (index > 0 && records[index - 1].RecordedAt > newRecord.RecordedAt)
        {
            index--;
        }

        records.Insert(index, newRecord);
        return newRecord;
    }

    /// <summary>誤差 = 公式値 - 予測値(PredictedPercent のある記録のみ)。</summary>
    public static IReadOnlyList<(OfficialHealthRecord Record, double Error)> Errors(
        IEnumerable<OfficialHealthRecord> records) =>
        records
            .Where(item => item.PredictedPercent.HasValue)
            .OrderBy(item => item.RecordedAt)
            .Select(item => (item, item.HealthPercent - item.PredictedPercent!.Value))
            .ToArray();

    public static double? MeanAbsoluteError(IEnumerable<OfficialHealthRecord> records)
    {
        var errors = Errors(records);
        return errors.Count == 0 ? null : errors.Average(item => Math.Abs(item.Error));
    }

    private readonly record struct ModelValue(double Percent, double SlopePerYear);

    private readonly record struct BacktestStat(int Count, double Mae);

    private static OfficialHealthRecord[] SortedValid(IReadOnlyList<OfficialHealthRecord> records) =>
        records
            .Where(item => double.IsFinite(item.HealthPercent))
            .OrderBy(item => item.RecordedAt)
            .ToArray();

    private static readonly object BacktestCacheLock = new();
    private static (string Key, Dictionary<OfficialHealthModel, BacktestStat> Value)? _backtestCache;

    // 毎秒の再計算でバックテストをやり直さないよう、記録内容が同じ間は結果を再利用する。
    private static Dictionary<OfficialHealthModel, BacktestStat> CachedBacktest(
        OfficialHealthRecord[] valid,
        DateTimeOffset? installedAt)
    {
        var key = string.Join(
            ';',
            valid.Select(item => FormattableString.Invariant($"{item.RecordedAt.UtcTicks}:{item.HealthPercent:R}"))
                .Prepend(installedAt?.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"));
        lock (BacktestCacheLock)
        {
            if (_backtestCache is { } cached && cached.Key == key)
            {
                return cached.Value;
            }
        }

        var computed = Backtest(valid, installedAt);
        lock (BacktestCacheLock)
        {
            _backtestCache = (key, computed);
        }

        return computed;
    }

    private static Dictionary<OfficialHealthModel, BacktestStat> Backtest(
        OfficialHealthRecord[] valid,
        DateTimeOffset? installedAt)
    {
        var sums = new Dictionary<OfficialHealthModel, (int Count, double Sum)>();
        for (var i = 1; i < valid.Length; i++)
        {
            var prior = valid[..i];
            foreach (var model in AllModels)
            {
                if (EvaluateModel(model, prior, installedAt, valid[i].RecordedAt) is not { } value)
                {
                    continue;
                }

                var error = Math.Abs(valid[i].HealthPercent - value.Percent);
                sums.TryGetValue(model, out var current);
                sums[model] = (current.Count + 1, current.Sum + error);
            }
        }

        return sums.ToDictionary(item => item.Key, item => new BacktestStat(item.Value.Count, item.Value.Sum / item.Value.Count));
    }

    private static OfficialHealthModel SelectModel(
        IEnumerable<OfficialHealthModel> available,
        Dictionary<OfficialHealthModel, BacktestStat> backtests)
    {
        var models = available.ToArray();
        var enough = models.All(model =>
            backtests.TryGetValue(model, out var stat) && stat.Count >= MinimumBacktestsForSelection);
        if (!enough)
        {
            return models.Contains(OfficialHealthModel.InstallAnchoredLinear)
                ? OfficialHealthModel.InstallAnchoredLinear
                : OfficialHealthModel.RecentLinear;
        }

        var best = models.Min(model => backtests[model].Mae);
        return TiePriority.First(model =>
            models.Contains(model) && backtests[model].Mae <= best + 1e-9);
    }

    private static ModelValue? EvaluateModel(
        OfficialHealthModel model,
        OfficialHealthRecord[] valid,
        DateTimeOffset? installedAt,
        DateTimeOffset at)
    {
        // 購入日が最古の記録より後なら無視する(M1 の既存挙動)。M2/M3 は購入日が最古の記録より前であることが必要。
        var useInstall = installedAt is { } installed
            && (valid.Length == 0 || installed <= valid[0].RecordedAt);
        // M2/M3 は利用可否を範囲内・外挿の別なく先に判定する。
        double installSlopePerYear = 0;
        (double A, double P) powerFit = default;
        if (model == OfficialHealthModel.InstallAnchoredLinear)
        {
            if (!useInstall || FitInstallAnchored(valid, installedAt!.Value) is not { } fitted)
            {
                return null;
            }

            installSlopePerYear = fitted;
        }
        else if (model == OfficialHealthModel.AcceleratingPower)
        {
            if (!useInstall || FitPower(valid, installedAt!.Value) is not { } fitted)
            {
                return null;
            }

            powerFit = fitted;
        }

        var points = valid
            .Select(item => (Time: item.RecordedAt, Value: item.HealthPercent))
            .ToList();
        if (useInstall)
        {
            points.Add((installedAt!.Value, 100));
        }

        if (points.Count == 0)
        {
            return null;
        }

        points = points.OrderBy(item => item.Time).ToList();
        var first = points[0];
        var last = points[^1];
        if (at <= first.Time)
        {
            return new ModelValue(Math.Clamp(first.Value, 0, 100), 0);
        }

        if (at <= last.Time)
        {
            // 記録範囲内は全モデル共通で線形補間する。
            var index = points.FindLastIndex(item => item.Time < at);
            var lower = points[index];
            var upper = points[index + 1];
            var span = (upper.Time - lower.Time).TotalDays;
            var slope = span > 0 ? (upper.Value - lower.Value) / span : 0;
            var value = span > 0 ? lower.Value + (slope * (at - lower.Time).TotalDays) : upper.Value;
            return new ModelValue(Math.Clamp(value, 0, 100), slope * DaysPerYear);
        }

        // 外挿: 最後の記録の値 + (モデル曲線の増分)。最後の記録とは必ず一致する。
        var elapsedDays = (at - last.Time).TotalDays;
        double slopePerDay;
        switch (model)
        {
            case OfficialHealthModel.RecentLinear:
                var window = points.Where(item => (last.Time - item.Time).TotalDays <= SlopeWindowDays).ToArray();
                slopePerDay = window.Length >= 2 ? LeastSquaresSlopePerDay(window) : 0;
                break;

            case OfficialHealthModel.InstallAnchoredLinear:
                slopePerDay = installSlopePerYear / DaysPerYear;
                break;

            default:
                {
                    // 曲線の増分を経過日数で割った平均傾きにする。
                    var tLast = (last.Time - installedAt!.Value).TotalDays / DaysPerYear;
                    var tAt = (at - installedAt.Value).TotalDays / DaysPerYear;
                    var delta = -powerFit.A * (Math.Pow(tAt, powerFit.P) - Math.Pow(tLast, powerFit.P));
                    slopePerDay = elapsedDays > 0 ? delta / elapsedDays : 0;
                    break;
                }
        }

        slopePerDay = Math.Clamp(slopePerDay, -MaximumDeclinePointsPerYear / DaysPerYear, 0);
        var percent = last.Value + (slopePerDay * elapsedDays);
        return new ModelValue(Math.Clamp(percent, 0, 100), slopePerDay * DaysPerYear);
    }

    /// <summary>購入日 (0,100) を通る原点固定の最小二乗直線の低下速度(pt/年)。b = Σ t·d / Σ t²。</summary>
    private static double? FitInstallAnchored(OfficialHealthRecord[] valid, DateTimeOffset installedAt)
    {
        double numerator = 0, denominator = 0;
        foreach (var record in valid)
        {
            var t = (record.RecordedAt - installedAt).TotalDays / DaysPerYear;
            if (t <= 0)
            {
                continue;
            }

            numerator += t * (100 - record.HealthPercent);
            denominator += t * t;
        }

        // 傾きは低下方向(正の b)のみ。b<0 の場合は 0 低下として扱う。
        return denominator > 0 ? -Math.Max(0, numerator / denominator) : null;
    }

    /// <summary>h = 100 - a×t^p を p∈[1,3] のグリッドで探索し、a は閉形式の最小二乗。記録2件以上が必要。</summary>
    public static (double A, double P)? FitPower(OfficialHealthRecord[] valid, DateTimeOffset installedAt)
    {
        var samples = valid
            .Select(record => (T: (record.RecordedAt - installedAt).TotalDays / DaysPerYear, D: 100 - record.HealthPercent))
            .Where(item => item.T > 0)
            .ToArray();
        if (samples.Length < 2)
        {
            return null;
        }

        var steps = (int)Math.Round((PowerExponentMaximum - PowerExponentMinimum) / PowerExponentStep);
        (double A, double P)? best = null;
        var bestSse = double.PositiveInfinity;
        for (var i = 0; i <= steps; i++)
        {
            var p = PowerExponentMinimum + (i * PowerExponentStep);
            double numerator = 0, denominator = 0;
            foreach (var (t, d) in samples)
            {
                var basis = Math.Pow(t, p);
                numerator += d * basis;
                denominator += basis * basis;
            }

            if (denominator <= 0)
            {
                continue;
            }

            // 低下方向のみ(a≥0)。
            var a = Math.Max(0, numerator / denominator);
            var sse = samples.Sum(item =>
            {
                var residual = item.D - (a * Math.Pow(item.T, p));
                return residual * residual;
            });
            if (sse < bestSse - 1e-12)
            {
                bestSse = sse;
                best = (a, p);
            }
        }

        return best;
    }

    /// <summary>
    /// 旧 KnownHealthAnchor 基準を公式値記録へ1件だけ移行する。冪等。
    /// 元の基準・アンカーは変更しない。
    /// </summary>
    public static bool TryMigrateLegacyAnchor(BatteryHealthProfile profile)
    {
        if (profile.RuntimeBaselineKind != BatteryRuntimeBaselineKind.KnownHealthAnchor
            || profile.AnchorHealthPercent is not { } anchor
            || !(anchor > 0 && anchor <= 100 && double.IsFinite(anchor))
            || profile.BaselineRecordedAt is not { } recordedAt)
        {
            return false;
        }

        if (profile.OfficialHealthRecords.Any(item =>
                SameSecond(item.RecordedAt, recordedAt)
                && Math.Abs(item.HealthPercent - anchor) < 1e-9))
        {
            return false;
        }

        profile.OfficialHealthRecords.Insert(0, new OfficialHealthRecord
        {
            RecordedAt = recordedAt,
            HealthPercent = anchor,
            Source = profile.VendorHealthCategory,
            PredictedPercent = null,
            Note = "legacy anchor",
        });
        return true;
    }

    private static bool SameSecond(DateTimeOffset left, DateTimeOffset right) =>
        left.ToUniversalTime().Ticks / TimeSpan.TicksPerSecond
        == right.ToUniversalTime().Ticks / TimeSpan.TicksPerSecond;

    private static double LeastSquaresSlopePerDay((DateTimeOffset Time, double Value)[] points)
    {
        var origin = points[0].Time;
        var xs = points.Select(item => (item.Time - origin).TotalDays).ToArray();
        var meanX = xs.Average();
        var meanY = points.Average(item => item.Value);
        var denominator = xs.Sum(x => (x - meanX) * (x - meanX));
        if (denominator <= 0)
        {
            return 0;
        }

        var numerator = 0.0;
        for (var i = 0; i < xs.Length; i++)
        {
            numerator += (xs[i] - meanX) * (points[i].Value - meanY);
        }

        return numerator / denominator;
    }
}

/// <summary>
/// ランタイム比較用の定常判定。直近 window の間、負荷の振れ幅が tolerance 以内のときだけ
/// 値を通し、それ以外は最後の定常値で RuntimeRemaining/LoadPercent を置き換える。
/// 1デバイス1インスタンス。スレッドセーフではない。
/// </summary>
public sealed class RuntimeSteadyStateFilter
{
    private readonly TimeSpan _window;
    private readonly double _tolerancePercent;
    private readonly List<(DateTimeOffset Time, double Load)> _history = [];
    private ValidatedTelemetryValue<TimeSpan>? _lastRuntime;
    private ValidatedTelemetryValue<double>? _lastLoad;

    public RuntimeSteadyStateFilter(double windowSeconds = 60, double tolerancePercent = 3)
    {
        _window = TimeSpan.FromSeconds(windowSeconds);
        _tolerancePercent = tolerancePercent;
    }

    public UpsTelemetry Apply(UpsTelemetry telemetry)
    {
        var steady = false;
        if (telemetry.LoadPercent.IsValid)
        {
            var now = telemetry.Timestamp;
            _history.Add((now, telemetry.LoadPercent.Value!.Value));
            var cutoff = now - _window;
            // 窓の開始時点を表す1点(cutoff 以前で最新)を残し、それより古い分を捨てる。
            while (_history.Count > 1 && _history[1].Time <= cutoff)
            {
                _history.RemoveAt(0);
            }

            steady = _history[0].Time <= cutoff
                && _history.Max(item => item.Load) - _history.Min(item => item.Load) <= _tolerancePercent;
        }
        else
        {
            _history.Clear();
        }

        if (steady)
        {
            if (telemetry.RuntimeRemaining.IsValid)
            {
                _lastRuntime = telemetry.RuntimeRemaining;
                _lastLoad = telemetry.LoadPercent;
            }

            return telemetry;
        }

        return _lastRuntime is { } runtime && _lastLoad is { } load
            ? telemetry with { RuntimeRemaining = runtime, LoadPercent = load }
            : telemetry with { RuntimeRemaining = ValidatedTelemetryValue<TimeSpan>.Missing() };
    }
}

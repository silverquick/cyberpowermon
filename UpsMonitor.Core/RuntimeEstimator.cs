namespace UpsMonitor.Core;

public sealed record RuntimeEstimateTableItem(
    double LoadWatts,
    TimeSpan EstimatedRuntime,
    double LoadPercent,
    double EstimatedDischargeCurrentAmperes);

public static class RuntimeEstimator
{
    private const double DefaultInverterEfficiency = 0.88;
    private const double PeukertExponent = 1.15; // 標準的な鉛蓄電池の放電係数

    public static TimeSpan EstimateRuntime(
        double targetLoadWatts,
        double batteryPercent,
        double? sohPercent = null,
        double? nominalBatteryVoltage = null,
        double? batteryCapacityAh = null,
        TimeSpan? baselineRuntimeAtCurrentLoad = null,
        double? currentActiveLoadWatts = null,
        double? ratedActivePowerWatts = null)
    {
        if (targetLoadWatts <= 0)
        {
            return TimeSpan.FromHours(24);
        }

        var effectiveChargePercent = Math.Clamp(batteryPercent, 0.0, 100.0) / 100.0;
        var effectiveSoh = Math.Clamp(sohPercent ?? 100.0, 10.0, 100.0) / 100.0;

        // 1. 実測の現在の負荷と残り時間（RuntimeRemaining）が利用可能な場合、その基準点を用いてPeukert則で補正
        if (baselineRuntimeAtCurrentLoad.HasValue
            && baselineRuntimeAtCurrentLoad.Value > TimeSpan.Zero
            && currentActiveLoadWatts is > 5.0)
        {
            var baseMinutes = baselineRuntimeAtCurrentLoad.Value.TotalMinutes;
            var baseLoad = currentActiveLoadWatts.Value;

            // T2 = T1 * (P1 / P2) ^ k
            var ratio = baseLoad / targetLoadWatts;
            var estimatedMinutes = baseMinutes * Math.Pow(ratio, PeukertExponent);
            estimatedMinutes = Math.Max(0.5, estimatedMinutes);
            return TimeSpan.FromMinutes(estimatedMinutes);
        }

        // 2. バッテリーの公称電圧と容量(Ah)から物理計算
        var voltage = nominalBatteryVoltage is > 0 ? nominalBatteryVoltage.Value : 24.0;
        // 定格容量が未指定の場合、定格電力(W)や電圧から一般的なUPSバッテリー容量を推定 (例: 1200VA/780W = 24V 9Ah x 2 など)
        var capacityAh = batteryCapacityAh is > 0
            ? batteryCapacityAh.Value
            : (ratedActivePowerWatts is > 0 ? (ratedActivePowerWatts.Value / voltage) * 0.28 : 9.0);

        var totalBatteryEnergyWattHours = voltage * capacityAh * effectiveSoh * effectiveChargePercent;
        var drawFromBatteryWatts = (targetLoadWatts / DefaultInverterEfficiency) + 8.0; // 自家消費電力約8Wを加算

        var hours = totalBatteryEnergyWattHours / drawFromBatteryWatts;
        var minutes = hours * 60.0;

        // 大放電時のPeukert低下補正
        var cRate = (drawFromBatteryWatts / voltage) / capacityAh;
        if (cRate > 1.0)
        {
            minutes /= Math.Pow(cRate, 0.15);
        }

        minutes = Math.Clamp(minutes, 0.1, 1440.0);
        return TimeSpan.FromMinutes(minutes);
    }

    public static IReadOnlyList<RuntimeEstimateTableItem> GenerateStandardLoadEstimates(
        double batteryPercent,
        double? sohPercent,
        double? nominalBatteryVoltage,
        double? ratedActivePowerWatts,
        TimeSpan? currentRuntime = null,
        double? currentLoadWatts = null)
    {
        var ratedPower = ratedActivePowerWatts is > 0 ? ratedActivePowerWatts.Value : 780.0;
        var voltage = nominalBatteryVoltage is > 0 ? nominalBatteryVoltage.Value : 24.0;

        double[] sampleLoads = [50, 100, 200, 300, 400, 500, 600, 700, 800];
        var list = new List<RuntimeEstimateTableItem>();

        foreach (var load in sampleLoads)
        {
            if (load > ratedPower * 1.15)
            {
                continue;
            }

            var runtime = EstimateRuntime(
                targetLoadWatts: load,
                batteryPercent: batteryPercent,
                sohPercent: sohPercent,
                nominalBatteryVoltage: voltage,
                ratedActivePowerWatts: ratedPower,
                baselineRuntimeAtCurrentLoad: currentRuntime,
                currentActiveLoadWatts: currentLoadWatts);

            var loadPercent = Math.Clamp((load / ratedPower) * 100.0, 0.0, 150.0);
            var dischargeCurrent = (load / DefaultInverterEfficiency) / voltage;

            list.Add(new RuntimeEstimateTableItem(load, runtime, loadPercent, dischargeCurrent));
        }

        return list;
    }
}

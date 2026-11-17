using UpsMonitor.Core;

internal static class OfficialGroupingTests
{
    private static readonly DateTimeOffset Start = new(2024, 1, 1, 23, 30, 0, TimeSpan.Zero);
    private static OfficialHealthRecord R(double hours, double value = 90) =>
        new() { RecordedAt = Start.AddHours(hours), HealthPercent = value };
    private static void Check(bool condition) { if (!condition) throw new Exception("Grouping assertion failed"); }
    public static void Boundaries()
    {
        Check(OfficialHealthEstimator.GroupRecords([R(0), R(24)]).Length == 1);
        Check(OfficialHealthEstimator.GroupRecords([R(0), R(24 + 1.0 / 3600)]).Length == 2);
        var chain = OfficialHealthEstimator.GroupRecords([R(0), R(20), R(40)]);
        Check(chain.Length == 2 && chain[0].RecordedAt == Start.AddHours(20));
        Check(OfficialHealthEstimator.GroupRecords([R(0), R(1)]).Length == 1); // UTC midnight
        var offset = R(1); offset.RecordedAt = offset.RecordedAt.ToOffset(TimeSpan.FromHours(9));
        Check(OfficialHealthEstimator.GroupRecords([R(0), offset]).Length == 1);
    }
    public static void OrderAndDuplicates()
    {
        var a = R(0, 95); var b = R(0, 88);
        var records = new[] { a, b };
        Check(ReferenceEquals(OfficialHealthEstimator.GroupRecords(records).Single(), b));
        var estimate = OfficialHealthEstimator.Estimate(records, Start.AddDays(-100), Start.AddDays(1))!;
        Check(estimate.RecordCount == 2 && estimate.EffectiveRecordCount == 1);
        Check(estimate.Confidence == BatteryHealthConfidence.Medium);
        Check(estimate.Models.All(m => m.BacktestCount == 0));
        var sorted = new[] { R(0,95), R(20,94), R(40,93), R(100,92) };
        var shuffled = new[] { sorted[3], sorted[1], sorted[0], sorted[2] };
        Check(OfficialHealthEstimator.GroupRecords(sorted).SequenceEqual(OfficialHealthEstimator.GroupRecords(shuffled)));
        Check(OfficialHealthEstimator.Estimate(sorted, null, Start.AddDays(10))!.Percent ==
              OfficialHealthEstimator.Estimate(shuffled, null, Start.AddDays(10))!.Percent);
    }
    public static void PastPredictionAndSnapshots()
    {
        var old = R(0,95); old.PredictedPercent = 97;
        old.ModelPredictions = new() { [OfficialHealthModel.RecentLinear] = 96 };
        var future = R(20,10);
        var before = OfficialHealthEstimator.Predict([old], Start.AddDays(-100), Start.AddHours(10))!;
        var after = OfficialHealthEstimator.Predict([old,future], Start.AddDays(-100), Start.AddHours(10))!;
        Check(before.Percent == after.Percent && after.EffectiveRecordCount == 1);
        var e = OfficialHealthEstimator.Estimate([old,future,R(40,80)], Start.AddDays(-100), Start.AddDays(5))!;
        var bt = OfficialHealthEstimator.BacktestModels([old,future,R(40,80)],Start.AddDays(-100));
        Check(e.Models.Zip(bt).All(p => p.First.BacktestCount == p.Second.BacktestCount && p.First.MeanAbsoluteError == p.Second.MeanAbsoluteError));
        Check(e.Models[0].BacktestCount == 1);
        var representativeOnly = OfficialHealthEstimator.BacktestModels([future,R(40,80)],Start.AddDays(-100));
        // Representatives only must NOT be regrouped inside the backtest: 20h and 40h belong to separate original groups.
        Check(representativeOnly[0].BacktestCount == 0);
        Check(old.PredictedPercent == 97 && old.ModelPredictions[OfficialHealthModel.RecentLinear] == 96);
        Check(OfficialHealthEstimator.MeanAbsoluteError([old,future]) == 2);
    }
    private static OfficialHealthRecord[] Series(bool power)
    {
        var install = Start.AddHours(-23.5);
        return Enumerable.Range(1,52).Select(i => {
            var day = i * 14;
            return new OfficialHealthRecord {
                RecordedAt = install.AddDays(day),
                HealthPercent = Math.Round(100 - (power ? 8 * Math.Pow(day / 365.0, 1.6) : 12 * day / 365.0)
                    + .8 * Math.Sin(day / 55.0) + .25 * Math.Cos(day / 23.0))
            };
        }).ToArray();
    }
    public static void SparseExactRegression()
    {
        // Full precision golden values captured from unmodified b288edd Debug Core.
        var expected = new[] {
            new[] {75.92464678178963,76.00872308284707,75.9819338298091,.5081464221399142,.4988458572660223,.5467263607824902},
            new[] {75.59759288330717,76.16307277628033,75.3819081803991,.4361323511722979,.4392629041592196,.46428941958139547}
        };
        for (var k=0;k<2;k++) {
            var install = Start.AddHours(-23.5);
            var records = Series(k==1);
            var e = OfficialHealthEstimator.Estimate(records,install,install.AddDays(758))!;
            Check(e.RecordCount == 52 && e.EffectiveRecordCount == 52);
            Check(e.Model == (k==0 ? OfficialHealthModel.InstallAnchoredLinear : OfficialHealthModel.RecentLinear));
            var bt = OfficialHealthEstimator.BacktestModels(records,install);
            for(var j=0;j<3;j++) {
                Check(e.Models[j].Percent == expected[k][j]);
                Check(e.Models[j].MeanAbsoluteError == expected[k][j+3]);
                Check(bt[j].MeanAbsoluteError == expected[k][j+3]);
                Check(bt[j].BacktestCount == (j==2 ? 50 : 51));
            }
        }
    }
    public static void DenseSeries()
    {
        foreach(var power in new[]{false,true}) {
            var install=Start.AddHours(-23.5);
            var regular=Series(power);
            var clustered=regular.Concat(Enumerable.Range(1,8).Select(h=>new OfficialHealthRecord {
                RecordedAt=install.AddDays(560).AddHours(h),
                HealthPercent=regular[39].HealthPercent
            })).ToArray();
            var a=OfficialHealthEstimator.Estimate(regular,install,install.AddDays(758))!;
            var c=OfficialHealthEstimator.Estimate(clustered,install,install.AddDays(758))!;
            Check(c.RecordCount==60 && c.EffectiveRecordCount==52 && c.Model==a.Model);
            // Report B24: estimate differences < .000026pt, MAE differences < .00044pt.
            Check(Math.Abs(a.Percent-c.Percent)<.00003);
            foreach(var pair in a.Models.Zip(c.Models)) {
                Check(pair.First.BacktestCount==pair.Second.BacktestCount);
                Check(Math.Abs(pair.First.MeanAbsoluteError!.Value-pair.Second.MeanAbsoluteError!.Value)<.001);
            }
            var duplicates=regular.Concat(Enumerable.Repeat(regular[39],8)).ToArray();
            var d=OfficialHealthEstimator.Estimate(duplicates,install,install.AddDays(758))!;
            Check(d.Percent==a.Percent && d.Model==a.Model);
            Check(a.Models.SequenceEqual(d.Models));
        }
    }
}

# UPS Monitor

Windows 11 x64 向けの読み取り専用 UPS 監視アプリです。.NET 10 / C# / WPF / MVVM で構成し、PowerPanel やサードパーティ USB ライブラリを介さず、Windows 標準 HID / SetupAPI を P/Invoke して USB HID Power Device を読み取ります。

Version 0.1 は監視・状態表示・イベント検出・ローカル履歴・ファイルログまでを対象とし、UPS や Windows に対するシャットダウン操作は一切実行しません。HID Output report、`HidD_SetFeature`、電源制御 API も呼び出しません。

## 現在の機能

- HID top-level collection の Usage Page `0x84` / Usage `0x04` を基準に UPS を検出
- VID、PID、Manufacturer、Product、Serial Number、Device Path、Usage、report length を取得
- `HidD_GetPreparsedData`、`HidP_GetCaps`、`HidP_GetValueCaps`、`HidP_GetButtonCaps` で descriptor を解析
- Feature report と Interrupt IN report を Report ID / Usage 単位で解析（モデル固有のバイトオフセットなし）
- AC、Charging、Discharging、Battery、Runtime、Low Battery、Shutdown Imminent、Overload を独立して取得
- 入出力電圧、バッテリー電圧、負荷率、有効電力、皮相電力、定格値、切替電圧など、descriptor が公開する全82項目を保持・表示
- 入力値の妥当性検証と、物理容量または同等負荷のランタイム基準による説明可能なバッテリーSOH推定
- 新品時の絶対基準、現在値からの相対推移、CyberPower BHI等の既知値アンカーを区別して保存
- 健全性の算出方法・信頼度・根拠と、BHIから独立した交換判定を表示
- `Unknown / Online / OnBattery / LowBattery / Critical` の状態判定
- PowerLost、PowerRestored、BatteryLow、BatteryCritical、RuntimeLow、OverloadDetected、UpsDisconnected、UpsReconnected イベント
- 1秒周期のバックグラウンド監視、read error 後の再列挙、`WM_DEVICECHANGE` による即時再スキャン
- Windows 11 Fluent風の Dashboard、History、UPS、Devices、Actions、Logs、Settings UI
- ローカルSQLiteへ1秒テレメトリ、HID数値、状態遷移、イベント、健全性履歴を保存
- 1時間／6時間／24時間／7日／30日のグラフ、Dashboardの小型トレンド、状態タイムライン
- ライト／ダークテーマ、角丸カード、細いFluent風スクロールバー
- 日本語／英語を設定画面から即時切り替え、選択言語を `config.json` に保存
- `%ProgramData%\UpsMonitor\config.json`、`telemetry.db` と日別イベントログ

未公開の Usage は `N/A` のまま表示し、他の Usage の監視は継続します。HID詳細画面では標準項目だけでなくベンダー定義Usageも生値・Report ID・Collection Path・論理/物理範囲・Unit・ビット配置とともに確認できます。

## プロジェクト構成

```text
UpsMonitor.Core            Snapshot / State / Event / Rule contracts / Monitor engine
UpsMonitor.Hid             HID / SetupAPI P/Invoke、descriptor/report parser、UPS mapper
UpsMonitor.Infrastructure  JSON configuration、file event log、local SQLite telemetry store
UpsMonitor.App             WPF MVVM UI、WM_DEVICECHANGE bridge
UpsMonitor.Probe           実機の descriptor と現在値を確認する console tool
UpsMonitor.Core.Tests      NuGet test framework を使わない core self-tests
```

依存方向は次のとおりです。

```text
Windows USB HID
      ↓
UpsMonitor.Hid → IUpsProvider
      ↓
UpsSnapshot → UpsPowerState / UpsEvent
      ↓
UpsMonitorEngine
      ↓
WPF ViewModel
```

UI は Win32/HID API を参照せず、`UpsSnapshot` とイベントだけを受け取ります。将来の Windows Service は `IUpsProvider` と `UpsMonitorEngine` をホストし、同じ snapshot/event を Named Pipe DTO として GUI に渡せます。

## ビルドと起動

前提は Windows 11 x64 と .NET 10 SDK です。SQLiteアクセスにはMicrosoft公式の `Microsoft.Data.Sqlite` を使用します。

```powershell
dotnet build UpsMonitor.sln
dotnet run --project .\UpsMonitor.App\UpsMonitor.App.csproj
```

実機の列挙と現在値だけを確認する場合:

```powershell
dotnet run --project .\UpsMonitor.Probe\UpsMonitor.Probe.csproj
```

descriptor の全 Power Device / Battery System item も表示する場合:

```powershell
dotnet run --project .\UpsMonitor.Probe\UpsMonitor.Probe.csproj -- --descriptor
```

core self-tests:

```powershell
dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj
```

## 設定とログ

初回起動時に次のファイルを作成します。

```text
C:\ProgramData\UpsMonitor\
  config.json
  telemetry.db
  logs\ups-YYYY-MM-DD.log
```

`telemetry.db` はこのPC内だけで使うローカルSQLiteデータベースです。クラウドや外部DBへの送信は行いません。実行中はSQLiteのWAL用に `telemetry.db-wal` と `telemetry.db-shm` が同じ場所へ作成されることがあります。

1秒ごとの主要値と変化時／5分ごとの全数値HID Usageを14日間保存し、同時に1分単位の最小・平均・最大値を長期集計として保持します。状態遷移、UPSイベント、バッテリー健全性の観測履歴も同じDBへ保存します。割合グラフ（充電率、負荷率、健全性）は常に0～100%固定で、電圧、ランタイム、W/VAは値に応じた軸を使います。

設定例は [`config.example.json`](config.example.json) にあります。`history.rawRetentionDays` で生データ保持日数、`history.rawUsageCheckpointSeconds` で全HID数値の定期記録間隔を変更できます。`shutdownPolicies` は将来互換のデータ形状だけで、v0.1 では読み込まれても実行されません。通常ユーザーで ProgramData の作成権限がない配布環境では、インストーラー側で `C:\ProgramData\UpsMonitor` と適切な ACL を作成してください。

## CP1200PFCLCD JP 実機確認

2026-08-22 に接続中の CP1200PFCLCD JP で、Windows 標準 HID ドライバのまま次を確認しています。

```text
Product      : CP1200PFCLCD JP
VID / PID    : 0764 / 0601
Usage        : 0x84 / 0x04
Report bytes : input=64, feature=64
Battery      : 100%
Runtime      : HID Unit(seconds) として取得
AC present   : True
```

実際の report descriptor が公開する項目だけを使うため、この個体で descriptor に存在しない `ShutdownImminent` などは `N/A` になります。

### バッテリー健全性について

PowerPanel Personal の Battery Health Indicator（例: 59%）は、このUPSがUSB HIDで直接公開する値ではありません。PowerPanel同梱ヘルプでは放電状況と利用期間に基づくPowerPanel Cloudの推定値と説明されており、実機の標準Usage 82項目にも対応値はありません。そのため、このアプリは未根拠の数値を作らず、次の優先順位でSOHを算出します。

1. 制御ランタイム／放電エネルギー測定
2. 物理単位を持つ `FullChargeCapacity / DesignCapacity`
3. 満充電時かつ同等負荷における、保存済みランタイム基準との比較
4. 比較可能な測定値がなければ `N/A / データ不足`

CP1200PFCLCD JP が返す `DesignCapacity=100` と `FullChargeCapacity=100` は、物理容量ではなく0～100%の尺度なのでSOH計算から除外します。設定画面では、満充電時の現在ランタイムを次の3方式で記録できます。

- `新品／交換直後`: 現在値を絶対的なSOH 100%の基準にします。新品または交換直後だけに使用します。
- `現在値（相対推移のみ）`: 使用中のバッテリーでも設定できます。現在値を相対100%として以後の低下を表示しますが、絶対的なSOHは `N/A` のままです。
- `既知の健全性`: PowerPanelで確認したBHIなどを手入力します。例えば59%を記録した後は、`59 × 現在ランタイム / 基準ランタイム` を同等負荷で評価します。PowerPanelに表示された公式区分（Good / Average / Below Average / Poor）が分かる場合は併記できます。

絶対的な健全性と、記録時点からの相対推移はDashboardで別々に表示します。CyberPower BHIを標準HIDから取得したとは表示せず、保存した値の取得元（初期値は `CyberPower BHI`）も併記します。CyberPowerがBHIの数値に対応する交換しきい値を公表していないため、59%などのBHI値だけを「早めに交換」へ変換しません。

交換判定はBHIとは別に表示します。`NeedReplacement` は交換要求、セルフテスト失敗は要確認、物理容量・制御放電測定・新品時ランタイムが基準の80%未満なら交換検討、記録時点からの相対ランタイムが80%未満なら要確認とします。この80%は実測／基準性能の確認目安にだけ使い、CyberPower BHIには適用しません。

SOC、負荷、ランタイム等は範囲検証し、例えば120%の残量は100%へ丸めずInvalidとして除外します。`NeedReplacement` またはセルフテスト失敗が報告された場合は、計算上の割合より重大状態を優先します。

## v0.1 の境界

以下は意図的に未実装です。

- Windows Service / Named Pipe
- SSH、ローカル shutdown、外部 command、notification、webhook
- Rule Engine の評価・Action 実行
- Windows Event Log 出力
- 複数 UPS の選択 UI（現在は最初に開けた UPS を監視）
- Tray 常駐、installer、code signing

`RuleDefinition`、`RuleTriggerType`、`RuleConditionType`、`RuleActionType` は Core に契約だけ用意してあります。Service 化するときも WPF に特権を与えず、Action executor は Service 側だけに追加します。

## 仕様資料

- [USB-IF Usage Tables for HID Power Devices](https://www.usb.org/sites/default/files/pdcv10.pdf)
- [Microsoft: Introduction to HID Concepts](https://learn.microsoft.com/windows-hardware/drivers/hid/hid-concepts)
- [Microsoft: HIDClass Support Routines](https://learn.microsoft.com/windows-hardware/drivers/hid/hidclass-support-routines)

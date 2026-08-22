# UPS Monitor

Windows 11 x64 向けの読み取り専用 UPS 監視アプリです。.NET 10 / C# / WPF / MVVM で構成し、PowerPanel やサードパーティ USB ライブラリを介さず、Windows 標準 HID / SetupAPI を P/Invoke して USB HID Power Device を読み取ります。

Version 0.1 は監視・状態表示・イベント検出・ファイルログまでを対象とし、UPS や Windows に対するシャットダウン操作は一切実行しません。HID Output report、`HidD_SetFeature`、電源制御 API も呼び出しません。

## 現在の機能

- HID top-level collection の Usage Page `0x84` / Usage `0x04` を基準に UPS を検出
- VID、PID、Manufacturer、Product、Serial Number、Device Path、Usage、report length を取得
- `HidD_GetPreparsedData`、`HidP_GetCaps`、`HidP_GetValueCaps`、`HidP_GetButtonCaps` で descriptor を解析
- Feature report と Interrupt IN report を Report ID / Usage 単位で解析（モデル固有のバイトオフセットなし）
- AC、Charging、Discharging、Battery、Runtime、Low Battery、Shutdown Imminent、Overload を独立して取得
- 入出力電圧、バッテリー電圧、負荷率、有効電力、皮相電力、定格値、切替電圧など、descriptor が公開する全82項目を保持・表示
- `Unknown / Online / OnBattery / LowBattery / Critical` の状態判定
- PowerLost、PowerRestored、BatteryLow、BatteryCritical、RuntimeLow、OverloadDetected、UpsDisconnected、UpsReconnected イベント
- 1秒周期のバックグラウンド監視、read error 後の再列挙、`WM_DEVICECHANGE` による即時再スキャン
- Windows 11 Fluent風の Dashboard、UPS、Devices、Actions、Logs、Settings UI
- ライト／ダークテーマ、角丸カード、細いFluent風スクロールバー
- 日本語／英語を設定画面から即時切り替え、選択言語を `config.json` に保存
- `%ProgramData%\UpsMonitor\config.json` と日別イベントログ

未公開の Usage は `N/A` のまま表示し、他の Usage の監視は継続します。HID詳細画面では標準項目だけでなくベンダー定義Usageも生値・Report ID・Collection Path・論理/物理範囲・Unit・ビット配置とともに確認できます。

## プロジェクト構成

```text
UpsMonitor.Core            Snapshot / State / Event / Rule contracts / Monitor engine
UpsMonitor.Hid             HID / SetupAPI P/Invoke、descriptor/report parser、UPS mapper
UpsMonitor.Infrastructure  JSON configuration、file event log
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

前提は Windows 11 x64 と .NET 10 SDK です。追加 workload や NuGet パッケージは不要です。

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
  logs\ups-YYYY-MM-DD.log
```

設定例は [`config.example.json`](config.example.json) にあります。`shutdownPolicies` は将来互換のデータ形状だけで、v0.1 では読み込まれても実行されません。通常ユーザーで ProgramData の作成権限がない配布環境では、インストーラー側で `C:\ProgramData\UpsMonitor` と適切な ACL を作成してください。

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

PowerPanel Personal の Battery Health Indicator（例: 59%）は、このUPSがUSB HIDで公開する値ではありません。PowerPanel同梱ヘルプでは放電状況と利用期間に基づくPowerPanel Cloudの推定値と説明されており、実機の標準Usage 82項目にも対応値はありません。近い生値を健全性として誤認しないよう、現時点ではGUIに重要指標の表示枠を設けたうえで `N/A / USB HIDでは非公開` と明示します。

将来このアプリ自身で設置日・放電深度・放電回数・セルフテスト結果を蓄積できれば、PowerPanelに依存しない独立した交換時期指標を追加できます。

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

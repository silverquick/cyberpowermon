# CLAUDE.md

AIエージェント向けの作業手引き。ここに書いた内容は実コードで確認したもの。

## プロジェクト概要

UPS Monitor (PowerGuard) は、Windows 11 x64 向けの USB HID UPS 監視アプリ（.NET 10 / C# / WPF / MVVM）。
Windows 標準の HID / SetupAPI を P/Invoke して UPS を読み取り、状態判定・イベント検出・SQLite 履歴・分析・通知を行う。
詳細な機能と仕様は `README.md` を参照。

## プロジェクト構成

| プロジェクト | 役割 |
| --- | --- |
| `UpsMonitor.App` | WPF UI（`MainWindow.xaml`、`MainViewModel.cs`、`LogsViewModel.cs`、`TrayIconManager.cs`、`MiniMonitorWindow`）。多言語リソースは `Resources/Strings.*.xaml` |
| `UpsMonitor.Core` | Snapshot・状態・イベント検出・監視エンジン（`Monitoring.cs`）、バッテリー健全性（`BatteryHealth.cs`、`OfficialHealthEstimator.cs`）、履歴の型（`TelemetryHistory.cs`） |
| `UpsMonitor.Core.Tests` | 独自ランナーのテスト（`Program.cs` と保存済みログのテスト（`SavedEventTests.cs`、日英リソース用 adapter）。Logs VM はソースリンクで検証）。NuGet のテストフレームワークは使わない |
| `UpsMonitor.Hid` | HID / SetupAPI の P/Invoke（`HidNative.cs`）、descriptor・report の解析、UPS へのマッピング（`WindowsHidUpsProvider.cs`） |
| `UpsMonitor.Infrastructure` | 設定（`AppConfiguration.cs`、`JsonConfigurationStore.cs`）、パス解決（`AppPaths.cs`）、SQLite 履歴（`SqliteTelemetry*.cs`）、エクスポート、Webhook、外部コマンド、ファイルログ、スタートアップ登録 |
| `UpsMonitor.Probe` | 実機の descriptor と現在値を表示するコンソールツール |

## ビルドとテスト

```powershell
dotnet build UpsMonitor.sln                              # Debug ビルド
dotnet run --project UpsMonitor.Core.Tests               # 独自ランナー。現在 72/72 件通過（2026-11-13 確認）
dotnet run --project .\UpsMonitor.App\UpsMonitor.App.csproj
```

- ネットワークが遮断されたサンドボックスで NuGet 脆弱性監査（NU1900）により restore が失敗する場合に限り、`-p:NuGetAudit=false` を付けてよい。
- `dotnet test` ではなく `dotnet run` でテストを実行する。末尾に `N/N tests passed.` が出る。
- ビルド警告を増やさないこと。変更前後で警告数を比較する。
- テストは合成データ（一時ディレクトリの SQLite など）だけを使う。実 DB・実 config を使うテストは書かない。

## 設計上の注意

- `UpsMonitor.App/MainViewModel.cs` は約 3,300 行と巨大（監視表示・履歴・Analytics・Logs・設定・export・健全性記録を一括）。
  変更は小さく局所的に行い、無関係な整理を混ぜない。分割の検討状況は `docs/improvement_backlog.md` の #7 を参照。
- 旧 `DailyEnergyReports`（`MainViewModel.cs:423` 付近）は XAML 未バインドのまま残っている（backlog の整理事項）。
- HID は読み取り専用。`HidNative.cs` が使うのは `HidD_GetFeature` などの読み取り系で、Output report・`HidD_SetFeature` は使わない。追加しない。
- アプリ自身は OS のシャットダウンや UPS への書き込みを行わない。「シャットダウン自動化」パネルと Actions タブは表示・プレビューのみ。
  ただし外部コマンド連携（`CommandRunner.cs`）はユーザー指定のコマンドを `cmd.exe /c` で実行する（既定は無効）。
- SQLite 履歴 DB は `Microsoft.Data.Sqlite`（WAL）。共有キャッシュ（`Cache=Shared`）は廃止済み（`d6d9f5d`）。接続文字列に戻さない。
- 多言語: UI 文字列は `Resources/Strings.ja-JP.xaml` と `Resources/Strings.en-US.xaml` の両方に同じキーで追加する。
  存在しないキーを `DynamicResource` で参照するとエラーにならず空表示になる（`f7f1055` の原因）ので、キー名を必ず確認する。
- `config.json` は後方互換を保つ（新規項目には既定値を持たせる）。設定の例は `config.example.json` を参照。
- 設計・作業の過去記録は `docs/` 配下。改善施策の正本は `docs/improvement_backlog.md`。

## git 運用

- main で直接作業しない。`feature/...` や `fix/...` のブランチを切ってコミットし、main へ `--no-ff` でマージする。
  マージコミットのメッセージは `Merge: <日本語の要約>`。
- docs だけの変更も `docs/...` ブランチで作業し、`--no-ff` でマージする。バージョン番号は上げない。
- コミットメッセージは Conventional Commits 形式で、本文は日本語（例: `fix(ui): ...`、`feat(health): ...`）。
- 実態の確認（`git log --graph --oneline -30`）: 近年の変更は feature/fix ブランチ → `--no-ff` マージ（`Merge: ...`）になっている。
  古いマージは `Merge #1: ...` や `Merge branch '...'` 形式、docs のみの更新は main へ直接コミットされた例（`80044e9`）がある。上記の運用に従う。
- プッシュは `git push origin main`。非対話環境で Git Credential Manager が失敗する場合は次を使う（git の設定自体は変更しない）。

```powershell
git -c credential.helper= -c "credential.helper=!gh auth git-credential" push origin main
```

## バージョン管理

- 番号は `Directory.Build.props` の `PowerGuardPatch` で一元管理する。表示形式は `0.1.XXX`（3 桁ゼロ埋め）。
- feature/fix ブランチを main にマージするたびに +1 する。マージ前にブランチ内で `PowerGuardPatch` を +1 し、`CHANGELOG.md` の先頭に `## 0.1.XXX — YYYY-MM-DD` とブランチ名・日本語要約を追加する。
- 0.1.017 以降の CHANGELOG にはコミット hash ではなくブランチ名を記載する。マージコミットは承認後の `v0.1.XXX` タグまたは `git log --merges --grep` で辿る。
- docs だけの変更では番号を上げない。999 を超える場合は、その時点でユーザーに確認する。
- マージ後のタグ付けと push はユーザー承認事項。承認後に `git tag -a v0.1.XXX -m "v0.1.XXX" <マージコミット>`、続けて `git push origin v0.1.XXX` を行う（`--tags` は使わない）。push は git 運用節の gh 認証 helper を使ってもよい。

## データの扱い（重要）

- 実運用の履歴 DB（UPS テレメトリの SQLite）と実 config は**機密扱い**。エージェントは読み取りも変更もしない。
  - 既定パス（`UpsMonitor.Infrastructure/AppPaths.cs`、`Environment.SpecialFolder.CommonApplicationData` + `UpsMonitor`）:
    - 履歴 DB: `C:\ProgramData\UpsMonitor\telemetry.db`（WAL 用に `-wal` / `-shm` も同じ場所）
    - config: `C:\ProgramData\UpsMonitor\config.json`
    - ログ: `C:\ProgramData\UpsMonitor\logs\`
    - `AppPaths.DevicesFile`（`devices.json`）と `AppPaths.UserDirectory`（`%LOCALAPPDATA%\UpsMonitor`）は初期コミット `342d350` から定義だけあり、読み書きするコードは一度も存在しない
  - 上記パスの中身は開かない。リポジトリ直下の `error.log` も開かない。
- Antigravity など、非承認の外部エージェントには実 DB へアクセスさせない。
- 動作確認が必要なときは、`AppPaths` のコンストラクタ引数や一時ディレクトリで合成データを作る（既存テストの方式に倣う）。
- 設定の例は `config.example.json` を使う。

## ドキュメント

- `README.md`: 利用者向けの機能・ビルド・設定の説明。機能を変えたら更新する。
- `CHANGELOG.md`: バージョンごとの変更履歴（`0.1.XXX`）。
- `AGENTS.md`: エージェント向けに CLAUDE.md の参照と重要規則を案内する。
- `docs/improvement_backlog.md`: 改善施策の正本（実装状況・未着手・見送り理由）。
- `docs/` 配下の施策別フォルダ: 過去の設計・作業記録（`implementation_plan.md` / `task.md` / `walkthrough.md`）。現状と食い違うことがあるので、コードを正とする。

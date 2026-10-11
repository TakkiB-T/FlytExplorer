# Paste this into Claude Code in the FlytExplorer repository root

FlytExplorer の Phase 0 を開始してください。これは Windows 11 x64 用の Nordic Dark な独立型ファイルマネージャーで、UI は WinUI 3 / XAML、C# / .NET 10、検索はインストール済み Everything の SDK / IPC に正式依存し、独自検索エンジンは作成しません。

最初に `CLAUDE.md`, `docs/ARCHITECTURE.md`, `docs/PHASE_0.md`, `docs/PERFORMANCE_AND_TESTS.md` をすべて読み、リポジトリ状態と Windows 開発環境を確認してください。

**まず状況確認と短い作業計画を提示**し、その後 Phase 0 の PoC を順次実装してください。実装順は (1) WinUI 3 の Release x64 ビルドとウィンドウ起動、(2) テスト用ローカルフォルダーの非同期列挙と仮想化一覧、(3) Everything SDK の IPC検索・接続失敗表示・結果のページ取得、(4) STA スレッドでの `IFileOperation` による安全なテストファイル操作、(5) Explorer との性能比較・テスト・リスク記録です。

ルール:
- Windows 標準の Explorer の置き換え・Shell既定設定変更は禁止。
- 実ユーザーファイル・実NASへの変更や削除は禁止。破壊的テストは専用 fixture フォルダーだけ。
- システム全体のインストールや管理者権限の変更は事前確認。
- ビルド可能な最小実装を重視。未実装のPhase 1以降へ勝手に進まない。
- 実測データなしに性能達成を報告しない。実行できなかったテストも明記。
- ライブラリ・SDKの互換性やネイティブ関数名は公式仕様で確認する。
- 実行したコマンド、変更ファイル、成功/失敗、残課題を最後に報告する。

まず開発環境の確認結果と Phase 0 の実装計画を示してください。

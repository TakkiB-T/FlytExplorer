FlytExplorer v0.16.0 — インストール手順
========================================

動作環境
--------
- Windows 11 x64 (22H2 以降)
- .NET 10 Desktop Runtime (x64)
- Windows App SDK 2.5 Runtime

【方法 A】setup.ps1 で自動セットアップ（推奨）
-----------------------------------------------
1. このフォルダを展開する
2. PowerShell を右クリック → 管理者として実行
3. 以下を実行:

   Set-ExecutionPolicy -Scope Process Bypass
   .\setup.ps1

   ※ .NET 10 が未インストールの場合、ダウンロードページが開きます。
   ※ Windows App SDK が未インストールの場合、自動でダウンロード・インストールします。

4. 前提条件のインストール後、FlytExplorer が起動します。
   以降は FlytExplorer.App.exe を直接ダブルクリックで起動できます。

【方法 B】手動セットアップ
---------------------------
1. .NET 10 Desktop Runtime (x64) をインストール:
   https://dotnet.microsoft.com/download/dotnet/10.0
   → 「.NET Desktop Runtime 10.x.x (x64)」を選択

2. Windows App SDK 2.5 Runtime をインストール:
   https://aka.ms/windowsappsdk/2.5/latest/windowsappruntimeinstall-x64.exe
   → ダウンロードして実行

3. FlytExplorer.App.exe をダブルクリックで起動

よくある問題
------------
Q: 起動時にエラーダイアログが出る
A: Windows App SDK Runtime が正しくインストールされているか確認してください。
   setup.ps1 を再実行すると自動判定します。

Q: 「このアプリはこの PC では実行できません」と表示される
A: x64 版 Windows 11 でのみ動作します。ARM/32bit 環境は非対応です。

Q: ウイルス対策ソフトが反応する
A: このビルドはコード署名なし（開発用ビルド）のため、
   SmartScreen や AV ソフトが警告を出す場合があります。
   「詳細情報」→「実行」を選択してください。

フィードバック
--------------
テスト結果は開発者まで共有してください:
- 起動可否
- ナビゲーション動作
- ファイル操作（コピー/削除/リネーム）
- 表示スケール（DPI 設定）
- テーマ切替（OS の色設定を変更）

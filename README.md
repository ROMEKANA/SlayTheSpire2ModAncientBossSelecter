# SelectMOD 制作メモ

自分用の日本語メモ。公開用の紹介文ではなく、現状・決めたこと・動かし方・次にやることをここに残す。

最終更新: 2026-11-18（日本時間）。現在は**設計と調査準備の段階**。MOD本体は未実装。

## 作りたいもの

Slay the Spire 2で、Actごとのエンシェントとボスを指定／Ban／候補制限し、アセンション効果を個別にON/OFFできるMOD。例えば「A1はOFF、A2はON」を可能にする。

- BaseLib・RitsuLib・ModConfigなど、別MODへの依存なし。ゲーム標準ローダーと同梱Harmonyを使う。
- マルチ対応を目標に、ホストが設定を編集し、全員が同じラン設定を使う設計にする。
- マルチでは全員が対応するSelectMODを導入する前提。
- セーブ再開では、そのランの保存済み設定と確定済みの出現結果を使う。

「SelectMOD」は作業名。DLL名・MOD ID・バージョン・対応ゲーム版は実装時に決める。

## ソロから作るか

**機能の動作確認はソロから進める。設定と保存の構造は最初からマルチを見据えて作る。** ソロなら抽選・表示・A効果の問題を切り分けやすい。一方、ソロ完成後まで同期の調査を遅らせると、保存方法や設定の持ち方を作り直す可能性がある。

順番は「現行DLL調査 → 最小限の設定保存・同期試作 → ソロで機能実装・再開確認 → マルチで機能確認」。ソロ試作段階ではマルチ使用を明示的に止め、同期が未完成のまま有効化しない。ソロ専用MODとして完成させてから別物に改造する方針にはしない。

初版の入口は標準のカスタムランを第一候補にする。通常ラン・デイリーは追加検討。標準Modifierの設定・保存・同期を優先し、不足する保証だけ追加する。詳細は[初期設計](DESIGN_AND_LESSONS.md)を参照。

## 今の状態

| 項目 | 状態 |
| --- | --- |
| 開発ルール・初期設計・既存技術調査 | あり。現行ゲームDLLでの検証はこれから |
| ゲーム調査スクリプト | scripts/Export-GameSources.ps1を作成 |
| 共通パス処理・ビルド入口 | 参考MODから必要部分を抽出・調整 |
| スクリプト検証 | PowerShell 7.4で模擬ツールの異常系を検証。実ILSpyCmd 9.1.0.7988＋自作C# DLLでもレポート生成を確認。実ゲーム・Windowsでの成功とは別 |
| C#本体／csproj／manifest | 未作成。今はMODをビルドできない |
| セットアップ・インストーラー | 未作成 |
| 実ゲームの逆コンパイル／本体ビルド／ソロ・マルチ実機確認 | 未実施 |

## 手元のゲームを調査する

WindowsのPowerShellで、このリポジトリのフォルダを開いて実行する。ゲームを終了し、Steamの更新が終わってから行う。

.NET 9 SDKがなければ追加する（調査ツール用。本体の対象SDKは現行ゲーム調査後に決める）。

```powershell
winget install --id Microsoft.DotNet.SDK.9 --exact
```

PowerShellを開き直し、リポジトリ直下で調査スクリプトを実行する。

```powershell
.\scripts\Export-GameSources.ps1
```

初回はlocal/paths.txtを作成し、固定版のILSpyCmd 9.1.0.7988をlocal/ilspy9へ導入する（NuGetへの接続が必要）。ゲームDLLを短い一時フォルダ（OSのTEMP配下のsm-*）へ逆コンパイルして関連箇所を抽出するため、数分以上かかる場合がある。ゲームや生成プロジェクトは実行しない。

成功したら **local/selectmod-sources.txt** をこのチャットに送る。内容は以下。

- sts2.dll・0Harmony.dll・GodotSharp.dllのSHA-256、ファイルバージョン、取得可能ならSteam Build ID、runtimeconfig。
- 実在するソースの一覧と、ラン開始・ロード・保存・部屋生成・マップ・アセンション・通信に関係する主要な型。
- HasLevelや抽選・同期処理などの検索結果と前後のコード。既定では最大1200箇所で、打ち切り件数を明記する。完全な呼び出し解析ではない。

セーブ・ゲーム設定・MOD導入先は読み書きしない。変更するのは作業用local/配下、OSのTEMP配下に作成する専用一時フォルダ、指定したパスファイルの初回作成だけ。DLLのハッシュを前後で確認し、失敗・更新検出時は前回の正常レポートを置き換えない。成功時は今回の一時ソースを削除し、前回のレポートは別名で残す。生成ソース・DLL・診断結果はGit対象外。

失敗したら **local/selectmod-export-error.txt** を送る。調査に使った一時ファイルも残す（場所はエラーファイルのPartial output欄）。PowerShell自体に実行を拒否された場合はスクリプトが起動していないため、このエラーファイルは作成されない。

### ゲームの場所が標準と違う場合

local/paths.txtの3行は、ゲームルート、セーブのルート、SelectMODの設定ディレクトリ。既存ファイルは上書きしない。ゲームが見つからなければ、1行目を実際の場所に直して再実行する。調査では2・3行目の場所にはアクセスしない。

パスファイルを開く（リポジトリ直下）。

```powershell
notepad .\local\paths.txt
```

別のパスファイルで調査する場合。

```powershell
.\scripts\Export-GameSources.ps1 -PathsFile 'D:\STS2-settings\paths.txt'
```

書式は[scripts/paths.example.txt](scripts/paths.example.txt)を参照。

### PowerShellの権限エラー

「スクリプトの実行が無効」は実行ポリシー、「署名されていない」は取得元のブロック、「アクセス拒否」は書き込み権限、「使用中」はファイルロックを確認する。

現在のポリシーを確認する（作業フォルダは任意）。

```powershell
Get-ExecutionPolicy -List
```

組織のポリシーで管理されていない場合、信頼できるスクリプトのために現在のPowerShellだけRemoteSignedにする。

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned
```

取得元と内容を確認し、エラーで指定されたファイルだけブロックを解除する（例）。

```powershell
Unblock-File -LiteralPath .\scripts\Export-GameSources.ps1
```

MachinePolicy/UserPolicyは上書きしない。調査は通常のユーザー権限で行う。導入時の書き込み拒否では必要に応じて同じWindowsアカウントで昇格し、使用中ならゲームを通常終了する。広範囲のACL変更やプロセスの強制終了はしない。

起動前のポリシー情報をファイルに保存する（localが作成済みの場合、リポジトリ直下）。

```powershell
Get-ExecutionPolicy -List | Out-File -Encoding utf8 .\local\execution-policy.txt
```

## ビルド・導入

[scripts/Build.ps1](scripts/Build.ps1)は、参照DLLの検査・SDK確認・対象csprojのビルド・終了コード確認を前作から引き継いだ入口。**本体がない今は「未実装」として停止する。** 前作専用のテスト・保存先パッチ・監査承認・インストール処理は引き継いでいない。

今後src/SelectMOD/SelectMOD.csprojを用意した後のビルド入口（リポジトリ直下、現時点では実行不可）。

```powershell
.\scripts\Build.ps1
```

参照するDLLはゲーム所有のものを外部指定し、csprojではPrivate=falseにする。DLLを同梱・再配布しない。Build.ps1はビルドのみで、配布物のステージングやゲームへの導入は行わない。SDK/TFMはゲームのruntimeconfigを確認して確定する。

導入機能を作る段階でSetup.ps1を1コマンドの入口にする予定。テスト・ビルドに失敗した場合は既存の導入を維持し、ゲーム外に旧版を退避して復元できる形にする。

スクリプトの独立検証を行う（PowerShell、リポジトリ直下。ゲームDLL不要）。

```powershell
.\tests\Scripts.Tests.ps1
```

## 資料と参考元

| ファイル | 内容 |
| --- | --- |
| [AGENTS.md](AGENTS.md) | Codexの共通開発ルール |
| [STS2_MODDING.md](STS2_MODDING.md) | STS2固有の注意とSelectMODの技術上の条件 |
| [DESIGN_AND_LESSONS.md](DESIGN_AND_LESSONS.md) | 初期設計・判断理由・未確定事項・検証順序 |
| [TECHNICAL_RESEARCH.md](TECHNICAL_RESEARCH.md) | 版固定の外部ソースに基づく既存調査 |

参考フォルダは必要な開発ルールとスクリプトの仕組みを引き継いだ後、削除済み。前作は[SlayTheSpire2ModSaveManeged](https://github.com/ROMEKANA/SlayTheSpire2ModSaveManeged)から再確認する。削除前の同梱資料は、このリポジトリのコミット3181983にも残っている。前作の0.3.0実機成功報告をSelectMODの成功として扱わない。

元の相談: [Mod作成方法比較](chatgpt-conversation://6af79b5d-a204-83ee-8d1b-01a386bd5559)。会話内の推奨や参考MODの説明は、現行ゲームの動作保証にはしない。

## 更新メモ

- 2026-11-18: Windows PowerShellで長い生成ソースの読み取りに失敗した報告を受け、短いTEMP作業先と長いパス対応の読み取りを追加。レポートの一時書き込みはlocal/に置き、TEMPが別ドライブでも前回分を保全する。更新後は同じExport-GameSources.ps1コマンドで再実行できる。旧local/source-export-*はそのまま残してよい。

- 2026-11-18: ビルド入口・共通パス処理・ゲーム調査スクリプトと独立検証を追加。一時参考フォルダを削除して参照元URLを保存。ソロで機能確認し、同期の最小試作は早期に行う順序を追記。
- 2026-11-18: チャット名を「SelectMOD製作」に変更。参考資料と前の会話を確認し、ルートの4資料を整備。

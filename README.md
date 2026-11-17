# SelectMOD製作メモ

自分用の非公開向けメモ。現在は **0.3.0：ソロのボス・エンシェント選択版**。通常ランの開始直前に選択画面を開く実装まで用意した。Cloudの独立テストとGodot UI APIのコンパイルは成功。**0.3.0の実ゲームDLLでのビルド・画面・進行・再開はこれから確認する。**

0.2.0は手元でビルド・導入できたが、開始後に固まったとの報告があった。ログは設定選択までで途切れており、停止原因は断定できない。まず動くソロ選択MODを優先し、個別アセンションのフックを外した。通常の累積アセンションはゲームの設定をそのまま使う。

## 使えるようにした機能

- ソロの新規Standardランで、通常の開始操作の直後・ラン作成前に選択画面を表示する。Standardカスタムランの開始経路も対象。
- 各Actの主ボスとエンシェントを指定するか、「標準の抽選」を選ぶ。
- 「この設定で開始」で確定。「変更せず開始」または画面を閉じると、指定せず開始する。
- **1つでも指定した場合、入口は通常ランでも内部的にはカスタムラン扱いになる。** 実績・進行等の扱いはゲーム標準のカスタムラン規則に従う。全項目が標準なら元のラン種別を保つ。
- ボスはそのActの候補、エンシェントはそのActの解放済み候補。Act2以降では解放済み共通エンシェントも指定できる（通常のランダムなAct割り当てを越えて指定可能）。同じ共通エンシェントの複数Act指定は現段階では禁止していない。
- A10の第2ボスは標準抽選を維持する。指定した主ボスと重なる場合だけ、元の主ボスを第2ボスに入れ替える。第2ボスを直接指定する画面はまだない。
- 通常の全Act生成後、マップ生成前に指定を反映する。追加でゲームの乱数を消費しない設計。
- 指定内容と確定した出現先をランに保存し、再開では再抽選せず照合する。

外部MODへの依存はない。ゲーム標準ローダーと同梱Harmonyを使用し、affects_gameplay=true。MOD ID／DLL名はSelectMOD。調査済みSteam Build IDは26660232で、導入・起動時にDLLの指紋を照合する。未知のDLLでは停止する。

## 今の優先順位と制限

第一目標はソロで選択→開始→実際の遭遇→保存・再開を通すこと。Ban・候補制限、個別アセンション、マルチ対応は後回し。マルチ対応の目標とラン単位の保存構造は残すが、**この版の導入中はマルチの開始・再開を止める**。デイリー、Blitz／Dread、選択ランのリプレイは対象外。

0.2.0の個別アセンション付き保存ランは、効果を黙って変えず再開を拒否する。保存データは変換・削除しないので、今回の確認は新規ランで行う。0.1.0の設定保存テスト用モデルは過去の保存ランを読むため残しているが、新しい選択肢には表示しない。

| 確認項目 | 状態 |
| --- | --- |
| 現行ゲームのソース調査 | v2の全1586検索結果と保存・生成・通信の契約を確認。再採取は今のところ不要 |
| 0.1.0実DLLビルド・導入・初期化 | 手元のレポートで確認済み |
| 0.1.0保存・次回設定の分離 | 保存42／再起動後42、次回設定73／新規73を確認済み |
| 0.2.0実DLLビルド・導入 | 手元で警告0・エラー0。開始後のフリーズ報告あり |
| 0.3.0独立契約・Harmonyテスト | 非同期選択、二重開始防止、保存復元、A10、乱数維持、旧アセンション拒否を検証 |
| 0.3.0 Godot APIコンパイル | 公開GodotSharp 4.5.1で警告0・エラー0。実ゲーム参照ビルドとは別 |
| 0.3.0実ゲームの表示・進行・再開 | 未確認。以下のセットアップで試す |

## 更新・ビルド・導入

Windows PowerShellで、このリポジトリ直下を開いて実行する。ゲームは終了しておく。

.NET 9 SDKがない場合だけ追加する（SDK 10だけではglobal.jsonを満たさない）。

```powershell
winget install --id Microsoft.DotNet.SDK.9 --exact
```

リポジトリを更新する。

```powershell
git pull --ff-only origin main
```

テスト・実ゲームDLLでのビルド・導入をまとめて実行する。

```powershell
.\scripts\Setup.ps1
```

初回はlocal/paths.txtを作り、3行でゲームルート・セーブのルート・MOD設定ディレクトリを管理する。既存のパス・設定は維持する。以前選択画面を無効にした場合、次のコマンドで再び有効にして導入する。

```powershell
.\scripts\Setup.ps1 -SelectionDialog $true
```

設定のSelectionDialogは次回の選択画面の表示だけを切り替える。旧AscensionMaskは保全するが使用しない。以前の-Ascensions引数は廃止。ボスやエンシェントはセットアップ時ではなくゲーム内で毎回選ぶ。

Setupはビルド失敗なら既存導入を置き換えず、導入途中の失敗なら旧版へ戻す。旧MODと設定のバックアップはmods外へ置く。配置するのはDLL・manifest・生成パス指定の3ファイルだけ。ゲームDLL・テスト用Harmony・GodotSharpを同梱しない。セーブをコピー・統合・書き換えしない。

## 今回のソロ確認

1. ゲームを起動し、**ソロの通常ランを新規開始**する。従来の個別アセンションModifierは選ばない。
2. 開始直前の画面でAct1の主ボス／エンシェントを選び、「この設定で開始」を押す。マップ表示と実際の出現先が一致し、進行できることを確認する。
3. 通常のセーブ・終了後に再起動し、同じランを再開する。選んだ出現先が維持され、先へ進めることを確認する。
4. 別の新規ランで全項目を「標準の抽選」にし、変更なしでも開始できることを確認する。A10を使う場合は最終Actの第2ボスも別途確認する。

途中で止まる・画面が出ない・選択が反映されない場合は、最後にできた操作を一言添えて次のログを送る。

ログを1ファイルにまとめ、最後にエクスプローラーでそのファイルを選択表示する。

```powershell
.\scripts\Export-ProbeReport.ps1
```

送るファイルは **local/selectmod-probe.txt**。ビルド・導入・MODログと、見つかった場合はゲームのlogs/godot.log末尾も含む。エクスプローラーが起動できなくてもレポートは残る。自動処理で画面を開きたくない場合は-NoOpenを付ける。

START_ENTER → SELECTION_SHOWN → SELECTION_CONFIRMED → START_ORIGINAL → ENCOUNTER_PREPARED → ROOMS_GENERATION_BEGIN → ROOMS_RESOLVED → START_COMPLETEDが開始時の目印。再開はENCOUNTER_RUN_LOADED。START_FAILED／ROOMS_FAILEDには例外を残す。ログの到達だけで実際の遭遇・進行確認済みとはしない。

無効化する場合はゲーム終了後、MODをmods外に退避する。

```powershell
.\scripts\Disable.ps1
```

セーブ・設定は削除しない。SelectMODのデータを含む中断ランには、このMODの対応版が必要。

## 手元のゲームを調査する

WindowsのPowerShellで、このリポジトリのフォルダを開いて実行する。ゲームを終了し、Steamの更新が終わってから行う。

.NET 9 SDKがなければ追加する（調査ツールと本体ビルド用。SDK 10だけではglobal.jsonを満たさない）。

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
- HasLevelや抽選・同期処理などの検索結果と前後のコード。既定では最大2500箇所で、打ち切り件数を明記する。完全な呼び出し解析ではない。

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

## 開発用の検証

本体だけビルドする（実ゲームDLLが必要）。

```powershell
.\scripts\Build.ps1
```

ゲームを使わない独立契約・Harmonyテスト。

```powershell
dotnet run --project .\tests\SelectMOD.ContractTests -c Release
```

公開Godot APIと独立ゲーム契約でのコンパイル確認（ゲームそのものの互換性は別）。

```powershell
dotnet build .\tests\SelectMOD.GodotCompileChecks -c Release
```

診断スクリプトの模擬テスト。

```powershell
.\tests\Scripts.Tests.ps1
```

インストーラーの模擬テスト。

```powershell
.\tests\Setup.Tests.ps1
```

Cloudにはゲーム本体がない。本番プロジェクトは実ゲームDLLのみを参照し、独立契約型や公開Godotパッケージを本番ビルドの代用にはしない。実DLLビルド・UI・実保存・実際の進行・マルチは別々に確認する。

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

- 2026-11-18／0.3.0: 開始後に固まる報告を受け、個別アセンションを停止。ソロの通常ラン開始直前に主ボス・エンシェントを選ぶ画面、確定結果の保存・照合、開始段階のログを追加。ログ回収後はエクスプローラーで出力ファイルを選択表示。
- 2026-11-18／0.2.0: 個別アセンションを実装し独立検証は通過。手元でビルド・導入できたが開始後に停止し、実効果の確認には至らなかった。
- 2026-11-18／0.1.0: 設定保存の試作と導入・診断スクリプトを整備。手元で再起動後の保存42／次回設定73と新規73の分離を確認。
- 初期資料・一時参考フォルダ・長いWindows生成パスの修正履歴はDESIGN_AND_LESSONS.mdとGit履歴に保存。

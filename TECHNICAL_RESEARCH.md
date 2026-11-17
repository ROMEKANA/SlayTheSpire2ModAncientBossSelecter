# Slay the Spire 2 独立MOD 技術調査

調査日: 2026-11-15。対象: Actごとのエンシェント／ボスの選択・Ban・候補制限、A1〜A10個別ON/OFF、ホスト権限とゲスト同期。今回は資料作成のみで、MOD実装・ゲーム実行は行っていない。

## 1. 結論と設計の確度

**標準のカスタムラン用 `ModifierModel` を設定・保存・同期の正本にする案を第一候補とする。** Ultimate Custom Run（以下UCR）には、BaseLib・RitsuLib・ModConfigを参照せず、独立したアセンション効果と `[SavedProperty]` を持つ設定を扱う実例がある。ただし現行ゲームDLLがないため、標準経路の通信形式、適用順序、ACK／途中参加の保証までは検証できない。実装可能性とマルチ安全性の確認を分ける。

とくに要求された3点は、以下を**暫定設計**とし、DLL照合・実機検証後に確定する。

| 論点 | 調査から支持される案 | 確定に必要な確認 |
|---|---|---|
| UCRからアセンションを独立 | 自MODの設定モデルと `AscensionManager.HasLevel` パッチで対象ランだけ10効果を判定。UCRの型・UI・DLLは参照しない | HasLevelを通らない数値比較、初期化前の判定、全10効果の呼び出し元 |
| ボス／エンシェントの確定時点 | ルールを開始前に凍結し、各Actの部屋生成に合わせて結果を一度だけ確定。少なくとも建図・表示・イベント生成より前 | GenerateRoomsとGenerateMapの実際の前後関係、全Act先行生成の有無、保存済み部屋の復元順序 |
| ホスト設定の保持 | ローカル既定値・ロビー編集中・ラン内固定値を分離。ラン内固定値を標準Modifier保存に含め、ロードでは保存値を優先 | SavedPropertyの対応型、標準保存／開始／ロード通信での往復、初回効果より早く参照できるか |

「ゲストにUIが表示された」「同じseedだった」だけでは同期完了と扱わない。設定・候補集合・生成結果・乱数消費位置が一致することを開始／再開条件とする。DLL未確認の現時点で安全性を保証したとはしない。

## 2. 調査環境・根拠・制約

リポジトリは調査開始時にソース・コミットのない状態。添付AGENTS.mdをルートに原文で追加した。本資料の日本語はユーザーの明示指定を優先している。

`/workspace`、`/tmp`、`/opt`、`/home` のアクセス可能な範囲で、`*sts2*.dll`／`*spire*.dll`を探索したが見つからなかった。ゲームのバージョン、Steam Build ID、DLL SHA-256／MVIDは取得できない。したがって**現行DLLを逆コンパイルして確認した事項はゼロ**。参考MOD内の「ILを確認した」というコメントも第三者の記述であり、今回の直接確認とは区別する。

GitHubの各ツリーを取得し、以下のコミットに固定したrawソースを読んだ。HEADの更新を追うリンクではなく、再現用に固定リンクを使う。

| 略号 | リポジトリ／調査コミット | 主な読了箇所 |
|---|---|---|
| U | [ultimate-custom-run](https://github.com/vertopolkaLF/ultimate-custom-run/tree/4187872a17cae5809c2bb2073d4b150473a5fbac) `4187872a17cae5809c2bb2073d4b150473a5fbac` | AscensionModifiers、CustomRunParameters、設定UI、ModifierValues、ModEntry、csproj、Smokeの関連テスト |
| S | [slay-the-streamer-2](https://github.com/Surfinite/slay-the-streamer-2/tree/43d9e22c2d0e78fdc228c51d01ae1e2fdd0f8a60) `43d9e22c2d0e78fdc228c51d01ae1e2fdd0f8a60` | BossVotePatch、AncientVotePatch、BossCandidateSampler、BossVoteSeed、SeedCompat、ActVariantVotePatch、csproj |
| N | [StS2-NotEnoughDifficulty](https://github.com/bwnotfound/StS2-NotEnoughDifficulty/tree/8026abb53913d9e21b47ea5fab816e3a5d6dbdbe) `8026abb53913d9e21b47ea5fab816e3a5d6dbdbe` | MultiplayerSync全6ファイル、Ancient／Boss重複回避、DoubleBoss、ValidateAncientAfterLoad、csproj |

以下の根拠番号を以後用いる。

- [U1: AscensionModifiers.cs](https://github.com/vertopolkaLF/ultimate-custom-run/blob/4187872a17cae5809c2bb2073d4b150473a5fbac/src/UltimateCustomRun/AscensionModifiers.cs): 10型、HasLevelパッチ、InitializeSharedへの結び付け、ホスト側UI。
- [U2: CustomRunParameters.cs](https://github.com/vertopolkaLF/ultimate-custom-run/blob/4187872a17cae5809c2bb2073d4b150473a5fbac/src/UltimateCustomRun/CustomRunParameters.cs): SavedProperty、作成／ロードフック、MutableCloneと旧保存互換。
- [U3: CustomRunParametersUi.cs](https://github.com/vertopolkaLF/ultimate-custom-run/blob/4187872a17cae5809c2bb2073d4b150473a5fbac/src/UltimateCustomRun/CustomRunParametersUi.cs): ホスト編集制限、ModifiersChanged、受信値コピー。
- [U4: ModifierVariantUi.cs](https://github.com/vertopolkaLF/ultimate-custom-run/blob/4187872a17cae5809c2bb2073d4b150473a5fbac/src/UltimateCustomRun/ModifierVariantUi.cs#L332): 選択、SetTickedModifiers、SyncModifierListのパッチ。
- [U5: ModifierValues.cs](https://github.com/vertopolkaLF/ultimate-custom-run/blob/4187872a17cae5809c2bb2073d4b150473a5fbac/src/UltimateCustomRun/ModifierValues.cs): SerializableModifier.PropsとSavedProperties、クローン。
- [U6: ModEntry.cs](https://github.com/vertopolkaLF/ultimate-custom-run/blob/4187872a17cae5809c2bb2073d4b150473a5fbac/src/UltimateCustomRun/ModEntry.cs)、[csproj](https://github.com/vertopolkaLF/ultimate-custom-run/blob/4187872a17cae5809c2bb2073d4b150473a5fbac/src/UltimateCustomRun/UltimateCustomRun.csproj)、[Smoke](https://github.com/vertopolkaLF/ultimate-custom-run/blob/4187872a17cae5809c2bb2073d4b150473a5fbac/tests/Smoke/Program.cs#L631): 起動、依存、個別アセンションテスト。
- [S1: BossVotePatch.cs](https://github.com/Surfinite/slay-the-streamer-2/blob/43d9e22c2d0e78fdc228c51d01ae1e2fdd0f8a60/src/Game/DecisionVotes/BossVotePatch.cs): ボス差し替え、2体目、マルチ除外。
- [S2: AncientVotePatch.cs](https://github.com/Surfinite/slay-the-streamer-2/blob/43d9e22c2d0e78fdc228c51d01ae1e2fdd0f8a60/src/Game/DecisionVotes/AncientVotePatch.cs): 報酬選択への介入。出現エンシェント自体の選択ではない。
- [S3: BossVoteSeed.cs](https://github.com/Surfinite/slay-the-streamer-2/blob/43d9e22c2d0e78fdc228c51d01ae1e2fdd0f8a60/src/Game/DecisionVotes/BossVoteSeed.cs)、[BossCandidateSampler.cs](https://github.com/Surfinite/slay-the-streamer-2/blob/43d9e22c2d0e78fdc228c51d01ae1e2fdd0f8a60/src/Game/DecisionVotes/BossCandidateSampler.cs)、[SeedCompat.cs](https://github.com/Surfinite/slay-the-streamer-2/blob/43d9e22c2d0e78fdc228c51d01ae1e2fdd0f8a60/src/Game/DecisionVotes/SeedCompat.cs): 安定ハッシュ、別乱数、ゲーム版差。
- [N1: MultiplayerSyncPatches.cs](https://github.com/bwnotfound/StS2-NotEnoughDifficulty/blob/8026abb53913d9e21b47ea5fab816e3a5d6dbdbe/NotEnoughDifficultyCode/MultiplayerSync/MultiplayerSyncPatches.cs): 新規／ロードロビー、早期ハンドラ、開始前ACK。
- [N2: ConfigSyncMessage.cs](https://github.com/bwnotfound/StS2-NotEnoughDifficulty/blob/8026abb53913d9e21b47ea5fab816e3a5d6dbdbe/NotEnoughDifficultyCode/MultiplayerSync/ConfigSyncMessage.cs)、[ConfigSyncAckMessage.cs](https://github.com/bwnotfound/StS2-NotEnoughDifficulty/blob/8026abb53913d9e21b47ea5fab816e3a5d6dbdbe/NotEnoughDifficultyCode/MultiplayerSync/ConfigSyncAckMessage.cs)、[ConfigSyncManager.cs](https://github.com/bwnotfound/StS2-NotEnoughDifficulty/blob/8026abb53913d9e21b47ea5fab816e3a5d6dbdbe/NotEnoughDifficultyCode/MultiplayerSync/ConfigSyncManager.cs): 実際のパケット、適用、待機、復元。
- [N3: DeduplicateCustomActAncientsPatch.cs](https://github.com/bwnotfound/StS2-NotEnoughDifficulty/blob/8026abb53913d9e21b47ea5fab816e3a5d6dbdbe/NotEnoughDifficultyCode/ExtraActs/Patches/DeduplicateCustomActAncientsPatch.cs): エンシェント候補とRoomSet書換え。
- [N4: DeduplicateCustomActBossesPatch.cs](https://github.com/bwnotfound/StS2-NotEnoughDifficulty/blob/8026abb53913d9e21b47ea5fab816e3a5d6dbdbe/NotEnoughDifficultyCode/ExtraActs/Patches/DeduplicateCustomActBossesPatch.cs)、[DoubleBossConfigPatch.cs](https://github.com/bwnotfound/StS2-NotEnoughDifficulty/blob/8026abb53913d9e21b47ea5fab816e3a5d6dbdbe/NotEnoughDifficultyCode/ExtraActs/Patches/DoubleBossConfigPatch.cs): 建図後変更の不具合記録、現在の建図前確定、別乱数方式。
- [N5: ValidateAncientAfterLoadPatch.cs](https://github.com/bwnotfound/StS2-NotEnoughDifficulty/blob/8026abb53913d9e21b47ea5fab816e3a5d6dbdbe/NotEnoughDifficultyCode/ExtraActs/Compat/ValidateAncientAfterLoadPatch.cs): ロード検証と空エンシェント事故。
- [N6: DeterministicModelHashPatch.cs](https://github.com/bwnotfound/StS2-NotEnoughDifficulty/blob/8026abb53913d9e21b47ea5fab816e3a5d6dbdbe/NotEnoughDifficultyCode/MultiplayerSync/DeterministicModelHashPatch.cs): モデルハッシュの不一致対策例。原因説明はMOD作者の記述として扱う。

表中の「ソース上」はMODの宣言・呼び出し・型指定・コメントを確認した意味であり、ゲームDLLの完全な宣言を確認した意味ではない。署名を復元できない箇所には戻り値・追加引数を補わない。

## 3. ホスト・ゲスト通信と標準同期

### 3.1 ソースから確認できる構造

N1は `MegaCrit.Sts2.Core.Multiplayer.Game.INetGameService` を使い、`Type == NetGameType.Host` を権限判定に、`NetId` をローカルIDに使う。新規開始ロビーは `StartRunLobby`、保存ランの再開ロビーは `LoadRunLobby`。ラン生成前には `RunManager.NetService` を利用できない場合があるため、ロビーの `NetService` を保持している。

N1/N2で確認した呼び出し形は以下。

```csharp
netService.SendMessage(new CustomMessageWrapper { Message = msg });
netService.RegisterMessageHandler<CustomMessageWrapper>(EarlyHandleCustomMessage);
// MOD側ハンドラの宣言:
private static void EarlyHandleCustomMessage(CustomMessageWrapper wrapper, ulong senderId)
```

**`CustomMessageWrapper` と `ICustomMessage` は `BaseLib.Abstracts` の型。ゲーム標準APIではない。** 上のコードをそのまま流用すると独立MOD要件を破る。`SendMessage<T>`／`RegisterMessageHandler<T>`のゲーム側制約、型ID登録、未知メッセージの処理、送信先指定、ハンドラ解除はDLL未確認。独自型を渡すだけで動くとは結論できない。

N1は標準開始メッセージ名を `LobbyBeginRunMessage`／`LobbyBeginLoadedRunMessage` と記述し、独自設定をその前に送る。これらのゲーム内宣言、フィールド、正確な送受信順序、配送チャネルは未確認。N2コメントの「reliable+ordered」は通信順序の実証としては採用しない。Reliable指定だけでは異種メッセージ間の順序や適用完了を保証しない。

構造上は全員に同じ設定を渡して各端末がゲーム処理を進める設計が必要となる。ホストだけがHasLevelやRoomSetを変更して標準機構が自動的に全状態を修正すると期待しない。トランスポートのトポロジー、Steam等の下層実装、ゲーム全体の権威モデルは今回未確認。

### 3.2 第一候補: 標準Modifier同期

U2では `CustomRunParameters : ModifierModel` に `[SavedProperty] public int FloorsPerAct { get; set; }` 等の実プロパティを用意する。U3ではホスト編集後に `NCustomRunModifiersList.SignalName.ModifiersChanged` を発火し、受信側のModifierからUI用インスタンスへ値をコピーする。U4は `SetTickedModifiers` と `SyncModifierList` の第1引数を `IReadOnlyCollection<ModifierModel>` として受ける。U1ではホストのみ `Lobby.SyncAscensionChange(0)` を呼ぶ。

標準同期を優先する具体案:

1. 自MOD専用の設定 `ModifierModel` を1つ設ける。名前・IDは自MODの名前空間にする。以下のモデル名・フィールド名は設計案でありゲームAPIではない。
2. 実プロパティとしてschema版、10bit効果マスク、Act別ルール、revision、凍結状態を保存する。まずソース実例のあるint型を優先し、文字列・リスト・ModelId配列のSavedProperty対応をDLLで確認してから表現を選ぶ。
3. 標準カスタムラン画面の選択リストに自モデルを載せ、既存のModifiersChanged→同期→受信リスト更新を利用する。通常ラン／デイリーへの適用は今回の最小案に含めず、必要なら別途入口を調べる。
4. 受信値を検証してから読み取り専用UIに反映する。受信中は変更通知による再送を抑止する。U4ではSetTickedModifiers本体がpostfixより先に通知するため、値の反映をprefixで行っている点に注意。
5. 標準開始／保存／ロードが同じModifier値を保持するか往復試験する。U5の任意文字列キーをPropsに後付けする旧方式は採らず、U2の登録されたSavedProperty方式を優先する。

UCRのコードは標準経路を使う有力な先例だが、ネットワーク実装そのものは含まない。設定の変更が標準Readyを解除するか、途中参加に全設定を再送するか、開始時の最新revisionを保証するかは未確認。ここを通過条件にする。

### 3.3 必要時のみ追加する通信

標準経路が完全なsnapshot・参加者別適用完了を保証するなら、独自の設定転送を追加しない。確認できない場合は標準Modifierを設定正本のままにし、**検証digestとACK／開始バリアだけ**をゲームの既存トランスポート上に追加する案を次点とする。標準で運べないデータがある場合に限りsnapshot転送も追加する。別TCP・UDPサーバーは設けない。

独立実装にはDLLで次の確認が必要: ゲーム側メッセージinterfaceと全メンバー、登録表／型IDの割当、serializer、最大長、配送モード、ホストID取得、宛先指定、lobbyでの受信登録時点、解除方法。これらは未確認APIのためコード例で捏造しない。BaseLibのwrapperやバッファを再実装する設計はまだ確定できない。

N1の「lobby構築時に登録し、開始前に全ゲストACKを待つ」という配置は参考になる。一方、全constructorへの無条件パッチや重複登録例外の握り潰しは採らず、確認したconstructor署名に限定して1セッション1登録・確実な解除を設計する。

## 4. アセンションA1〜A10の独立化

### 4.1 UCRで実在する方式

U1は次の10個の `AscensionLevel` メンバーを使う。左列はUCRの列挙順に対応するA番号の整理であり、**enum数値と実際の効果量は現行DLLで未照合**。

| A | enumメンバー／UCRクラス名 | 調べる効果領域（名称に基づく整理） |
|---|---|---|
| 1 | SwarmingElites | エリート配置 |
| 2 | WearyTraveler | 回復等の制限 |
| 3 | Poverty | ゴールド獲得 |
| 4 | TightBelt | ポーション枠 |
| 5 | AscendersBane | 開始デッキの呪い |
| 6 | Inflation | ショップ価格 |
| 7 | Scarcity | 報酬等の制限 |
| 8 | ToughEnemies | 敵の耐久等 |
| 9 | DeadlyEnemies | 敵の攻撃等 |
| 10 | DoubleBoss | 追加ボス |

これはゲームの効果説明を確定した表ではない。具体的な倍率・対象・実行箇所を名称から推測して実装しない。U1の表示は `AscensionHelper.GetTitle((int)Level, GameMode.Standard)`／`GetDescription(...)` を使う。

確認できたゲーム呼び出し形とMODパッチ署名:

```csharp
// U1: Harmonyターゲット。MODパッチ引数よりbool結果とAscensionLevel引数を確認。
AscensionManager.HasLevel(AscensionLevel level) // 戻り値bool、追加overloadは未確認
private static void Postfix(AscensionManager __instance,
    AscensionLevel __0, ref bool __result)

// U6 Smokeに存在するコンストラクタ呼び出し:
new AscensionManager(GameMode.Standard, 0)
```

UCRは `ConditionalWeakTable<AscensionManager, RunState>` で管理器とランを結び、標準結果がfalseのときだけ `state.Modifiers.OfType<AscensionModifier>()` を見てtrueにする。`RunManager.InitializeShared` のpostfixから結び付け、`<State>k__BackingField` を反射で読む。無関係な管理器は変えない。

UCRは独立効果を選ぶとロビーの累積アセンションを0にする。したがって**高い累積値の一部をOFFにする機能を、このpostfixのOR方式だけで実現することはできない**。SmokeはA0で1効果ずつ、複数効果、保存復元、非対象A10管理器の保持を検査するソースになっているが、今回そのテストは実行していない。

### 4.2 自MODへの切り出し案

- UCRの10クラスや型チェックを直接参照せず、自設定モデルの10bitマスクを使う。全OFFでも独立モードであることを表せるよう、マスクとは別に有効フラグまたは設定モデルの存在でモードを判定する。
- 対象ランの管理器だけ、既知の10効果について `__result = maskに含むか` とする案を優先する。未知のenumと対象外ランは標準結果を維持する。UIでOFFにした効果を累積値が再びONにしないようにする。
- カスタムランの累積値は0にして、マスクのみを効果正本とする案がUCRに近く安全。表示用の「A10」と実効効果を混同しない。実績・アンロック・記録上の扱いは標準カスタムランの挙動を調べ、通常ラン相当とは表明しない。
- ゲーム内効果の数式や戦闘フックは再実装しない。まずHasLevel経由で既存処理を利用する。DLL全体で `HasLevel` の呼び出しと生のAscension数値比較を検索し、経由しない効果だけ追加パッチを検討する。
- `InitializeShared` postfixで間に合うとはまだ断定できない。デッキ・HP・ポーション等の初期生成より前にマスクを読める必要がある。必要なら管理器生成／初期化入口に結び付けを前倒しするが、実在署名を確認してから選ぶ。
- 効果はラン開始後変更不可。ロード時も開始デッキへの呪い追加や初期能力適用を再実行しない。保存済み状態を復元し、将来の判定にのみ保存マスクを使う。
- UCRとの同時利用は両方がHasLevelを書き換える可能性がある。初版では競合検出して開始を拒否し、Harmony順序だけで偶然成立させない。

U6のcsprojの参照は `sts2.dll`、`GodotSharp.dll`、`0Harmony.dll` のみ（net9.0）。独立MODでもゲーム同梱参照を候補とする。起動は `[ModInitializer(nameof(Initialize))]` とHarmony。U6コメントではゲームがロード済みMODのModifierModel派生型を発見するとされ、ModelDbへの重複手動登録を避けている。これも現行DLLで発見時点を照合する。

ソースの移植とランタイム依存の排除は別問題。実装前に各リポジトリLICENSEを確認し、コードを複製する場合の表示・配布条件を満たす。本調査では外部ソースをリポジトリに取り込んでいない。

## 5. エンシェント・ボス抽選と確定時点

### 5.1 ソースで確認できる対象

| 対象 | ソース上で確認したメンバー／呼び出し形 | 根拠・限界 |
|---|---|---|
| 部屋生成 | `RunManager.GenerateRooms`、`ActModel.GenerateRooms` | N3/N4。完全署名・呼出順・回数は未確認 |
| 候補 | `act.GetUnlockedAncients(state.UnlockState)`、`ActModel._sharedAncientSubset` | N3。後者は反射で `List<AncientEventModel>` と扱う。GetUnlockedAncientsの完全署名は未確認 |
| エンシェント結果 | `ActModel._rooms` → `RoomSet`、`rooms.HasAncient`、`rooms.Ancient = picked` | N3/N5。標準の公開setterがActModelに存在するか未確認 |
| ボス候補と結果 | `AllBossEncounters`、`BossEncounter`、`SecondBossEncounter`、`HasSecondBoss` | S1/N4 |
| ボス設定 | `act.SetBossEncounter(picked)`、`act.SetSecondBossEncounter(second)` | N4/S1。引数はEncounterModelとして使用、ゲーム宣言の完全署名は未確認 |
| UI込みボス変更 | `MapCmd.SetBossEncounter(rs, boss)` | S1で `Action<IRunState, EncounterModel>` 内から呼ぶ。通信を伴うかは未確認 |
| ロード検証 | `ActModel.ValidateRoomsAfterLoad` | N5パッチ署名は `EnsureAncientFilled(ActModel __instance, Rng rng)`。全引数・戻り値は未確認 |
| 乱数 | `state.Rng.UpFront.NextItem(available)`、`state.Rng.StringSeed` | N3。NextItemの空候補／消費回数の現行仕様は未確認 |

N3はUnlocked候補と割り当て済みshared subsetを連結して選ぶ。全モデル一覧を使うと未解放候補やActに割り当てられていないshared候補を混ぜるおそれがある。標準の有効候補を基準に制限する。型のcanonicalモデル／mutableモデルの区別とsetterの要求もDLLで調べる。

S2のAncientVotePatchは `NEventRoom.OptionButtonClicked(EventOption, int)` に介入して報酬選択を投票化するもの。出現エンシェント抽選の先例として転用しない。S1も `NTreasureRoom.OnProceedButtonPressed(NButton)` に介入する投票機能であり、両方とも `Players.Count > 1` では標準処理へ戻す。**このMODは当該機能のマルチ同期成功例ではない。**

### 5.2 時系列の設計案

1. **ロビー編集:** ホストがルールとマスクを編集。各Actを「位置＋実ActモデルID」で識別し、1〜3という型の決め打ちを避ける。Actの変種が未確定ならこの時点ではスロットルールだけ持つ。
2. **開始前凍結:** ルールrevision、参加者集合、対応ゲーム／MOD版を凍結し全員確認。最初のアセンション判定・部屋生成より前にラン設定を読めるようにする。
3. **部屋生成:** Actと候補が確定してから、標準の生成境界で選択結果を1度だけ確定する。全Actが開始時に生成されるなら、その時点で全Act分を確定する。Act移行ごとに生成される版なら当該Actの生成時に確定する。Act移行のたびに無条件再抽選しない。
4. **建図前:** 主ボス・必要な第2ボス・エンシェントを部屋結果へ反映。生成結果digestを照合する必要があれば、最初の建図／入室をバリアで止める。途中で非同期に返ってくる投票は採らない。
5. **Act移行:** 既に確定・保存された結果を再利用し、未生成のActだけ同じ手順を実行。UIアイコンと戦闘／イベントが同じ結果を見ることを確認する。
6. **途中ロード／再接続:** ホストの保存済み設定・生成済み結果を正本として復元し、ローカル既定値では上書きしない。マップ・部屋の復元後は照合のみ。保存に既存結果があるのにseedから再抽選しない。再接続も最初の行動前に同じ検証が必要。標準経路が対応しない場合は初版の途中参加を明示的に禁止する。

N3コメントはGenerateRooms完了後の変更を説明する一方、N4のボスpostfixは即 `return` する停止済みコードで、「建図後の変更でアイコンと敵が食い違った」と記録している。現在の `EnsureAct4TopBoss(ActModel act4, RunState state)` は建図前呼び出し用である。コメント間の時系列をゲーム全体の保証とせず、DLLで外側の生成経路を追う必要がある。単に `RunManager.GenerateRooms` postfixを採用するだけでは不十分。

### 5.3 ルールと乱数の扱い

ルールは対象ごとに Vanilla／Fixed／Restricted を持つ案とする。RestrictedにはAllow集合（未指定なら標準全候補）とBan集合を持ち、`標準有効候補 ∩ Allow − Ban` を使う。Fixedの禁止衝突、未知ID、空集合は開始前エラーにする。Banを黙って解除したり各端末で別fallbackを選んだりしない。未確定Actでのみ分かるエラーはその生成境界で全員停止する。

A10が有効なActでは主・第2ボス両方にBan／候補制限を適用する。Fixedは初版では主ボスの指定とし、第2ボスは許可候補から主と異なるものを選ぶ案。候補1件で二体目を作れなければエラー。二体目の固定UIや重複許可は追加仕様として明示する。A10をOFFにしても、別機構が作った第二ボスを無条件削除しない。標準A10による生成を判定側から抑える。

乱数方式は次の順で検証する。

- 第一候補は**同じ入力で全員が標準抽選を実行する方式**。候補制限を抽選直前に適用し、同じ順序の候補、同じRng・同じ回数で引く。Fixedでも標準乱数の進行をどう保つか定義する。候補件数でNextItem内部の消費回数が変わる可能性もDLLで確認する。
- 代替は標準の部屋生成を全員同条件で一度完了させ、生成された結果の対象だけを**MOD専用の決定的選択**で建図前に上書きする方式。ゲームRngを追加消費しない。独自乱数はseed・Act ID／位置・用途・ルールdigest・選択アルゴリズム版から導出する。標準生成が既に結果を別オブジェクトへコピーしていたら、この方式は採用しない。
- ホストのみがUpFrontを引いて結果IDだけ送る方式は不可。ホストとゲストのゲーム乱数カウンターがずれる。ホスト確定IDを配る場合も全員の標準乱数進行を一致させ、追加選択は独立した乱数とする。
- 候補は完全な安定IDで重複排除し `StringComparer.Ordinal` 相当で並べる。`Id.Entry` のみでカテゴリ衝突しないとは限らない。登録番号、Dictionary順、ロケール順、`GetHashCode()`、時刻を使わない。
- S3は `System.Random` と安定FNV-1a seedによる別抽選を使い、N4にもゲームRngを消費しない安定選択例がある。ただし単純剰余は偏りを生むため、必要なら版固定の棄却法で均等選択する。再接続やロードでの再実行を防ぐため最終IDも保存する。
- S3のSeedCompatは `Rng(ulong)` と旧 `Rng(uint, int)`、`RunRngSet.Seed` と `NMapScreen.SetMap` の幅変更を記述する。これらは版差の証拠であり現行版の署名保証ではない。seedの切り詰めや未検証の反射fallbackで互換性を装わない。

標準状態でVanillaを選ぶ場合は、元の抽選結果と乱数進行を保持する。OFFを含む設定変更により正当に生成経路が変わることと、同じ設定のホスト／ゲストで進行がずれることを区別して検証する。

## 6. 設定の保持と通信データ契約（設計案）

ローカルファイルはUIの次回既定値のみを保持し、ラン中の判定は固定snapshotから読む。ゲストの設定ファイルをホスト値で書き換えない。N2は静的設定を一時上書きしCleanUpで再読込する方式だが、自MODではその復元漏れを避ける。

| 層 | 所有者・寿命 | 用途 |
|---|---|---|
| LocalPreferences | 各端末、セッション外も保持 | 次回初期値、UI表示。ゲーム処理は直接参照しない |
| LobbyDraft | ホスト編集、ロビー限り | revision付きのルール。変更でReady／ACKを無効化 |
| RunConfigSnapshot | ホストが凍結、全端末に同値、ラン保存 | マスク・全Actルール・schema等の唯一の実効設定 |
| ResolvedActPlan | 生成時確定、ラン保存 | 各Actの実ID、主／第2ボスID、エンシェントID、確定フラグ |

以下は**論理的な同期対象**。標準Modifierで運べれば別パケットにしない。ゲーム側の実在フィールド名ではない。

| データ | 内容・検証 |
|---|---|
| Protocol/schema/algorithm version | 解釈と抽選規約を固定。未対応版は拒否 |
| Game build / mod version / compatibility fingerprint | 版不一致を検出。独立に取得した各端末の値を照合 |
| Session epoch / run identity / revision | 前ロビーの遅延メッセージ、同seed別ラン、古いACKを分離 |
| Ascension enabled + mask | 既知10bitのみ、全OFFと標準モードを区別 |
| Act rules | Act位置＋ID、対象種別、mode、Fixed ID、Allow ID列、Ban ID列 |
| Candidate digest | 解放状態・shared割当込みの実効候補の順序付き完全ID列のhash。生成時に確認 |
| Resolved results | 各Actの確定有無、最終ID、第2ボス有無。保存済み状態と照合 |
| Config/plan digest | 正規化した全フィールドにSHA-256等の安定hash。対象外フィールドを作らない |
| ACK/NACK（必要時） | epoch、revision、digest、送信者、成功／不一致理由。単なる受信通知ではなく適用後照合 |

文字列はUTF-8、集合はOrdinal順、null／空集合／未指定を区別し、件数・文字列長・全体長に上限を設ける。hash対象の仕様を版固定する。hashは改ざん認証の代用ではなく不一致検出用。通信由来senderと現在のホストIDを照合し、ゲスト発の設定更新は拒否する。ホストIDをパケット内の自己申告から信頼しない。ホスト移譲は標準の保証を調べ、初版で対応できなければ進行停止とする。

追加通信が必要な場合の状態遷移案:

`Editing → Validating → WaitingForAcks → Frozen → Starting → Active`

編集、参加・退出、Ready取消、タイムアウト、ロビー破棄で該当revisionの待機を無効化する。受信側は一時領域で全検証してから一括置換し、digest確認後ACK。ホストは凍結した参加者全員の同revision ACKを確認し、直前に参加者集合を再検査して標準開始を一度だけ実行する。await中に元メソッドへ流さない。再入許可はセッションとrevisionに限定した一回限りのトークンとし、グローバルboolの残留を避ける。ゲーム操作はメインスレッドへ戻す。

ロードも `LoadRunLobby` で同じバリアを使うが、入力は現在のLocalPreferencesではなく保存snapshot。生成済み結果は部屋の保存内容と照合する。欠落・破損を端末ごとに補抽選せず、明示的な共通移行処理がない保存は再開拒否。保存ファイル側に設定を保存できない案や、メモリのWeakTableだけを正本とする案は採用しない。

## 7. Harmonyパッチ候補と導入条件

以下は候補一覧であり、現行DLLの正確な署名・呼出時点を照合してから選定する。`?` は完全署名を確認できず、空引数と推定していないことを表す。

| ゲーム側候補 | ソース上の署名情報 | 目的／制限 |
|---|---|---|
| `AscensionManager.HasLevel` | U1: `AscensionLevel`引数、bool結果 | 対象ランの既知10効果のみ置換。初期化順検証が必須 |
| `RunManager.InitializeShared` | U1: `?`、postfixはinstanceのみ | 管理器とsnapshotの関連付け候補。遅ければ別入口に変更 |
| `NCustomRunScreen.OnModifiersListChanged` | U1: `?` | ホスト変更時の整合性・累積値0。権限制限は設定更新側にも設ける |
| `NCustomRunScreen.AscensionChanged` | U1: `?` | 累積モードとの排他、UI表示更新 |
| `NCustomRunModifiersList.GetModifiersTickedOn` | U4: 結果 `List<ModifierModel>` | 自設定モデルを選択へ含める候補 |
| `NCustomRunModifiersList.SetTickedModifiers` / `SyncModifierList` | U4: 第1引数 `IReadOnlyCollection<ModifierModel>`、他は未確認 | 受信値・UI反映、再送ループ防止 |
| `NCustomRunModifiersList._Ready` | UCR ModifierGroupsUiはここでGetAllModifiers呼出後へIL挿入 | 選択肢追加の先例。UI全体のコピーを避け、必要箇所だけ追加 |
| `StartRunLobby.BeginRunForAllPlayers` | N1は `private void (string seed, List<ModifierModel> modifiers)` として参照 | 新規開始の凍結／必要時のACKバリア。ゲーム実宣言は未照合 |
| `LoadRunLobby.BeginRunForAllPlayersIfAllReady` | N1は引数なしで反射Invoke、同期voidとの記述 | `IsAboutToBeginGame()`がtrueのとき再開前検証。旧名への無条件fallback禁止 |
| Start/Load lobby constructor | N1はINetGameServiceを含むconstructorを探索 | 独自通信が必要な場合のみ早期登録。完全署名・解除経路未確認 |
| `ActModel.GenerateRooms` / `RunManager.GenerateRooms` | N3/N4: `?` | 候補抽選／結果確定。建図との順序を必ず追う |
| `RunManager.GenerateMap` | N4は建図prefixからの確定を記述 | 生成済みplanの最終検証。完全署名・実際の呼び出し元は要追跡 |
| `ActModel.ValidateRoomsAfterLoad` | N5のpostfixが `Rng rng` を受ける | 保存結果の整合性検査。ローカル補抽選をしない |
| `ModifierModel.ToSerializable` / `FromSerializable` | U5: Toの結果SerializableModifier、From第1引数SerializableModifier／結果ModifierModel | SavedProperty標準で不足する場合のみ。独自Propsキー追加を避ける |
| `AbstractModel.MutableClone` | U2/U5: 結果AbstractModel | 外部保持状態があるならコピー。通常プロパティで済めば不要 |
| `RunManager.CleanUp` | N1: `?` | snapshot参照・通信待機・購読・再入トークンの解除 |

ModifierModelの `protected override void AfterRunCreated(RunState runState)`／`AfterRunLoaded(RunState runState)` はU2に存在するoverrideで、Harmonyパッチではなく標準フック候補。初回のアセンション効果や抽選より前かは未確認。

`MapCmd.SetBossEncounter` の呼び出しだけでゲスト同期が成立するとは扱わない。S1のUI更新は単人前提。エンシェントは公開setterの確認を優先し、なければ `_rooms` 反射書換えを厳密なバージョン検査付きの最終手段とする。フィールドが見つからないのに処理続行しない。

## 8. 異常時処理とState Divergence防止

| 異常 | 処理案 |
|---|---|
| MOD未導入／バージョン・モデルhash・schema不一致 | 標準互換チェックを維持。開始拒否し差分を表示。hash検査を無効化しない |
| 最新設定未受信／ACK timeout | ロビーに残して再同期可能にする。設定未適用での開始はしない |
| ACKの重複・旧revision・別session | 冪等処理または破棄。未参加者からのACKで人数を満たさない |
| 待機中の参加者変更・ホスト離脱 | 待機をcancelし再検証。ホスト権限が曖昧なら進行しない |
| 空候補／未解放Fixed／BanとFixed衝突 | 開始・生成を拒否しホストへ具体的なActと原因を表示 |
| 未知ID・候補集合不一致 | 全員停止。端末別の先頭候補・ランダムfallbackは禁止 |
| snapshotは一致、生成結果が不一致 | 最初のマップ／入室前に停止。設定digest・候補digest・結果・乱数診断を保存 |
| 不正長・破損パケット | 上限検査で拒否、一時状態を破棄。部分適用しない |
| 保存設定や生成結果の欠落 | 既知schemaの共通移行以外は再開拒否。勝手な補抽選は禁止 |
| 必須パッチ失敗・反射先不一致 | MOD起動時に非対応表示。対象機能付きランの開始を止める |
| 既存生成パッチと競合 | 競合内容を報告し対象ランを止める。silent skipや優先順位依存で続行しない |
| ラン終了／ロビー破棄／再試行 | handler・task・snapshot・pending・結び付けを解除。前ランの返信を無効化 |

N1/N2は開始前ACKを実装する一方、版不一致は警告して続行し、一部prefix例外でも標準開始へ流す。ConfigSyncMessage.HandleMessageはsenderIdを受けるが、その場でホストIDを照合していない。未知フィールドの無視／欠落時のローカル値維持、string設定がACK件数に含まれない構成もある。これらをそのまま採用せず、完全snapshotと内容digestで検証する。

N6のモデルhashソート変更は実験的な対策で、型IDや互換判定を広く変える。初版には導入しない。同じMOD構成・DLL・ゲーム版をそろえ、標準のモデルhash不一致は問題の検知として残す。State Divergence／checksumを抑止するパッチも作らない。

## 9. 検証計画と設計確定条件

### 9.1 DLL入手後の必須調査

ローカルの正規ゲームインストールの `sts2.dll`、`GodotSharp.dll`、`0Harmony.dll` を参照し、ゲーム本体をリポジトリに追加しない。調査時点のBuild ID、版、DLL SHA-256／MVID、decompiler版を記録する。

1. 上表の型の完全修飾名・visibility・戻り値・全引数・overloadを出力し、引用する逆コンパイル箇所に対応付ける。Start/Load lobbyは `MegaCrit.Sts2.Core.Multiplayer.Game`、AscensionManagerは `MegaCrit.Sts2.Core.Entities.Ascension`、RunManagerは `MegaCrit.Sts2.Core.Runs`、ActModel／ModifierModelは `MegaCrit.Sts2.Core.Models` が参考MODのusing根拠。
2. 新規開始→ローカル開始→InitializeShared→Modifierフック→部屋生成→建図、および保存ロード→部屋検証→描画の順を実際の呼び出しグラフで確定する。ゲスト側と再接続側も追う。
3. 標準Modifierのserializer・SavedProperty型登録・clone・ロビーメッセージ・開始メッセージ・保存復元を追跡する。任意string／配列／リストが対応するとは仮定しない。
4. `HasLevel`全呼び出し、Ascension値の直接比較、10効果別の実メソッドを列挙し、表4.1を効果量と完全署名まで埋める。最初の呼び出しにマスクが間に合うか検証する。
5. 候補の解放判定／shared割当／RoomSetの保存／第2ボス生成、Rng.NextItemの内部とRngカウンター保存を追う。標準生成後の結果上書きに副作用がないか確認する。
6. 独自通信が必要と判明した場合だけゲームのメッセージ登録interface・型ID・送信先指定・配送順・ライフサイクルを完全署名付きで追記する。

### 9.2 実装後の自動検証

- 正規化・digest: ID列の入力順、ロケール、別プロセスが違っても同値。欠落、未知bit、未知schema、長さ超過は拒否。
- ルール: Vanilla、Fixed、Banのみ、Allowのみ、併用、空集合、未知ID、未解放候補、A10の候補不足。
- アセンション: 全OFF・全ON・10単独・累積値との競合・対象外管理器。安価なマスク判定は全1024組合せ、実効果は各効果のゲーム経路を検査。初期呪い／枠／HP等はロードで二重適用されないことを確認。
- 保存・clone・通信: 自設定Modifierの全フィールドと確定planを保存→読込→再serializeして一致。fresh processでも一致。UCRがインストールされていない環境で行う。
- バリア: timeout、ACK重複／順逆転、変更中のACK、参加・退出、旧session返信、cancel、再入を模擬し、開始が1回以下かつ同じrevisionだけで起きることを確認。

### 9.3 実機マルチ受入試験

別プロセスのホスト＋ゲスト1人、加えて複数ゲストで行う。同じマシン内の共有static状態による見かけ上の一致を避ける。各自のローカル既定値を意図的に変える。

| 場面 | 合格条件 |
|---|---|
| ロビーでホスト編集／ゲスト操作 | 全ゲストに同値、ゲスト変更は効かない。UI以外の更新入口にも権限判定 |
| 新規開始／A効果初回適用 | 最初の判定前にsnapshot一致。A1〜10のON/OFFが実際に反映 |
| 各Act移行／変種Act | 候補・確定ID・設定revision一致。アイコン、実敵、実イベントが一致 |
| A10とボス制限 | 二体とも許可範囲で重複規約通り。候補不足を安全に拒否 |
| 開始直後／移行前後／報酬前後のsave/load | 同一結果を復元し再抽選・初期効果再適用なし |
| 遅延、切断、timeout、再接続 | 部分同期の進行なし。復帰時は保存snapshotを再照合 |
| 版／MOD構成／解放状態差 | 開始前または初回生成境界で明確に拒否。標準hash／checksum検知は有効 |
| 連続ラン・同seed別ラン | 前sessionの設定・結果・ACKが漏れない |
| Vanilla対照ラン | MOD無効の結果とRng状態が一致。独立選択方式なら対象外ゲームRngへの追加消費ゼロ |

ログは端末ごとにsession/revision/config digest、Act ID／位置、候補digest、主・第2ボス／エンシェントID、実効マスク、生成回数、取得可能なら各Rngカウンター、標準checksum結果を記録する。合格条件は「一度クラッシュしなかった」ではなく、境界ごとの照合一致と複数seedでの完走。通信／保存に同期対象が含まれていない場合はhashを表示するだけで完了としない。

### 9.4 今回実施した確認／未実施

実施: 添付AGENTS.md確認と原文コピー、リポジトリ状態確認、上記範囲のDLL探索、3リポジトリのコミット固定ソース取得と関連箇所読解、本資料の根拠リンク・パス・空白検査。

未実施: ゲームDLL逆コンパイル、MODビルド、Smoke実行、実際のネットワーク通信解析、保存ファイル往復、ゲーム起動・マルチ受入試験。ゲームやcsprojが本リポジトリにないため実行できない。

設計確定は、(1) HasLevel全効果の網羅と適用時点、(2) 建図前の確定と保存復元・乱数整合、(3) 標準Modifierでのホストsnapshot保持と開始／ロードの同期保証、の3条件が確認できてから行う。それまでは本資料の候補を実装済みAPIや確定設計として扱わない。

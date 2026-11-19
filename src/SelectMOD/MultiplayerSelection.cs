using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;

namespace SelectMOD;

public static class MultiplayerSelection
{
    private sealed class Session
    {
        public string Id = Guid.NewGuid().ToString("N");
        public int Revision;
        public LobbyChoice[]? Draft;
        public LobbyEnvelope? Current;
        public bool Starting;
        public string? Failure;
    }
    private static readonly ConditionalWeakTable<StartRunLobby, Session> Sessions = new();
    private static readonly ConditionalWeakTable<StartRunLobby, object> Closed = new();
    private static bool IsClosed(StartRunLobby lobby) => Closed.TryGetValue(lobby, out _);
    private static readonly ConditionalWeakTable<RunManager, object> ActiveRuns = new();
    public static event Action<StartRunLobby>? Changed;
    public static bool IsNetwork(StartRunLobby? lobby) => lobby?.NetService.Type is NetGameType.Host or NetGameType.Client;
    public static bool Supported(StartRunLobby lobby) => IsNetwork(lobby) && lobby.GameMode == GameMode.Standard;
    public static bool Editable(StartRunLobby lobby) => Supported(lobby) && !IsClosed(lobby) && lobby.NetService.Type == NetGameType.Host && !Sessions.GetOrCreateValue(lobby).Starting;
    public static StartRunLobby? GetLobby(object screen) =>
        (AccessTools.Property(screen.GetType(), "Lobby")?.GetValue(screen) ?? AccessTools.Field(screen.GetType(), "_lobby")?.GetValue(screen)) as StartRunLobby;
    public static (ModelKey? Boss, ModelKey? Ancient) Choice(StartRunLobby lobby, int index)
    {
        if (IsClosed(lobby)) return (null, null);
        var choices = Sessions.GetOrCreateValue(lobby).Current?.Draft.Choices;
        return choices != null && index < choices.Length ? (choices[index].Boss, choices[index].Ancient) : (null, null);
    }
    internal static int Mask(StartRunLobby lobby) => Sessions.GetOrCreateValue(lobby).Current?.Draft.AscensionMask ?? AscensionSettings.All;

    public static string Status(StartRunLobby lobby)
    {
        if (IsClosed(lobby)) return "ロビーは終了しました。";
        var session = Sessions.GetOrCreateValue(lobby);
        if (!Supported(lobby)) return "マルチはStandardカスタムランから作成してください。";
        if (session.Failure != null) return "設定エラー：" + session.Failure;
        if (session.Current == null) return "ホストの設定を待っています。";
        return (lobby.NetService.Type == NetGameType.Host ? "ホスト編集" : "ホストの設定（変更不可）") + $" revision={session.Current.Draft.Revision} / A{session.Current.Draft.Ascension} / {session.Current.Digest[..8]}";
    }
    public static void InitializeHost(StartRunLobby lobby)
    {
        if (Supported(lobby) && !IsClosed(lobby) && lobby.NetService.Type == NetGameType.Host && Sessions.GetOrCreateValue(lobby).Current == null)
            lobby.SetModifiers(lobby.Modifiers.ToArray());
    }
    public static void Edit(StartRunLobby lobby, int index, bool boss, ModelKey? key)
    {
        if (!lobby.IsCustom || !Editable(lobby)) throw new InvalidOperationException("SelectMOD: ホストだけが開始前に設定できます。");
        var session = Sessions.GetOrCreateValue(lobby);
        var choices = (session.Current?.Draft.Choices ?? InitialChoices()).ToArray();
        if (index < 0 || index >= choices.Length) throw new InvalidOperationException("SelectMOD: Act番号が不正です。");
        choices[index] = boss ? choices[index] with { Boss = key } : choices[index] with { Ancient = key };
        MultiplayerContract.Create(session.Id, checked(session.Revision + 1), choices, lobby.Ascension);
        session.Draft = choices;
        try { lobby.SetModifiers(lobby.Modifiers.ToArray()); } catch { session.Draft = null; throw; }
    }
    public static void Reset(StartRunLobby lobby)
    {
        if (!lobby.IsCustom || !Editable(lobby)) throw new InvalidOperationException("SelectMOD: ホストだけが設定できます。");
        var session = Sessions.GetOrCreateValue(lobby);
        session.Draft = EncounterCatalog.BuildOptions().Select(a => new LobbyChoice(a.Index, null, null)).ToArray();
        try { lobby.SetModifiers(lobby.Modifiers.ToArray()); } catch { session.Draft = null; throw; }
    }
    private static LobbyChoice[] InitialChoices() => EncounterCatalog.BuildOptions().Select(a => new LobbyChoice(a.Index, EncounterDraft.Get(a.Index).Boss, EncounterDraft.Get(a.Index).Ancient)).ToArray();
    public static void BeforeSetModifiers(StartRunLobby lobby, ref IReadOnlyCollection<ModifierModel> modifiers)
    {
        if (!IsNetwork(lobby)) return;
        if (lobby.GameMode != GameMode.Standard)
        {
            modifiers = modifiers.Where(m => m is not SelectLobbySettings).ToArray();
            return;
        }
        if (!Editable(lobby)) throw new InvalidOperationException("SelectMOD: 非対応モード／ゲスト／開始中の設定変更です。");
        if (modifiers.Any(m => m is SelectEncounterSettings or SelectSettingsProbe or SelectAscensionSettings) || modifiers.OfType<SelectLobbySettings>().Count() > 1) throw new InvalidOperationException("SelectMOD: 不正なロビー設定です。");
        var session = Sessions.GetOrCreateValue(lobby);
        var envelope = MultiplayerContract.Create(session.Id, checked(session.Revision + 1), session.Draft ?? session.Current?.Draft.Choices ?? (lobby.IsCustom ? InitialChoices() : EncounterCatalog.BuildOptions().Select(a => new LobbyChoice(a.Index, null, null)).ToArray()), lobby.Ascension, AscensionPreference.Read());
        var model = (SelectLobbySettings)ModelDb.Modifier<SelectLobbySettings>().ToMutable();
        model.SelectLobbyPayload = EncounterRules.Encode(envelope); model.Freeze();
        // The host must explicitly become ready again after every edit. The
        // final start packet contains the complete latest snapshot regardless
        // of delayed/missing preview messages on clients.
        if (lobby.Players.Count > 0) lobby.SetReady(false);
        modifiers = modifiers.Where(m => m is not SelectLobbySettings).Append(model).ToArray();
        session.Current = envelope; session.Revision = envelope.Draft.Revision; session.Draft = null; session.Failure = null;
    }
    public static void Notify(StartRunLobby lobby)
    {
        var current = Sessions.GetOrCreateValue(lobby).Current;
        if (current != null) ModEntry.WriteLog($"MP_LOBBY role={lobby.NetService.Type} revision={current.Draft.Revision} digest={current.Digest}");
        Changed?.Invoke(lobby);
    }
    public static LobbyEnvelope ReadSnapshot(IEnumerable<ModifierModel> models)
    {
        var list = models.ToArray();
        if (list.Any(m => m is SelectEncounterSettings or SelectSettingsProbe or SelectAscensionSettings)) throw new InvalidOperationException("SelectMOD: ロビーにラン用／旧版の設定が混入しています。");
        var snapshots = list.OfType<SelectLobbySettings>().ToArray();
        if (snapshots.Length != 1) throw new InvalidOperationException("SelectMOD: ホストの設定が欠落／重複しています。全員に同じ版を導入してください。");
        return snapshots[0].Read();
    }
    public static bool IsHostSender(StartRunLobby lobby, ulong sender)
    {
        if (lobby.NetService.Type != NetGameType.Client) return false;
        var property = AccessTools.Property(lobby.NetService.GetType(), "HostNetId");
        if (property?.GetValue(lobby.NetService) is not ulong host) throw new InvalidOperationException("SelectMOD: ホストの識別情報を取得できません。");
        return sender == host;
    }
    public static bool Accept(StartRunLobby lobby, IEnumerable<ModifierModel> models, bool starting)
    {
        if (!Supported(lobby)) throw new InvalidOperationException("SelectMOD: マルチはStandardカスタムラン専用です。");
        if (IsClosed(lobby)) return false;
        var session = Sessions.GetOrCreateValue(lobby);
        if (session.Starting) return false;
        if (starting) ModEntry.WriteLog("MP_START_ATTEMPT role=" + lobby.NetService.Type);
        var envelope = ReadSnapshot(models);
        if (session.Current != null)
        {
            if (session.Current.Draft.Session != envelope.Draft.Session) throw new InvalidOperationException("SelectMOD: ロビーの識別情報が変わりました。");
            if (envelope.Draft.Revision < session.Current.Draft.Revision) { if (starting) throw new InvalidOperationException("SelectMOD: 古い設定での開始を停止しました。"); return false; }
            if (envelope.Draft.Revision == session.Current.Draft.Revision && envelope.Digest != session.Current.Digest) throw new InvalidOperationException("SelectMOD: 同じ版の設定内容が異なります。");
        }
        session.Current = envelope; session.Failure = null; session.Starting = starting;
        Notify(lobby);
        return true;
    }
    public static bool FreezeHost(StartRunLobby lobby, ref List<ModifierModel> models)
    {
        if (lobby.GameMode != GameMode.Standard) { models.RemoveAll(m => m is SelectLobbySettings); return true; }
        if (!Supported(lobby) || IsClosed(lobby) || lobby.NetService.Type != NetGameType.Host) throw new InvalidOperationException("SelectMOD: マルチはホストがStandardカスタムランから開始してください。");
        var session = Sessions.GetOrCreateValue(lobby);
        if (session.Starting) return false;
        ModEntry.WriteLog("MP_START_ATTEMPT role=" + lobby.NetService.Type);
        if (session.Failure != null) throw new InvalidOperationException(session.Failure);
        var envelope = session.Current ?? throw new InvalidOperationException("SelectMOD: マルチ設定が初期化されていません。");
        if (envelope.Draft.Ascension != lobby.Ascension)
        {
            lobby.SetModifiers(lobby.Modifiers.ToArray());
            ModEntry.WriteLog("MP_READY_REQUIRED ascension changed; host must ready again");
            return false;
        }
        var model = models.OfType<SelectLobbySettings>().SingleOrDefault();
        if (model == null || model.Read().Digest != envelope.Digest) throw new InvalidOperationException("SelectMOD: 開始時のホスト設定が一致しません。");
        if (lobby.Players.Count is < 2 or > MultiplayerContract.MaxPlayers) throw new InvalidOperationException("SelectMOD: この試験版のマルチは2～4人用です。");
        ReadSnapshot(models);
        model.Freeze(); session.Starting = true; Notify(lobby);
        ModEntry.WriteLog($"MP_START_FROZEN revision={envelope.Draft.Revision} digest={envelope.Digest} players={lobby.Players.Count}");
        return true;
    }
    public static IReadOnlyList<ModifierModel> Bind(StartRunLobby lobby, IReadOnlyList<ActModel> acts, IReadOnlyList<ModifierModel> models, int players, out int ascension)
    {
        if (!Supported(lobby)) throw new InvalidOperationException("SelectMOD: マルチはStandardカスタムラン専用です。");
        var session = Sessions.GetOrCreateValue(lobby);
        var envelope = ReadSnapshot(models);
        if (!session.Starting || session.Failure != null || session.Current?.Digest != envelope.Digest) throw new InvalidOperationException("SelectMOD: ホストが確定した開始設定がありません。");
        ascension = envelope.Draft.Ascension;
        var rules = MultiplayerContract.Bind(envelope, acts);
        var output = models.Where(m => m is not SelectLobbySettings).ToList();
        if (!lobby.IsCustom && EncounterRules.HasChanges(rules)) throw new InvalidOperationException("通常マルチではボス指定は使えません。");
        if (EncounterRules.HasChanges(rules))
        {
            var settings = (SelectEncounterSettings)ModelDb.Modifier<SelectEncounterSettings>().ToMutable();
            settings.SelectEncounterSchema = EncounterRules.Schema; settings.SelectEncounterRules = EncounterRules.Encode(rules);
            settings.SelectMultiplayerOrigin = EncounterRules.Encode(envelope); settings.SelectMultiplayerPlayers = players; output.Add(settings);
        }
        ModEntry.WriteLog($"MP_CONTRACT_CAPTURED digest={envelope.Digest} role={lobby.NetService.Type} phase=start");
        return AscensionPreference.Apply(output, ascension, envelope.Draft.AscensionMask ?? AscensionSettings.All);
    }
    public static void Reject(StartRunLobby lobby, string reason)
    {
        if (!IsClosed(lobby)) Sessions.GetOrCreateValue(lobby).Failure = reason;
        try { lobby.SetReady(false); } catch (Exception e) { ModEntry.WriteLog("MP_UNREADY_FAILED " + e.Message); }
        ModEntry.Reject(reason); Changed?.Invoke(lobby);
    }
    public static void Release(StartRunLobby lobby) { Sessions.Remove(lobby); Closed.Remove(lobby); Closed.Add(lobby, new object()); Changed?.Invoke(lobby); }
    public static object? Track(RunManager manager, RunState state)
    {
        if (!state.Modifiers.Any(m => m is SelectEncounterSettings or SelectAscensionSettings)) return null;
        var token = new object(); ActiveRuns.Remove(manager); ActiveRuns.Add(manager, token); return token;
    }
    public static void Clear(RunManager manager, object? token = null) { if (ActiveRuns.TryGetValue(manager, out var active) && (token == null || ReferenceEquals(active, token))) ActiveRuns.Remove(manager); }
    public static bool IsCurrent(RunManager manager, object token) => ActiveRuns.TryGetValue(manager, out var active) && ReferenceEquals(active, token);
    public static bool HasSelectedRun(RunManager manager) => ActiveRuns.TryGetValue(manager, out _);
}

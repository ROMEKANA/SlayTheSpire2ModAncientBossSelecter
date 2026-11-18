using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace SelectMOD;

[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.AddLocalHostPlayer))]
internal static class MultiplayerHostInitializePatch
{
    private static void Postfix(StartRunLobby __instance) => MultiplayerSelection.InitializeHost(__instance);
}
[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.SetModifiers))]
internal static class MultiplayerEditPatch
{
    private static void Prefix(StartRunLobby __instance, ref IReadOnlyCollection<ModifierModel> __0) => MultiplayerSelection.BeforeSetModifiers(__instance, ref __0);
    private static void Postfix(StartRunLobby __instance) { if (MultiplayerSelection.IsNetwork(__instance)) MultiplayerSelection.Notify(__instance); }
}
[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.InitializeFromMessage))]
internal static class MultiplayerJoinPatch
{
    private static void Postfix(StartRunLobby __instance)
    {
        if (__instance.NetService.Type == NetGameType.Client && MultiplayerSelection.Supported(__instance))
            try { MultiplayerSelection.Accept(__instance, __instance.Modifiers, false); }
            catch (Exception e) { MultiplayerSelection.Reject(__instance, e.Message); }
    }
}
[HarmonyPatch(typeof(StartRunLobby), "HandleModifiersChangedMessage")]
internal static class MultiplayerReceivePatch
{
    private static bool Prefix(StartRunLobby __instance, LobbyModifiersChangedMessage __0, ulong __1)
    {
        try
        {
            if (!MultiplayerSelection.IsHostSender(__instance, __1)) { ModEntry.WriteLog("MP_IGNORED unauthorized preview sender"); return false; }
            var ownIds = new[] { ModelDb.GetId<SelectLobbySettings>(), ModelDb.GetId<SelectEncounterSettings>(), ModelDb.GetId<SelectSettingsProbe>(), ModelDb.GetId<SelectAscensionSettings>() };
            return MultiplayerSelection.Accept(__instance, __0.modifiers.Where(m => ownIds.Contains(m.Id)).Select(ModifierModel.FromSerializable), false);
        }
        catch (Exception e) { MultiplayerSelection.Reject(__instance, e.Message); return false; }
    }
}
[HarmonyPatch(typeof(StartRunLobby), "HandleLobbyBeginRunMessage")]
internal static class MultiplayerReceiveStartPatch
{
    private static bool Prefix(StartRunLobby __instance, LobbyBeginRunMessage __0, ulong __1)
    {
        try
        {
            if (!MultiplayerSelection.IsHostSender(__instance, __1)) { ModEntry.WriteLog("MP_IGNORED unauthorized start sender"); return false; }
            if (__0.playersInLobby?.Count is not (>= 2 and <= MultiplayerContract.MaxPlayers)) throw new InvalidOperationException("SelectMOD: マルチ参加者数が対応範囲外です。");
            var ownIds = new[] { ModelDb.GetId<SelectLobbySettings>(), ModelDb.GetId<SelectEncounterSettings>(), ModelDb.GetId<SelectSettingsProbe>(), ModelDb.GetId<SelectAscensionSettings>() };
            return MultiplayerSelection.Accept(__instance, __0.modifiers.Where(m => ownIds.Contains(m.Id)).Select(ModifierModel.FromSerializable), true);
        }
        catch (Exception e) { MultiplayerSelection.Reject(__instance, e.Message); return false; }
    }
}
[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.CleanUp))]
internal static class MultiplayerLobbyCleanupPatch
{
    private static void Postfix(StartRunLobby __instance) => MultiplayerSelection.Release(__instance);
}
[HarmonyPatch(typeof(NGame), nameof(NGame.StartNewMultiplayerRun), new[] {
    typeof(List<Player>), typeof(StartRunLobby), typeof(bool), typeof(IReadOnlyList<ActModel>),
    typeof(IReadOnlyList<ModifierModel>), typeof(string), typeof(int), typeof(DateTimeOffset?) })]
internal static class MultiplayerStartPatch
{
    private static void Prefix(List<Player> __0, StartRunLobby __1, bool __2, IReadOnlyList<ActModel> __3,
        ref IReadOnlyList<ModifierModel> __4, string __5, ref int __6, DateTimeOffset? __7, out string? __state)
    {
        __state = null;
        try
        {
            if (!ModEntry.IsActive || !__2 || __7 != null || __0.Count != __1.Players.Count || __0.Count is < 2 or > MultiplayerContract.MaxPlayers)
                throw new InvalidOperationException("SelectMOD: 未対応のマルチ開始条件です。");
            __state = MultiplayerSelection.ReadSnapshot(__4).Digest;
            __4 = MultiplayerSelection.Bind(__1, __3, __4, __0.Count, out __6);
            var context = MultiplayerContract.Hash(EncounterRules.Encode(new { Seed = __5, Ascension = __6, Players = __0.Select(p => p.NetId).ToArray(), Acts = __3.Select(EncounterCatalog.Key).ToArray() }));
            ModEntry.WriteLog("MP_START_CONTEXT digest=" + context);
        }
        catch (Exception e) { ModEntry.WriteLog("MP_START_FAILED " + e); throw; }
    }
    private static void Postfix(string? __state, ref Task<RunState> __result)
    {
        if (__state != null) __result = Observe(__result, __state);
    }
    private static async Task<RunState> Observe(Task<RunState> task, string digest)
    {
        try { var state = await task; ModEntry.WriteLog("MP_START_COMPLETED digest=" + digest); return state; }
        catch (Exception e) { ModEntry.WriteLog("MP_START_FAILED digest=" + digest + " " + e); throw; }
    }
}
[HarmonyPatch]
internal static class MultiplayerScreenLifecyclePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { "MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen", "MegaCrit.Sts2.Core.Nodes.Screens.CustomRun.NCustomRunScreen" })
        {
            var type = typeof(StartRunLobby).Assembly.GetType(name) ?? throw new TypeLoadException(name);
            yield return AccessTools.Method(type, "AfterInitialized", Type.EmptyTypes) ?? throw new MissingMethodException(name, "AfterInitialized");
        }
    }
    private static void Postfix(Godot.Control __instance) => EncounterSelectionPanel.Refresh(__instance);
}
[HarmonyPatch(typeof(RunManager), nameof(RunManager.GetRejoinMessage))]
internal static class MultiplayerRejoinGuard
{
    private static void Prefix(RunManager __instance)
    {
        if (MultiplayerSelection.HasSelectedRun(__instance)) throw new InvalidOperationException("SelectMOD: 進行中の再接続は未対応です。全員で終了し、保存したマルチランを再開してください。");
    }
}

[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.SyncAscensionChange))]
internal static class MultiplayerAscensionPreviewPatch
{
    private static void Postfix(StartRunLobby __instance)
    {
        if (MultiplayerSelection.Editable(__instance)) __instance.SetModifiers(__instance.Modifiers.ToArray());
    }
}

[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.SetGameMode))]
internal static class MultiplayerModePatch
{
    private static void Postfix(StartRunLobby __instance)
    {
        if (MultiplayerSelection.Editable(__instance)) __instance.SetModifiers(__instance.Modifiers.ToArray());
        else if (MultiplayerSelection.IsNetwork(__instance)) MultiplayerSelection.Notify(__instance);
    }
}
[HarmonyPatch(typeof(StartRunLobby), "HandleGameModeChangedMessage")]
internal static class MultiplayerReceiveModePatch
{
    private static void Postfix(StartRunLobby __instance) => MultiplayerSelection.Notify(__instance);
}

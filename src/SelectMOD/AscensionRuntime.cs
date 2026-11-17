using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Runs;

namespace SelectMOD;

public static class AscensionRuntime
{
    private sealed class Binding(AscensionSettings settings, RunState state)
    {
        public AscensionSettings Settings { get; } = settings;
        public RunState State { get; } = state;
        public AscensionManager? Manager;
        public float OriginalCommonOdds;
        public float AppliedCommonOdds;
        public bool OddsApplied;
    }
    private static readonly ConditionalWeakTable<RunManager, Binding> Runs = new();
    private static readonly ConditionalWeakTable<AscensionManager, Binding> Managers = new();

    public static object? Prepare(RunManager manager, RunState state)
    {
        // The real setup methods reject a non-empty manager too; do not replace
        // the binding of an existing run before their original guard executes.
        if (manager.IsInProgress) throw new InvalidOperationException("SelectMOD: 前のランが終了していません。");
        var model = state.Modifiers.OfType<SelectAscensionSettings>().SingleOrDefault();
        if (model == null) return null;
        VerifyPatchOwners();
        Release(manager);
        var binding = new Binding(model.Freeze(), state);
        Runs.Add(manager, binding);
        return binding;
    }

    public static void AttachManager(RunManager manager)
    {
        if (!Runs.TryGetValue(manager, out var binding)) return;
        if (binding.Manager != null) throw new InvalidOperationException("SelectMOD: 管理器の初期化が重複しています。");
        binding.Manager = manager.AscensionManager;
        Managers.Add(binding.Manager, binding);
        // A7 has a static cached probability in the reviewed build, unlike the
        // other dynamic HasAscension consumers. Override only for this run.
        binding.OriginalCommonOdds = CardRarityOdds.regularCommonOdds;
        binding.AppliedCommonOdds = binding.Settings.Contains(7) ? 0.615f : 0.6f;
        CardRarityOdds.regularCommonOdds = binding.AppliedCommonOdds;
        binding.OddsApplied = true;
        var observed = 0;
        for (var level = 1; level <= 10; level++)
            if (binding.Manager.HasLevel((AscensionLevel)level)) observed |= 1 << (level - 1);
        if (observed != binding.Settings.Mask)
            throw new InvalidOperationException("SelectMOD: アセンション判定が設定と一致しません。");
        ModEntry.WriteLog($"ASCENSION_BOUND mask={binding.Settings.Mask} observedMask={observed} enabled={binding.Settings.Describe()}");
    }

    public static bool TryGet(RunManager manager, out AscensionSettings settings)
    {
        if (manager.IsInProgress && Runs.TryGetValue(manager, out var binding)) { settings = binding.Settings; return true; }
        settings = default;
        return false;
    }
    public static bool TryGet(AscensionManager manager, out AscensionSettings settings)
    {
        if (Managers.TryGetValue(manager, out var binding)) { settings = binding.Settings; return true; }
        settings = default;
        return false;
    }
    public static void LogRooms(RunManager manager)
    {
        if (!Runs.TryGetValue(manager, out var binding)) return;
        ModEntry.WriteLog($"ASCENSION_ROOMS mask={binding.Settings.Mask} finalSecondBoss={binding.State.Acts.Last().GetRoomsDebug().HasSecondBoss}");
    }
    public static void Release(RunManager manager, object? expectedBinding = null)
    {
        if (!Runs.TryGetValue(manager, out var binding)) return;
        if (expectedBinding != null && !ReferenceEquals(binding, expectedBinding)) return;
        if (binding.Manager != null) Managers.Remove(binding.Manager);
        if (binding.OddsApplied)
        {
            if (CardRarityOdds.regularCommonOdds == binding.AppliedCommonOdds)
                CardRarityOdds.regularCommonOdds = binding.OriginalCommonOdds;
            else ModEntry.WriteLog("ASCENSION_RARITY_CONFLICT external change retained");
        }
        Runs.Remove(manager);
    }
    private static void VerifyPatchOwners()
    {
        foreach (var method in new[] {
            AccessTools.Method(typeof(AscensionManager), nameof(AscensionManager.HasLevel)),
            AccessTools.Method(typeof(RunManager), nameof(RunManager.HasAscension)) })
        {
            var others = Harmony.GetPatchInfo(method)?.Owners.Where(owner => owner != ModEntry.Owner).ToArray() ?? [];
            if (others.Length != 0)
                throw new InvalidOperationException("SelectMOD: 他MODのアセンション判定と競合しています: " + string.Join(", ", others));
        }
    }
}

[HarmonyPatch(typeof(AscensionManager), nameof(AscensionManager.HasLevel), new[] { typeof(AscensionLevel) })]
internal static class IndependentLevelPatch
{
    private static bool Prefix(AscensionManager __instance, AscensionLevel __0, ref bool __result)
    {
        if ((int)__0 is < 1 or > 10 || !AscensionRuntime.TryGet(__instance, out var settings)) return true;
        __result = settings.Contains((int)__0);
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.HasAscension), new[] { typeof(AscensionLevel) })]
internal static class IndependentRunQueryPatch
{
    private static bool Prefix(RunManager __instance, AscensionLevel __0, ref bool __result)
    {
        if ((int)__0 is < 1 or > 10 || !AscensionRuntime.TryGet(__instance, out var settings)) return true;
        __result = settings.Contains((int)__0);
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), "InitializeShared")]
internal static class AscensionBindingPatch
{
    private static void Postfix(RunManager __instance)
    {
        try { AscensionRuntime.AttachManager(__instance); }
        catch { AscensionRuntime.Release(__instance); throw; }
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
internal static class AscensionRoomsPatch
{
    private static void Postfix(RunManager __instance) => AscensionRuntime.LogRooms(__instance);
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class AscensionCleanupPatch
{
    private static Exception? Finalizer(RunManager __instance, Exception? __exception)
    {
        AscensionRuntime.Release(__instance);
        return __exception;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpReplay))]
internal static class AscensionReplayGuard
{
    private static void Prefix(RunState __0)
    {
        if (__0.Modifiers.OfType<SelectAscensionSettings>().Any())
            throw new InvalidOperationException("SelectMOD: この版では個別アセンションを含むリプレイ再生は未対応です。");
    }
}

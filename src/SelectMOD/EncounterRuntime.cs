using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace SelectMOD;

public static class EncounterRuntime
{
    private sealed record Pending(RunState State, SelectEncounterSettings Model, string RulesJson);
    private static readonly ConditionalWeakTable<RunManager, Pending> Runs = new();
    public static object? Prepare(RunManager manager, RunState state)
    {
        if (manager.IsInProgress) throw new InvalidOperationException("SelectMOD: 前のランが終了していません。");
        var model = state.Modifiers.OfType<SelectEncounterSettings>().SingleOrDefault();
        if (model == null) return null;
        if (model.SelectEncounterResults.Length != 0) throw new InvalidOperationException("SelectMOD: 新規ランに確定済み結果が混入しています。");
        EncounterCatalog.ValidateChoice(model.ReadRules(), EncounterCatalog.Build(state.Acts, state.UnlockState));
        model.Freeze();
        Runs.Remove(manager);
        var pending = new Pending(state, model, model.SelectEncounterRules);
        Runs.Add(manager, pending);
        ModEntry.WriteLog("ENCOUNTER_PREPARED waiting for standard room generation");
        return pending;
    }
    public static void BeforeGenerate(RunManager manager)
    {
        if (!Runs.TryGetValue(manager, out var pending)) return;
        if (pending.Model.SelectEncounterResults.Length != 0) throw new InvalidOperationException("SelectMOD: 部屋の二重生成を停止しました。");
        ModEntry.WriteLog("ROOMS_GENERATION_BEGIN");
    }
    public static void Resolve(RunManager manager)
    {
        if (!Runs.TryGetValue(manager, out var pending)) return;
        try
        {
            if (pending.Model.SelectEncounterRules != pending.RulesJson) throw new InvalidOperationException("SelectMOD: 開始中に設定が変更されました。");
            var state = pending.State;
            var catalog = EncounterCatalog.Build(state.Acts, state.UnlockState);
            var rules = pending.Model.ReadRules();
            EncounterCatalog.ValidateChoice(rules, catalog);
            var plan = new ResolvedEncounter[rules.Length];
            for (var i = 0; i < plan.Length; i++)
                plan[i] = EncounterRules.Resolve(rules[i], Current(state.Acts[i], i),
                    catalog[i].Bosses.Select(EncounterCatalog.Key).ToArray(), catalog[i].Ancients.Select(EncounterCatalog.Key).ToArray());
            // Resolve and validate every ID before mutating any generated room.
            var bosses = plan.Select(p => ModelDb.GetById<EncounterModel>(EncounterCatalog.Id(p.Boss))).ToArray();
            var seconds = plan.Select(p => p.SecondBoss == null ? null : ModelDb.GetById<EncounterModel>(EncounterCatalog.Id(p.SecondBoss))).ToArray();
            var ancients = plan.Select(p => p.Ancient == null ? null : ModelDb.GetById<AncientEventModel>(EncounterCatalog.Id(p.Ancient))).ToArray();
            for (var i = 0; i < plan.Length; i++)
            {
                var act = state.Acts[i];
                act.SetBossEncounter(bosses[i]);
                act.SetSecondBossEncounter(seconds[i]);
                if (ancients[i] != null) act.GetRoomsDebug().Ancient = ancients[i]!;
                ModEntry.WriteLog($"ENCOUNTER_RESOLVED act={i + 1} boss={plan[i].Boss.StableKey} second={plan[i].SecondBoss?.StableKey ?? "none"} ancient={plan[i].Ancient?.StableKey ?? "none"}");
            }
            pending.Model.SelectEncounterResults = EncounterRules.Encode(plan);
            ModEntry.WriteLog("ROOM_PLAN_DIGEST digest=" + MultiplayerContract.Hash(pending.Model.SelectEncounterResults));
            ModEntry.WriteLog("ROOMS_RESOLVED all results saved before map creation; no additional game RNG draws");
        }
        catch (Exception e) { ModEntry.WriteLog("ROOMS_FAILED " + e); throw; }
    }
    public static ResolvedEncounter Current(ActModel act, int index)
    {
        var rooms = act.GetRoomsDebug();
        return new(index, EncounterCatalog.Key(act), EncounterCatalog.Key(rooms.Boss),
            rooms.SecondBoss == null ? null : EncounterCatalog.Key(rooms.SecondBoss), rooms.HasAncient ? EncounterCatalog.Key(rooms.Ancient) : null);
    }
    public static void ValidateSaved(SerializableRun save, SelectEncounterSettings model)
    {
        var rules = model.ReadRules();
        var plan = ReadPlan(model, rules.Length);
        if (save.Acts.Count != plan.Length) throw new InvalidOperationException("SelectMOD: 保存Act数が一致しません。");
        for (var i = 0; i < plan.Length; i++)
        {
            var act = save.Acts[i]; var room = act.SerializableRooms;
            var saved = new ResolvedEncounter(i, EncounterCatalog.Key(act.Id)!, EncounterCatalog.Key(room.BossId)!, EncounterCatalog.Key(room.SecondBossId), EncounterCatalog.Key(room.AncientId));
            if (saved != plan[i] || rules[i].Act != plan[i].Act ||
                (rules[i].SecondBoss != null && rules[i].SecondBoss != plan[i].SecondBoss) ||
                (rules[i].Boss != null && rules[i].Boss != plan[i].Boss) ||
                (rules[i].Ancient != null && rules[i].Ancient != plan[i].Ancient)) throw new InvalidOperationException("SelectMOD: 保存された出現結果が一致しません。再抽選せず停止します。");
            // Reject removed/deprecated targets before the game's fallback can
            // replace them or increment a load counter on the actual save.
            EnsureAvailable<ActModel>(plan[i].Act);
            EnsureAvailable<EncounterModel>(plan[i].Boss);
            if (plan[i].SecondBoss != null) EnsureAvailable<EncounterModel>(plan[i].SecondBoss!);
            if (plan[i].Ancient != null) EnsureAvailable<AncientEventModel>(plan[i].Ancient!);
        }
    }
    private static void EnsureAvailable<T>(ModelKey key) where T : AbstractModel
    {
        var model = ModelDb.GetById<T>(EncounterCatalog.Id(key));
        if (model.GetType().Name.StartsWith("Deprecated", StringComparison.Ordinal)) throw new InvalidOperationException("SelectMOD: 保存された候補は現在利用できません。");
    }
    public static void ValidateLoaded(RunState state, SelectEncounterSettings model)
    {
        var plan = ReadPlan(model, state.Acts.Count);
        for (var i = 0; i < plan.Length; i++)
            if (Current(state.Acts[i], i) != plan[i]) throw new InvalidOperationException("SelectMOD: 復元後の出現結果が一致しません。");
    }
    private static ResolvedEncounter[] ReadPlan(SelectEncounterSettings model, int count)
    {
        var plan = EncounterRules.Decode<ResolvedEncounter>(model.SelectEncounterResults);
        if (plan.Length != count) throw new InvalidOperationException("SelectMOD: 確定結果の数が一致しません。");
        for (var i = 0; i < plan.Length; i++)
        {
            if (plan[i] == null || plan[i].Index != i || plan[i].Act == null || plan[i].Boss == null) throw new InvalidOperationException("SelectMOD: 確定結果が破損しています。");
            plan[i].Act.Validate(); plan[i].Boss.Validate(); plan[i].SecondBoss?.Validate(); plan[i].Ancient?.Validate();
        }
        return plan;
    }
    public static void Release(RunManager manager, object? token = null)
    {
        if (Runs.TryGetValue(manager, out var current) && (token == null || ReferenceEquals(token, current))) Runs.Remove(manager);
    }
}

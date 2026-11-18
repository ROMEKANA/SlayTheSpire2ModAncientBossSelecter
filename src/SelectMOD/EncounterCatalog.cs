using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;

namespace SelectMOD;

public sealed record EncounterAct(int Index, ActModel Act, EncounterModel[] Bosses, AncientEventModel[] Ancients);
public static class EncounterCatalog
{
    public static ModelKey Key(AbstractModel model) => Key(model.Id)!;
    public static ModelKey? Key(ModelId? id) => id == null ? null : new ModelKey(id.Category, id.Entry);
    public static ModelId Id(ModelKey key) => new(key.Category, key.Entry);
    public static EncounterAct[] Build(IReadOnlyList<ActModel> acts, UnlockState unlocks)
    {
        if (acts.Count is < 1 or > EncounterRules.MaxActs) throw new InvalidOperationException("SelectMOD: 対応範囲外のAct数です。");
        return acts.Select((act, index) => BuildAt(index, act)).ToArray();
    }
    public static EncounterAct[] BuildOptions() => ModelDb.ActsByIndex
        .Select((variants, index) => BuildAt(index, variants.First())).ToArray();
    private static EncounterAct BuildAt(int index, ActModel actual)
    {
        // Fixed overrides may now use targets from any Act. Random still uses
        // the game's ordinary candidate pool and unchanged RNG progression.
        var variants = ModelDb.ActsByIndex.SelectMany(group => group).Append(actual);
        return new EncounterAct(index, actual,
            variants.SelectMany(a => a.AllBossEncounters).DistinctBy(Key)
                .OrderBy(m => m.GetType().Name switch { "TheKinBoss" => 0, "WaterfallGiantBoss" => 1, _ => 2 })
                .ThenBy(m => Key(m).StableKey, StringComparer.Ordinal).ToArray(),
            variants.SelectMany(a => a.AllAncients).Concat(ModelDb.AllSharedAncients)
                .DistinctBy(Key).OrderBy(m => Key(m).StableKey, StringComparer.Ordinal).ToArray());
    }
    public static bool IsNative(int index, AbstractModel model)
    {
        if (index >= ModelDb.ActsByIndex.Count) return false;
        var key = Key(model);
        var variants = ModelDb.ActsByIndex[index];
        return model switch {
            EncounterModel => variants.Any(a => a.AllBossEncounters.Any(b => Key(b) == key)),
            AncientEventModel => variants.Any(a => a.AllAncients.Any(a => Key(a) == key)) || (index > 0 && ModelDb.AllSharedAncients.Any(a => Key(a) == key)),
            _ => false
        };
    }
    public static void ValidateChoice(EncounterRule[] rules, EncounterAct[] catalog)
    {
        EncounterRules.Validate(rules);
        if (rules.Length != catalog.Length) throw new InvalidOperationException("SelectMOD: 選択したAct数が一致しません。");
        for (var i = 0; i < rules.Length; i++)
        {
            if (rules[i].Act != Key(catalog[i].Act)) throw new InvalidOperationException("SelectMOD: Actの候補が変わりました。");
            if (rules[i].Boss != null && !catalog[i].Bosses.Any(m => Key(m) == rules[i].Boss)) throw new InvalidOperationException("SelectMOD: ボス候補が変わりました。");
            if (rules[i].Ancient != null && !catalog[i].Ancients.Any(m => Key(m) == rules[i].Ancient)) throw new InvalidOperationException("SelectMOD: エンシェント候補が変わりました。");
        }
    }
}

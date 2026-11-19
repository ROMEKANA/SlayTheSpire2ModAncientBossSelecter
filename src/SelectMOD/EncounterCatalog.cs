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
            Ordered(index, variants.SelectMany(a => a.AllBossEncounters)),
            Ordered(index, variants.SelectMany(a => a.AllAncients).Concat(ModelDb.AllSharedAncients)));
    }

    private static int[] Origins(AbstractModel model) => Enumerable.Range(0, ModelDb.ActsByIndex.Count)
        .Where(i => IsNative(i, model)).ToArray();

    private static T[] Ordered<T>(int index, IEnumerable<T> models) where T : AbstractModel =>
        models.DistinctBy(Key).Select(model => (Model: model, Acts: Origins(model)))
            .OrderBy(x => x.Acts.Contains(index) ? (x.Acts.Length == 1 ? 0 : 1) : x.Acts.Length == 1 ? 2 : 3)
            .ThenBy(x => x.Acts.FirstOrDefault()).ThenBy(x => Key(x.Model).StableKey, StringComparer.Ordinal)
            .Select(x => x.Model).ToArray();

    public static string OriginSuffix(int index, AbstractModel model)
    {
        var acts = Origins(model);
        if (acts.Contains(index) || acts.Length == 0) return "";
        return "[アクト" + string.Join("/", acts.Select(i => i switch { 0 => "Ⅰ", 1 => "Ⅱ", 2 => "Ⅲ", _ => (i + 1).ToString() })) + "]";
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
            if (rules[i].SecondBoss != null && !catalog[i].Bosses.Any(m => Key(m) == rules[i].SecondBoss)) throw new InvalidOperationException("副ボス候補が変わりました。");
            if (rules[i].Boss != null && !catalog[i].Bosses.Any(m => Key(m) == rules[i].Boss)) throw new InvalidOperationException("SelectMOD: ボス候補が変わりました。");
            if (rules[i].Ancient != null && !catalog[i].Ancients.Any(m => Key(m) == rules[i].Ancient)) throw new InvalidOperationException("SelectMOD: エンシェント候補が変わりました。");
        }
    }
}

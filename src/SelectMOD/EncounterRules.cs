using System.Text.Json;

namespace SelectMOD;

public sealed record ModelKey(string Category, string Entry)
{
    public string StableKey => Category + "/" + Entry;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Category) || string.IsNullOrWhiteSpace(Entry) || Category.Length > 128 || Entry.Length > 256)
            throw new InvalidOperationException("SelectMOD: 不正な候補IDです。");
    }
}
public sealed record EncounterRule(int Index, ModelKey Act, ModelKey? Boss, ModelKey? Ancient,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] ModelKey? SecondBoss = null);
public sealed record ResolvedEncounter(int Index, ModelKey Act, ModelKey Boss, ModelKey? SecondBoss, ModelKey? Ancient);

public static class EncounterRules
{
    public const int Schema = 1;
    public const int MaxActs = 16;
    public const int MaxJsonLength = 32768;
    public static string Encode<T>(T value) => JsonSerializer.Serialize(value);
    public static T[] Decode<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxJsonLength)
            throw new InvalidOperationException("SelectMOD: 保存された選択情報が欠落、または大きすぎます。");
        try { return JsonSerializer.Deserialize<T[]>(json) ?? throw new JsonException(); }
        catch (JsonException e) { throw new InvalidOperationException("SelectMOD: 選択情報を読み込めません。", e); }
    }
    public static void Validate(EncounterRule[] rules)
    {
        if (rules.Length is < 1 or > MaxActs) throw new InvalidOperationException("SelectMOD: Act数が不正です。");
        for (var i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];
            if (rule == null || rule.Index != i || rule.Act == null) throw new InvalidOperationException("SelectMOD: Actの対応が不正です。");
            if (rule.SecondBoss != null && i != 2) throw new InvalidOperationException("副ボスはAct 3専用です。");
            rule.SecondBoss?.Validate(); rule.Act.Validate(); rule.Boss?.Validate(); rule.Ancient?.Validate();
        }
    }
    public static bool HasChanges(IEnumerable<EncounterRule> rules) => rules.Any(r => r.Boss != null || r.Ancient != null || r.SecondBoss != null);
    public static ResolvedEncounter Resolve(EncounterRule rule, ResolvedEncounter original, IReadOnlyCollection<ModelKey> bosses, IReadOnlyCollection<ModelKey> ancients)
    {
        if (rule.Act != original.Act || rule.Index != original.Index) throw new InvalidOperationException("SelectMOD: Actが選択時と異なります。");
        if (rule.Boss != null && !bosses.Contains(rule.Boss)) throw new InvalidOperationException("SelectMOD: 指定ボスが候補にありません。");
        if (rule.Ancient != null && !ancients.Contains(rule.Ancient)) throw new InvalidOperationException("SelectMOD: 指定エンシェントが候補にありません。");
        var boss = rule.Boss ?? original.Boss;
        var second = original.SecondBoss;
        // Preserve the standard second-boss draw. If the selected primary was
        // that second boss, swap the original primary into the second slot.
        if (second != null && second == boss) second = original.Boss;
        if (original.SecondBoss != null && rule.SecondBoss != null)
        {
            if (!bosses.Contains(rule.SecondBoss)) throw new InvalidOperationException("副ボス候補が変わりました。");
            second = rule.SecondBoss;
        }
        return original with { Boss = boss, SecondBoss = second, Ancient = rule.Ancient ?? original.Ancient };
    }
}

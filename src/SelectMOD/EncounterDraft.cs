namespace SelectMOD;

// Next-run UI state only. Gameplay uses the immutable modifier captured at start.
public static class EncounterDraft
{
    private static readonly Dictionary<int, (ModelKey? Boss, ModelKey? Ancient)> Choices = new();
    public static (ModelKey? Boss, ModelKey? Ancient) Get(int index) => Choices.GetValueOrDefault(index);
    public static void Set(int index, bool boss, ModelKey? value)
    {
        var old = Get(index);
        Choices[index] = boss ? (value, old.Ancient) : (old.Boss, value);
        ModEntry.WriteLog($"SELECTION_EDIT act={index + 1} target={(boss ? "boss" : "ancient")} value={value?.StableKey ?? "random"}");
    }
    public static ModelKey? SecondBoss { get; set; }
    public static void Reset() { SecondBoss = null; Choices.Clear(); ModEntry.WriteLog("SELECTION_RESET random for all Acts"); }
    public static EncounterRule[] Snapshot(EncounterAct[] catalog, bool doubleBoss = false)
    {
        var rules = catalog.Select(a => new EncounterRule(a.Index, EncounterCatalog.Key(a.Act), Get(a.Index).Boss, Get(a.Index).Ancient, doubleBoss && a.Index == 2 ? SecondBoss : null)).ToArray();
        EncounterCatalog.ValidateChoice(rules, catalog);
        return rules;
    }
}

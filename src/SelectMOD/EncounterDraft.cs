namespace SelectMOD;

// Next-run UI state only. Gameplay uses the immutable modifier captured at start.
public static class EncounterDraft
{
    private static readonly Lazy<EncounterPreferences> Storage = new(() => new(ModEntry.ConfigDirectory));
    public static (ModelKey? Boss, ModelKey? Ancient) Get(int index)
    {
        var choice = Storage.Value.Get(index);
        return (choice.Boss, choice.Ancient);
    }
    public static void Set(int index, bool boss, ModelKey? value)
    {
        Storage.Value.Set(index, boss, value);
        ModEntry.WriteLog($"SELECTION_EDIT act={index + 1} target={(boss ? "boss" : "ancient")} value={value?.StableKey ?? "random"}");
    }
    public static ModelKey? SecondBoss
    {
        get => Storage.Value.Get(2).SecondBoss;
        set => Storage.Value.Set(2, true, value, second: true);
    }
    internal static void RememberHost(LobbyChoice[] choices) => Storage.Value.Save(choices);
    public static void Reset() { Storage.Value.Save([]); ModEntry.WriteLog("SELECTION_RESET random for all Acts"); }
    internal static bool HasSelection(bool doubleBoss) => Storage.Value.HasSelection(doubleBoss);
    public static EncounterRule[] Snapshot(EncounterAct[] catalog, bool doubleBoss = false)
    {
        var rules = catalog.Select(a => new EncounterRule(a.Index, EncounterCatalog.Key(a.Act), Get(a.Index).Boss, Get(a.Index).Ancient, doubleBoss && a.Index == 2 ? SecondBoss : null)).ToArray();
        EncounterCatalog.ValidateChoice(rules, catalog);
        return rules;
    }
}

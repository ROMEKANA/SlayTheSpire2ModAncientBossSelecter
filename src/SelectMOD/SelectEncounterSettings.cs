using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SelectMOD;

public sealed class SelectEncounterSettings : ModifierModel
{
    private int _schema = -1;
    private string _rules = "";
    private string _results = "";
    private bool _frozen;
    private string _multiplayerOrigin = "";
    private int _multiplayerPlayers;
    [SavedProperty] public string SelectMultiplayerOrigin { get => _multiplayerOrigin; set { if (_frozen && value != _multiplayerOrigin) throw new InvalidOperationException("SelectMOD: 開始済みの同期設定は変更できません。"); _multiplayerOrigin = value; } }
    [SavedProperty] public int SelectMultiplayerPlayers { get => _multiplayerPlayers; set { if (_frozen && value != _multiplayerPlayers) throw new InvalidOperationException("SelectMOD: 開始済みの参加人数は変更できません。"); _multiplayerPlayers = value; } }
    [SavedProperty] public int SelectEncounterSchema
    {
        get => _schema;
        set { if (_frozen && value != _schema) throw new InvalidOperationException("SelectMOD: 開始済み設定は変更できません。"); _schema = value; }
    }
    [SavedProperty] public string SelectEncounterRules
    {
        get => _rules;
        set { if (_frozen && value != _rules) throw new InvalidOperationException("SelectMOD: 開始済み設定は変更できません。"); _rules = value; }
    }
    [SavedProperty] public string SelectEncounterResults
    {
        get => _results;
        set { if (_results.Length != 0 && value != _results) throw new InvalidOperationException("SelectMOD: 確定結果は変更できません。"); _results = value; }
    }
    public void Freeze() { ReadRules(); _frozen = true; }
    public override LocString Title => ProbeText.Create("encounter_title", "");
    public override LocString Description => ProbeText.Create("encounter_description", DescribeChoices());

    private string DescribeChoices()
    {
        // The canonical model has no run-specific settings.
        if (string.IsNullOrEmpty(_rules)) return "";
        var lines = new List<string>();
        foreach (var rule in ReadRules())
        {
            var act = $"Act {rule.Index + 1}";
            if (rule.Ancient is { } ancient)
                lines.Add($"{act} エンシェント　{ModelDb.GetById<AncientEventModel>(EncounterCatalog.Id(ancient)).Title.GetFormattedText()}");
            if (rule.Boss is { } boss)
                lines.Add($"{act} 主ボス　{ModelDb.GetById<EncounterModel>(EncounterCatalog.Id(boss)).Title.GetFormattedText()}");
            if (rule.SecondBoss is { } second)
                lines.Add($"{act} 副ボス　{ModelDb.GetById<EncounterModel>(EncounterCatalog.Id(second)).Title.GetFormattedText()}");
        }
        return string.Join("\n", lines);
    }
    public EncounterRule[] ReadRules()
    {
        if (SelectEncounterSchema != EncounterRules.Schema) throw new InvalidOperationException("SelectMOD: 未対応の選択データです。");
        var rules = EncounterRules.Decode<EncounterRule>(SelectEncounterRules);
        EncounterRules.Validate(rules);
        return rules;
    }
    protected override void AfterRunCreated(RunState runState)
    {
        ProbeRules.ValidateRun(runState);
        ModEntry.WriteLog("ENCOUNTER_RUN_CREATED selection validated; results pending standard generation");
    }
    protected override void AfterRunLoaded(RunState runState)
    {
        ProbeRules.ValidateRun(runState);
        EncounterRuntime.ValidateLoaded(runState, this);
        Freeze();
        ModEntry.WriteLog("ENCOUNTER_RUN_LOADED saved results verified; no reroll");
    }
}

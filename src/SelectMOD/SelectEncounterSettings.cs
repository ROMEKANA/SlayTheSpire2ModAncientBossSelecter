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
    public override LocString Title => ProbeText.Create("encounter_title", "SelectMOD ボス・エンシェント選択");
    public override LocString Description => ProbeText.Create("encounter_description", "キャラクター選択画面で指定した出現先を使います。確定結果はこのランに保存されます（ソロ用）。");
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

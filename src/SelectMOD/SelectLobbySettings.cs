using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SelectMOD;

// Transient standard-lobby transport. Replaced with SelectEncounterSettings
// before creating a run; never persisted as a playable run modifier.
public sealed class SelectLobbySettings : ModifierModel
{
    private string _payload = "";
    private bool _frozen;
    [SavedProperty] public string SelectLobbyPayload { get => _payload; set { if (_frozen && value != _payload) throw new InvalidOperationException("SelectMOD: 配信済み設定は変更できません。"); _payload = value; } }
    public void Freeze() { Read(); _frozen = true; }
    public LobbyEnvelope Read() => MultiplayerContract.Read(SelectLobbyPayload);
    public override LocString Title => ProbeText.Create("lobby_title", "SelectMOD ホストの選択設定");
    public override LocString Description => ProbeText.Create("lobby_description", "ホストが編集するマルチ設定。開始時にランへ固定します。");
}

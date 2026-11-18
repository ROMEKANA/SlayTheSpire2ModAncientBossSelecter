using System.Security.Cryptography;
using System.Text;
using MegaCrit.Sts2.Core.Models;

namespace SelectMOD;

public sealed record LobbyChoice(int Index, ModelKey? Boss, ModelKey? Ancient);
public sealed record LobbyDraft(int Protocol, string Session, int Revision, string Catalog, int Ascension, LobbyChoice[] Choices);
public sealed record LobbyEnvelope(LobbyDraft Draft, string Digest);

public static class MultiplayerContract
{
    public const int Protocol = 1;
    public const int MaxPlayers = 4;
    public const string Version = "SelectMOD/0.6.0/fixed-v1";
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static string CatalogDigest()
    {
        var options = EncounterCatalog.BuildOptions();
        return Hash(EncounterRules.Encode(new { Version, Acts = ModelDb.ActsByIndex.Select((group, index) => new {
            Index = index, Ids = group.Select(EncounterCatalog.Key).OrderBy(k => k.StableKey, StringComparer.Ordinal).ToArray(),
            Bosses = options[index].Bosses.Select(EncounterCatalog.Key).OrderBy(k => k.StableKey, StringComparer.Ordinal).ToArray(),
            Ancients = options[index].Ancients.Select(EncounterCatalog.Key).OrderBy(k => k.StableKey, StringComparer.Ordinal).ToArray()
        }).ToArray() }));
    }
    public static LobbyEnvelope Create(string session, int revision, LobbyChoice[] choices, int ascension = 0)
    {
        var draft = new LobbyDraft(Protocol, session, revision, CatalogDigest(), ascension, choices.ToArray());
        var envelope = new LobbyEnvelope(draft, Hash(EncounterRules.Encode(draft)));
        Validate(envelope);
        return envelope;
    }
    public static LobbyEnvelope Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > EncounterRules.MaxJsonLength) throw new InvalidOperationException("SelectMOD: マルチ設定が欠落／大きすぎます。");
        LobbyEnvelope envelope;
        try { envelope = System.Text.Json.JsonSerializer.Deserialize<LobbyEnvelope>(json) ?? throw new System.Text.Json.JsonException(); }
        catch (System.Text.Json.JsonException e) { throw new InvalidOperationException("SelectMOD: マルチ設定を読み込めません。", e); }
        Validate(envelope);
        if (EncounterRules.Encode(envelope) != json) throw new InvalidOperationException("SelectMOD: マルチ設定の形式が一致しません。");
        return envelope;
    }
    private static void Validate(LobbyEnvelope envelope)
    {
        var draft = envelope.Draft;
        if (draft == null || draft.Protocol != Protocol || !Guid.TryParseExact(draft.Session, "N", out _) || draft.Revision < 1 || draft.Ascension is < 0 or > 10 ||
            draft.Choices == null || draft.Choices.Length is < 1 or > EncounterRules.MaxActs ||
            envelope.Digest != Hash(EncounterRules.Encode(draft))) throw new InvalidOperationException("SelectMOD: マルチ設定の版／整合性が一致しません。");
        if (draft.Catalog != CatalogDigest()) throw new InvalidOperationException("SelectMOD: 候補一覧／MOD版がホストと異なります。全員のゲーム・MOD構成を揃えてください。");
        var options = EncounterCatalog.BuildOptions();
        if (draft.Choices.Length != options.Length) throw new InvalidOperationException("SelectMOD: マルチ設定のAct数が一致しません。");
        var rules = draft.Choices.Select((choice, i) => choice == null ? throw new InvalidOperationException("SelectMOD: 選択が欠落しています。")
            : new EncounterRule(choice.Index, EncounterCatalog.Key(options[i].Act), choice.Boss, choice.Ancient)).ToArray();
        EncounterCatalog.ValidateChoice(rules, options);
    }
    public static EncounterRule[] Bind(LobbyEnvelope envelope, IReadOnlyList<ActModel> acts)
    {
        Validate(envelope);
        if (envelope.Draft.Choices.Length != acts.Count) throw new InvalidOperationException("SelectMOD: 開始時のAct数が変わりました。");
        return acts.Select((act, i) => new EncounterRule(i, EncounterCatalog.Key(act), envelope.Draft.Choices[i].Boss, envelope.Draft.Choices[i].Ancient)).ToArray();
    }
}

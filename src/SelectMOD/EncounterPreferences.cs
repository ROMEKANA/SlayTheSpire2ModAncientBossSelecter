using System.Text.Json;

namespace SelectMOD;

// Local next-run choices. Network packets and saved runs never read this file on receipt.
internal sealed class EncounterPreferences
{
    private readonly string path;
    private LobbyChoice[] choices;

    internal EncounterPreferences(string directory)
    {
        path = Path.Combine(directory, "encounter-choices.json");
        choices = Load();
    }

    internal bool HasSelection(bool doubleBoss) => choices.Any(c => c.Boss != null || c.Ancient != null || (doubleBoss && c.SecondBoss != null));

    internal LobbyChoice Get(int index) => choices.FirstOrDefault(c => c.Index == index) ?? new(index, null, null);

    private LobbyChoice[] Load()
    {
        if (!File.Exists(path)) return [];
        try
        {
            var json = File.ReadAllText(path);
            if (json.Length > EncounterRules.MaxJsonLength) throw new InvalidDataException("Oversized encounter preferences");
            var saved = JsonSerializer.Deserialize<LobbyChoice[]>(json) ?? throw new InvalidDataException("Empty encounter preferences");
            if (saved.Length > EncounterRules.MaxActs || saved.Any(c => c == null || c.Index < 0 || c.Index >= EncounterRules.MaxActs)
                || saved.DistinctBy(c => c.Index).Count() != saved.Length) throw new InvalidDataException("Invalid encounter preference rows");
            if (saved.Length == 0) return [];
            var catalog = EncounterCatalog.BuildOptions();
            // A removed content mod must not strand the character screen on a missing item.
            return saved.Where(c => c.Index < catalog.Length).Select(c => {
                var act = catalog[c.Index];
                return c with {
                    Boss = act.Bosses.Any(b => EncounterCatalog.Key(b) == c.Boss) ? c.Boss : null,
                    Ancient = act.Ancients.Any(a => EncounterCatalog.Key(a) == c.Ancient) ? c.Ancient : null,
                    SecondBoss = c.Index == 2 && act.Bosses.Any(b => EncounterCatalog.Key(b) == c.SecondBoss) ? c.SecondBoss : null
                };
            }).ToArray();
        }
        catch (Exception e) when (e is InvalidDataException or JsonException)
        {
            var backup = path + ".invalid-" + Guid.NewGuid().ToString("N");
            File.Move(path, backup);
            ModEntry.WriteLog("ENCOUNTER_PREFS_INVALID retained " + Path.GetFileName(backup));
            return [];
        }
    }

    internal void Set(int index, bool boss, ModelKey? key, bool second = false)
    {
        var old = Get(index);
        var next = second ? old with { SecondBoss = key } : boss ? old with { Boss = key } : old with { Ancient = key };
        Save(choices.Where(c => c.Index != index).Append(next));
    }

    internal void Save(IEnumerable<LobbyChoice> next)
    {
        var selected = next.Where(c => c.Boss != null || c.Ancient != null || c.SecondBoss != null).OrderBy(c => c.Index).ToArray();
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(temp, EncounterRules.Encode(selected));
            File.Move(temp, path, true);
            choices = selected;
        }
        finally { File.Delete(temp); }
    }
}

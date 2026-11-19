using MegaCrit.Sts2.Core.Models;

namespace SelectMOD;

internal static class AscensionPreference
{
    private static string Filename => Path.Combine(ModEntry.ConfigDirectory, "ascension-mask.txt");
    internal static int Read()
    {
        if (!File.Exists(Filename)) return AscensionSettings.All;
        if (!int.TryParse(File.ReadAllText(Filename), out var mask) || mask is < 0 or > AscensionSettings.All)
            throw new InvalidDataException("SelectMOD: ascension-mask.txt の値が不正です。");
        return mask;
    }

    internal static void Write(int mask)
    {
        new AscensionSettings(AscensionSettings.CurrentSchema, mask).Validate();
        Directory.CreateDirectory(ModEntry.ConfigDirectory);
        var temp = Filename + "." + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temp, mask.ToString()); File.Move(temp, Filename, true); }
        finally { File.Delete(temp); }
    }

    internal static int Stack(int level) => level is >= 0 and <= 10
        ? (1 << level) - 1 : throw new InvalidDataException("SelectMOD: 未対応のアセンションレベルです。");

    internal static IReadOnlyList<ModifierModel> Apply(IReadOnlyList<ModifierModel> modifiers, int level, int preference)
    {
        var stack = Stack(level);
        var mask = stack & preference;
        if (mask == stack) return modifiers;
        var model = (SelectAscensionSettings)ModelDb.Modifier<SelectAscensionSettings>().ToMutable();
        model.SelectAscensionSchema = AscensionSettings.CurrentSchema;
        model.SelectAscensionMask = mask;
        return modifiers.Append(model).ToArray();
    }
}

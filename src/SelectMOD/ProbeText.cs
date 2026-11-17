using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace SelectMOD;

// Only LocString objects created by this MOD use literal probe labels.
// No game localization tables or third-party strings are replaced.
public static class ProbeText
{
    private static readonly ConditionalWeakTable<LocString, Label> Labels = new();
    private sealed record Label(string Text);
    public static LocString Create(string key, string text)
    {
        var value = new LocString("modifiers", "SELECTMOD_SETTINGS_PROBE." + key);
        Labels.Add(value, new Label(text));
        return value;
    }
    public static bool TryGet(LocString value, out string text)
    {
        if (Labels.TryGetValue(value, out var label)) { text = label.Text; return true; }
        text = "";
        return false;
    }
}

[HarmonyPatch]
internal static class ProbeLabelPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Runtime overload discovery avoids guessing optional formatting parameters.
        var methods = typeof(LocString).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "GetFormattedText" && m.ReturnType == typeof(string)).ToArray();
        if (methods.Length == 0) throw new MissingMethodException("LocString.GetFormattedText");
        return methods;
    }
    private static bool Prefix(LocString __instance, ref string __result)
    {
        if (!ProbeText.TryGet(__instance, out var text)) return true;
        __result = text;
        return false;
    }
}

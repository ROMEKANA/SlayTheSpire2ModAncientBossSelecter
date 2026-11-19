using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace SelectMOD;

internal static class AscensionIcon
{
    private static Texture2D? texture;
    internal static void Install(Harmony harmony)
    {
        // Optional presentation hook. A renamed getter must not prevent gameplay startup.
        var getter = AccessTools.PropertyGetter(typeof(ModifierModel), "Icon");
        if (getter == null || getter.ReturnType != typeof(Texture2D))
        {
            ModEntry.WriteLog("ASCENSION_ICON unavailable: no Texture2D Icon getter");
            return;
        }
        harmony.Patch(getter, prefix: new HarmonyMethod(typeof(AscensionIcon), nameof(GetIcon)));
    }

    private static bool GetIcon(ModifierModel __instance, ref Texture2D __result)
    {
        if (__instance is not SelectAscensionSettings) return true;
        if (texture == null)
        {
            const string svg = """
                <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">
                  <path fill="#12375e" stroke="#b3edff" stroke-width="2.5" d="M30 60C11 60 5 45 13 31c1 7 4 9 6 9-3-15 11-23 12-37 12 11 15 20 12 31 4-2 6-6 7-12 15 21 8 38-20 38Z"/>
                  <path fill="#279ffa" d="M31 57c-12 0-17-9-13-18 2 4 5 6 8 6-3-10 7-17 9-26 7 11 8 18 5 24 4-1 7-5 9-9 4 13-3 23-18 23Z"/>
                  <path fill="#bdfaff" d="M32 55c-7 0-10-5-8-11 2 3 4 3 6 3-1-5 4-9 6-15 7 11 7 22-4 23Z"/>
                </svg>
                """;
            using var image = new Image();
            if (image.LoadSvgFromString(svg) != Error.Ok) return true;
            texture = ImageTexture.CreateFromImage(image);
        }
        __result = texture;
        return false;
    }
}

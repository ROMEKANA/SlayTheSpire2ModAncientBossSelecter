using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;

namespace SelectMOD;

[HarmonyPatch]
internal static class NeowRewardPatch
{
    private static MethodBase TargetMethod() => AccessTools.DeclaredMethod(typeof(Neow), "GenerateInitialOptions", Type.EmptyTypes)
        ?? throw new MissingMethodException("Neow.GenerateInitialOptions");
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var replacement = AccessTools.Method(typeof(NeowRewardPatch), nameof(RewardModifiers));
        var code = new List<CodeInstruction>();
        var matched = 0;
        foreach (var instruction in instructions)
        {
            code.Add(instruction);
            if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                instruction.operand is MethodInfo getter && getter.Name == "get_Modifiers" &&
                getter.ReturnType == typeof(IReadOnlyList<ModifierModel>) &&
                getter.DeclaringType?.IsAssignableFrom(typeof(RunState)) == true)
            {
                // Preserve the actual getter and receiver (RunState or its
                // interface). Transform only its returned list in this method;
                // the run's persistence/combat modifier list is never mutated.
                code.Add(new CodeInstruction(OpCodes.Call, replacement));
                matched++;
            }
        }
        if (matched == 0) throw new InvalidOperationException("SelectMOD: Neow reward branch changed; update source evidence before continuing.");
        ModEntry.WriteLog($"NEOW_REWARD_PATCH modifier reads={matched}");
        return code;
    }
    internal static IReadOnlyList<ModifierModel> RewardModifiers(IReadOnlyList<ModifierModel> modifiers)
    {
        if (!ModEntry.IsActive || !modifiers.Any(IsStorageOnly)) return modifiers;
        var visible = modifiers.Where(m => !IsStorageOnly(m)).ToArray();
        ModEntry.WriteLog($"NEOW_REWARD_VIEW excluded SelectMOD storage; remaining={visible.Length}");
        return visible;
    }
    private static bool IsStorageOnly(ModifierModel model) => model is SelectEncounterSettings or SelectSettingsProbe or SelectAscensionSettings;
}

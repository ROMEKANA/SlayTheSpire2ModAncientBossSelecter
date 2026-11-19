using System.Reflection;
using Environment = System.Environment;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;

namespace SelectMOD;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public const string Owner = "ROMEKANA.SelectMOD";
    public static bool IsActive { get; private set; }
    public static string ConfigDirectory
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("SELECTMOD_CONFIG_DIR");
            if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
            var pathFile = Path.Combine(Path.GetDirectoryName(typeof(ModEntry).Assembly.Location)!, "SelectMOD.paths.txt");
            return File.Exists(pathFile) ? Path.GetFullPath(File.ReadAllText(pathFile).Trim())
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SelectMOD");
        }
    }

    public static void Initialize()
    {
        var harmony = new Harmony(Owner);
        try
        {
            VerifyReferences();
            // Never touch ModelDb or its caches while the loader is still adding this assembly.
            harmony.PatchAll(typeof(ModEntry).Assembly);
            AscensionIcon.Install(harmony);
            EncounterSelectionPanel.Install();
            IsActive = true;
            WriteLog("INITIALIZED version=0.11.0 normal/custom selectable ascension; host-owned multiplayer settings");
        }
        catch (Exception e)
        {
            IsActive = false;
            EncounterSelectionPanel.Uninstall();
            foreach (var method in Harmony.GetAllPatchedMethods().ToArray())
                harmony.Unpatch(method, HarmonyPatchType.All, Owner);
            WriteLog("INITIALIZATION_FAILED " + e.Message);
            GD.PushError("SelectMOD初期化失敗: " + e.Message);
            throw;
        }
    }

    private static void VerifyReferences()
    {
        using var stream = typeof(ModEntry).Assembly.GetManifestResourceStream("SelectMOD.reviewed-builds.json")
            ?? throw new InvalidOperationException("Missing reference policy");
        using var json = JsonDocument.Parse(stream);
        var directory = Path.GetDirectoryName(typeof(RunManager).Assembly.Location)!;
        foreach (var reference in json.RootElement.GetProperty("Assemblies").EnumerateObject())
        {
            var bytes = File.ReadAllBytes(Path.Combine(directory, reference.Name));
            if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(reference.Value.GetString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("未確認のゲームDLLです: " + reference.Name);
        }
    }

    public static int ReadNextValue()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(ConfigDirectory, "config.json")));
        var value = json.RootElement.GetProperty("ProbeValue").GetInt32();
        new ProbeSettings(ProbeSettings.CurrentSchema, value).Validate();
        return value;
    }

    public static bool ReadSelectionDialog()
    {
        // Workshop subscribers have no installer-generated config; keep the panel available.
        var path = Path.Combine(ConfigDirectory, "config.json");
        if (!File.Exists(path)) return true;
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        return !json.RootElement.TryGetProperty("SelectionDialog", out var field) || field.GetBoolean();
    }

    public static void WriteLog(string message)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var path = Path.Combine(ConfigDirectory, "selectmod.log");
        if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024)
            File.Move(path, path + ".previous", overwrite: true);
        File.AppendAllText(path, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
    }

    public static void Reject(string message)
    {
        WriteLog("START_BLOCKED " + message);
        GD.PushWarning(message);
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            var dialog = new AcceptDialog { Title = "SelectMOD", DialogText = message };
            tree.Root.AddChild(dialog);
            dialog.Confirmed += dialog.QueueFree;
            dialog.CloseRequested += dialog.QueueFree;
            dialog.PopupCentered();
        }
    }
}

using System;
using System.IO;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using SealHunter.Combat;

namespace SealHunter.IPC;

/// <summary>
/// BossMod Reborn autorotation backend, adapted from Questionable's BossModIpc.
/// A "preset" (bundled Overworld JSON, Targeting: Manual for all jobs) is created if missing,
/// then activated; SealHunter sets the target, BossMod kills it. Requires BossMod Reborn
/// (the preset references its xan/Veyn autorotation modules and the Presets.* IPC).
/// </summary>
public class BossModIPC : ICombatBackend
{
    private const string PluginName = "BossMod";
    private const string PresetName = "SealHunter";
    private const string PresetResource = "SealHunter.Combat.BossModPreset_Overworld.json";

    private readonly ICallGateSubscriber<string, string?> getPreset;
    private readonly ICallGateSubscriber<string, bool, bool> createPreset;
    private readonly ICallGateSubscriber<string, bool> setPreset;
    private readonly ICallGateSubscriber<bool> clearPreset;

    private bool active;

    public BossModIPC()
    {
        getPreset = Plugin.PluginInterface.GetIpcSubscriber<string, string?>($"{PluginName}.Presets.Get");
        createPreset = Plugin.PluginInterface.GetIpcSubscriber<string, bool, bool>($"{PluginName}.Presets.Create");
        setPreset = Plugin.PluginInterface.GetIpcSubscriber<string, bool>($"{PluginName}.Presets.SetActive");
        clearPreset = Plugin.PluginInterface.GetIpcSubscriber<bool>($"{PluginName}.Presets.ClearActive");
    }

    public string Name => "BossMod Reborn";

    public bool Installed
    {
        get
        {
            try
            {
                return getPreset.HasFunction;
            }
            catch (IpcError)
            {
                return false;
            }
        }
    }

    public void Enable()
    {
        try
        {
            // Keep the preset active across consecutive kills, and allow non-encounter combat.
            Plugin.CommandManager.ProcessCommand("/vbm cfg Autorotation ClearPresetOnCombatEnd false");
            Plugin.CommandManager.ProcessCommand("/vbm cfg ZoneModuleConfig EnableQuestBattles true");

            if (getPreset.InvokeFunc(PresetName) == null)
                createPreset.InvokeFunc(LoadPreset(), true);

            setPreset.InvokeFunc(PresetName);
            active = true;
        }
        catch (IpcError e)
        {
            Plugin.Logger.Warning(e, "BossMod: could not enable autorotation");
            active = false;
        }
    }

    public void Disable()
    {
        try
        {
            clearPreset.InvokeFunc();
        }
        catch (IpcError e)
        {
            Plugin.Logger.Warning(e, "BossMod: could not clear preset");
        }
        active = false;
    }

    public bool IsActive() => active;

    private static string LoadPreset()
    {
        using var stream = typeof(BossModIPC).Assembly.GetManifestResourceStream(PresetResource)
            ?? throw new InvalidOperationException($"Embedded preset {PresetResource} not found");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

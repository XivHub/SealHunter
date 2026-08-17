using System;
using System.IO;
using System.Text.Json.Nodes;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using SealHunter.Combat;
using SealHunter.Helpers;

namespace SealHunter.IPC;

/// <summary>
/// BossMod Reborn autorotation backend, adapted from Questionable's BossModIpc.
/// A "preset" (bundled Overworld JSON, Targeting: Manual for all jobs) is written and activated;
/// SealHunter sets the target, BossMod kills it. Requires BossMod Reborn (the preset references its
/// xan/Veyn autorotation modules and the Presets.* IPC).
/// The preset is built per activation: BossMod's own pathfinder (MiscAI.NormalMovement) is included
/// only for jobs that have to close the distance, so a ranged job holds the position SealHunter
/// walked it to instead of running into melee.
/// </summary>
public class BossModIPC : ICombatBackend
{
    private const string PluginName = "BossMod";
    private const string PresetName = "SealHunter";
    private const string PresetResource = "SealHunter.Combat.BossModPreset_Overworld.json";
    private const string MovementModule = "BossMod.Autorotation.MiscAI.NormalMovement";

    private readonly ICallGateSubscriber<string, string?> getPreset;
    private readonly ICallGateSubscriber<string, bool, bool> createPreset;
    private readonly ICallGateSubscriber<string, bool> setPreset;
    private readonly ICallGateSubscriber<bool> clearPreset;

    private bool active;

    /// <summary>Which variant of the preset BossMod currently holds, or null when it has not been
    /// written this session. Null on the first activation forces a rewrite, so a preset left behind
    /// by an older version is replaced rather than reused.</summary>
    private bool? presetHasMovement;

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
            // BossMod's AI mode does its own targeting and movement; SealHunter owns both, so only
            // the preset's autorotation modules may run.
            Plugin.CommandManager.ProcessCommand("/vbmai off");
            // Keep the preset active across consecutive kills, and allow non-encounter combat.
            Plugin.CommandManager.ProcessCommand("/vbm cfg Autorotation ClearPresetOnCombatEnd false");
            Plugin.CommandManager.ProcessCommand("/vbm cfg ZoneModuleConfig EnableQuestBattles true");

            var wantMovement = WantsMovement();
            if (presetHasMovement != wantMovement || getPreset.InvokeFunc(PresetName) == null)
            {
                createPreset.InvokeFunc(BuildPreset(wantMovement), true);
                presetHasMovement = wantMovement;
            }

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

    public bool MovesPlayer => active && presetHasMovement == true;

    /// <summary>Whether the active preset should carry BossMod's pathfinder. On Auto only the jobs
    /// that have to be in melee get it; a ranged job that is already in range must not walk in.</summary>
    private static bool WantsMovement() => Plugin.C.BossModMovement switch
    {
        BossModMovementMode.Always => true,
        BossModMovementMode.Never => false,
        _ => !CombatRange.IsRanged,
    };

    private static string BuildPreset(bool withMovement)
    {
        var preset = JsonNode.Parse(LoadPreset())
            ?? throw new InvalidOperationException($"Embedded preset {PresetResource} is not valid JSON");
        preset["Name"] = PresetName;
        if (!withMovement)
            preset["Modules"]?.AsObject().Remove(MovementModule);
        return preset.ToJsonString();
    }

    private static string LoadPreset()
    {
        using var stream = typeof(BossModIPC).Assembly.GetManifestResourceStream(PresetResource)
            ?? throw new InvalidOperationException($"Embedded preset {PresetResource} not found");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

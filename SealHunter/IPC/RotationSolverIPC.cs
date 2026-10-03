using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using SealHunter.Combat;

namespace SealHunter.IPC;

/// <summary>
/// RotationSolver Reborn backend, adapted from Questionable's RotationSolverRebornModule.
/// Selectable as an alternative to BossMod. SealHunter picks the target and owns movement; RSR in
/// Manual mode only presses buttons on the current target.
/// Manual mode alone does not make an idle mob attackable: RSR's per-job "Engage settings"
/// (<c>HostileType</c>, default <c>AllTargetsWhenSoloInDuty</c>) drop any mob that has no target of
/// its own outside a duty, so RSR stands next to an unpulled overworld mob doing nothing. A NameId on
/// RSR's priority list passes <c>ObjectHelper.IsAttackable</c> before that setting is read, so the hunt
/// mob is added there for the fight and removed after.
/// </summary>
public class RotationSolverIPC : ICombatBackend
{
    private enum StateCommandType : byte
    {
        Off,
        Auto,
        TargetOnly,
        Manual,
    }

    private readonly ICallGateSubscriber<uint, object> addPriorityNameId;
    private readonly ICallGateSubscriber<uint, object> removePriorityNameId;

    /// <summary>The NameId this backend put on RSR's priority list, so exactly that one comes off.</summary>
    private uint? prioritized;

    private readonly ICallGateSubscriber<string, object> test;
    private readonly ICallGateSubscriber<StateCommandType, object> changeOperatingMode;
    private readonly ICallGateSubscriber<bool> autorotationActive;

    private bool active;

    public RotationSolverIPC()
    {
        test = Plugin.PluginInterface.GetIpcSubscriber<string, object>("RotationSolverReborn.Test");
        changeOperatingMode = Plugin.PluginInterface.GetIpcSubscriber<StateCommandType, object>("RotationSolverReborn.ChangeOperatingMode");
        autorotationActive = Plugin.PluginInterface.GetIpcSubscriber<bool>("RotationSolverReborn.AutorotationActive");
        addPriorityNameId = Plugin.PluginInterface.GetIpcSubscriber<uint, object>("RotationSolverReborn.AddPriorityNameID");
        removePriorityNameId = Plugin.PluginInterface.GetIpcSubscriber<uint, object>("RotationSolverReborn.RemovePriorityNameID");
    }

    public string Name => "RotationSolver Reborn";

    public bool Installed
    {
        get
        {
            try
            {
                // HasAction alone only says a gate was registered; the Test call is RSR's own
                // "am I callable" probe and is what Questionable uses.
                if (!changeOperatingMode.HasAction)
                    return false;
                test.InvokeAction("SealHunter probe");
                return true;
            }
            catch (IpcError)
            {
                return false;
            }
        }
    }

    public void Enable(uint targetNameId)
    {
        try
        {
            if (prioritized != targetNameId)
            {
                ReleasePriority();
                addPriorityNameId.InvokeAction(targetNameId);
                prioritized = targetNameId;
            }
            changeOperatingMode.InvokeAction(StateCommandType.Manual);
            active = true;
            Plugin.Telemetry?.Log($"rsr: enable Manual priority={targetNameId} -> reports {ReportedState()}");
        }
        catch (IpcError e)
        {
            Plugin.Logger.Warning(e, "RSR: could not enable autorotation");
            active = false;
        }
    }

    public void Disable()
    {
        try
        {
            if (changeOperatingMode.HasAction)
                changeOperatingMode.InvokeAction(StateCommandType.Off);
        }
        catch (IpcError e)
        {
            Plugin.Logger.Warning(e, "RSR: could not disable autorotation");
        }
        ReleasePriority();
        active = false;
    }

    private void ReleasePriority()
    {
        if (prioritized is not { } id)
            return;
        try
        {
            removePriorityNameId.InvokeAction(id);
        }
        catch (IpcError e)
        {
            Plugin.Logger.Warning(e, "RSR: could not remove priority NameID {0}", id);
        }
        prioritized = null;
    }

    public bool IsActive() => active;

    /// <summary>RSR only presses buttons; SealHunter keeps itself in range.</summary>
    public bool MovesPlayer => false;

    public string ReportedState()
    {
        try
        {
            return autorotationActive.InvokeFunc() ? "on" : "off";
        }
        catch (IpcError e)
        {
            return $"err:{e.GetType().Name}";
        }
    }
}

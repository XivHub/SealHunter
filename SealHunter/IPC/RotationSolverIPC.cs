using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using SealHunter.Combat;

namespace SealHunter.IPC;

/// <summary>
/// RotationSolver Reborn backend, adapted from Questionable's RotationSolverRebornModule.
/// Selectable as an alternative to BossMod. Manual mode bypasses RSR's own engage settings and
/// attacks whatever is targeted, which is exactly the split SealHunter wants: it picks the target
/// and owns movement, RSR only presses buttons.
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

    private readonly ICallGateSubscriber<string, object> test;
    private readonly ICallGateSubscriber<StateCommandType, object> changeOperatingMode;

    private bool active;

    public RotationSolverIPC()
    {
        test = Plugin.PluginInterface.GetIpcSubscriber<string, object>("RotationSolverReborn.Test");
        changeOperatingMode = Plugin.PluginInterface.GetIpcSubscriber<StateCommandType, object>("RotationSolverReborn.ChangeOperatingMode");
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

    public void Enable()
    {
        try
        {
            changeOperatingMode.InvokeAction(StateCommandType.Manual);
            active = true;
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
        active = false;
    }

    public bool IsActive() => active;

    /// <summary>RSR only presses buttons; SealHunter keeps itself in range.</summary>
    public bool MovesPlayer => false;
}

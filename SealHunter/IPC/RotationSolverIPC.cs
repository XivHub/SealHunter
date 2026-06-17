using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using SealHunter.Combat;

namespace SealHunter.IPC;

/// <summary>
/// Optional RotationSolver Reborn backend, adapted from Questionable's RotationSolverRebornModule.
/// Alternative to BossMod; not used on the default path. ChangeOperatingMode(Manual) starts
/// autorotation on the manually-set target.
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
}

using System;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using ZhyraPluginKit;

namespace SealHunter.IPC;

/// <summary>
/// Optional AutoDuty integration to clear a dungeon (which kills its duty-bound hunting-log marks).
/// Duty support / Trust mode is whatever the user has configured inside AutoDuty itself.
/// IPC names are UNVERIFIED — confirm against the installed AutoDuty build.
/// </summary>
public class AutoDutyIPC
{
    public const string Name = "AutoDuty";

    private readonly ICallGateSubscriber<uint, int, bool, object> run; // (territoryType, loops, bareMode)
    private readonly ICallGateSubscriber<object> stop;
    private readonly ICallGateSubscriber<bool> isStopped;

    public AutoDutyIPC()
    {
        run = Plugin.PluginInterface.GetIpcSubscriber<uint, int, bool, object>("AutoDuty.Run");
        stop = Plugin.PluginInterface.GetIpcSubscriber<object>("AutoDuty.Stop");
        isStopped = Plugin.PluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsStopped");
    }

    public static bool Installed => PluginPresence.IsInstalled(Name);

    /// <summary>Run the duty whose instance territory is <paramref name="territoryType"/> once.</summary>
    public bool Run(uint territoryType)
    {
        try
        {
            run.InvokeAction(territoryType, 1, false);
            return true;
        }
        catch (IpcError e)
        {
            Plugin.Logger.Warning(e, "AutoDuty.Run failed");
            return false;
        }
    }

    public void Stop()
    {
        try { stop.InvokeAction(); }
        catch (IpcError) { }
    }

    public bool IsStopped()
    {
        try { return isStopped.InvokeFunc(); }
        catch (IpcError) { return true; }
    }
}

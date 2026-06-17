using System.Numerics;
using ECommons.Automation.NeoTaskManager;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using SealHunter.Helpers;
using SealHunter.IPC;

namespace SealHunter.Scheduler.Tasks;

/// <summary>Teleport to the target's zone, mount, and navigate to the camp hint.</summary>
public static class Task_Travel
{
    private const float MountDistance = 30f;

    public static void Enqueue()
    {
        var entry = SchedulerMain.Current;
        if (entry == null)
        {
            SchedulerMain.State = BotState.NextTarget;
            return;
        }

        var loc = entry.Location;
        var tm = Plugin.TaskManager;

        tm.Enqueue(() =>
        {
            if (Plugin.ClientState.TerritoryType == loc.Terri)
                return true;

            // Lifestream owns the teleport; wait while it's working, issue once otherwise.
            if (Plugin.Teleport.IsBusy())
                return false;

            if (TeleportIPC.Installed && EzThrottler.Throttle("SH.Teleport", 2000))
            {
                var aetheryte = AetheryteResolver.NearestAetheryte(loc.Map, loc.Terri, loc.MapCoords);
                Plugin.Telemetry?.Log($"teleport: aetheryte={aetheryte} -> terri {loc.Terri}");
                if (aetheryte != 0)
                    Plugin.Teleport.Teleport(aetheryte, 0);
            }
            return false;
        }, "Teleport to zone", new TaskManagerConfiguration { TimeLimitMS = 30000 });

        tm.Enqueue(() => Plugin.ClientState.TerritoryType == loc.Terri && !Plugin.Teleport.IsBusy() && !Player.IsBusy && Plugin.Navmesh.IsReady(),
            "Wait for arrival + navmesh", new TaskManagerConfiguration { TimeLimitMS = 30000 });

        tm.Enqueue(() =>
        {
            SchedulerMain.CurrentHint = MapCoords.WorldHint(loc.Terri, loc.Map, loc.xCoord, loc.yCoord);
            SchedulerMain.State = BotState.Navigating;
            if (Plugin.C.UseMount && Vector3.Distance(Player.Position, SchedulerMain.CurrentHint) > MountDistance)
                MountHelper.Mount();
            Plugin.Navmesh.PathfindAndMoveTo(SchedulerMain.CurrentHint, false);
            EzThrottler.Throttle("SH.RepathTravel", 3000); // prime: don't re-issue immediately
            Plugin.Telemetry?.Log($"travel: pathfind to camp hint=({SchedulerMain.CurrentHint.X:0},{SchedulerMain.CurrentHint.Y:0},{SchedulerMain.CurrentHint.Z:0}) dist={Vector3.Distance(Player.Position, SchedulerMain.CurrentHint):0}");
            return true;
        }, "Start pathfind to camp");

        tm.Enqueue(() =>
        {
            if (Vector3.Distance(Player.Position, SchedulerMain.CurrentHint) <= Plugin.C.MobSearchRadius)
            {
                Plugin.Navmesh.Stop();
                SchedulerMain.State = BotState.Locating;
                return true;
            }
            // Let the single pathfind run. Only nudge it if the navmesh genuinely stalled (idle),
            // and then at most once every few seconds — never re-issue every frame.
            if (!Plugin.Navmesh.PathfindInProgress() && !Plugin.Navmesh.IsRunning()
                && EzThrottler.Throttle("SH.RepathTravel", 3000))
                Plugin.Navmesh.PathfindAndMoveTo(SchedulerMain.CurrentHint, false);
            return false;
        }, "Travel to camp", new TaskManagerConfiguration { TimeLimitMS = 180000 });
    }
}

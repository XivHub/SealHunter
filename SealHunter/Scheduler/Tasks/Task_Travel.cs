using System.Numerics;
using ECommons.Automation.NeoTaskManager;
using ECommons.GameHelpers;
using SealHunter.Helpers;

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

            if (Plugin.Teleport.Installed)
            {
                var aetheryte = AetheryteResolver.NearestAetheryte(loc.Map, loc.Terri, loc.MapCoords);
                if (aetheryte != 0)
                    Plugin.Teleport.Teleport(aetheryte);
            }
            return false;
        }, "Teleport to zone", new TaskManagerConfiguration { TimeLimitMS = 30000 });

        tm.Enqueue(() => Plugin.ClientState.TerritoryType == loc.Terri && !Player.IsBusy && Plugin.Navmesh.IsReady(),
            "Wait for arrival + navmesh", new TaskManagerConfiguration { TimeLimitMS = 30000 });

        tm.Enqueue(() =>
        {
            SchedulerMain.CurrentHint = MapCoords.WorldHint(loc.Terri, loc.Map, loc.xCoord, loc.yCoord);
            SchedulerMain.State = BotState.Navigating;
            if (Plugin.C.UseMount && Vector3.Distance(Player.Position, SchedulerMain.CurrentHint) > MountDistance)
                MountHelper.Mount();
            Plugin.Navmesh.PathfindAndMoveTo(SchedulerMain.CurrentHint, false);
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
            // Re-issue pathfind if navmesh idled out short of the camp.
            if (!Plugin.Navmesh.PathfindInProgress() && !Plugin.Navmesh.IsRunning())
                Plugin.Navmesh.PathfindAndMoveTo(SchedulerMain.CurrentHint, false);
            return false;
        }, "Travel to camp", new TaskManagerConfiguration { TimeLimitMS = 180000 });
    }
}

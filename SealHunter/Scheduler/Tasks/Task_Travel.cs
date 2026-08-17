using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
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
        var loc = SchedulerMain.CurrentLocation();
        if (entry == null || loc == null)
        {
            SchedulerMain.State = BotState.NextTarget;
            return;
        }

        var tm = Plugin.TaskManager;

        // Can't teleport while in combat — if a stray mob aggro'd us, kill the attackers first.
        tm.Enqueue(() =>
        {
            if (!Plugin.Condition[ConditionFlag.InCombat])
            {
                Plugin.CombatBackend.Disable();
                return true;
            }
            var foe = MobLocator.FindNearestAttacker(40f);
            if (foe != null)
            {
                TargetingHelper.SetTarget(foe);
                Plugin.CombatBackend.Enable();
                // Ranged jobs stripped BossMod's pathfinder, so nothing else closes the gap.
                CombatPositioning.Maintain(foe);
            }
            return false;
        }, "Clear aggro before travel", new TaskManagerConfiguration { TimeLimitMS = 60000, AbortOnTimeout = false });

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

        // Mount FIRST (and wait for it). Flight capability (Player.CanFly) only reads true while
        // mounted, and vnavmesh only takes off if we're already mounted when it gets a fly path.
        tm.Enqueue(() =>
        {
            SchedulerMain.CurrentHint = MapCoords.WorldHint(loc.Terri, loc.Map, loc.xCoord, loc.yCoord);
            SchedulerMain.State = BotState.Navigating;
            var dist = Vector3.Distance(Player.Position, SchedulerMain.CurrentHint);
            if (dist <= MountDistance || !(Plugin.C.UseMount || Plugin.C.UseFlight))
                return true;
            if (!Player.Mounted && !Player.Mounting && EzThrottler.Throttle("SH.Mount", 3000))
                MountHelper.Mount();
            return Player.Mounted;
        }, "Mount", new TaskManagerConfiguration { TimeLimitMS = 8000, AbortOnTimeout = false });

        tm.Enqueue(() =>
        {
            // Only fly when actually mounted — a fly path on a grounded character just freezes navmesh.
            var fly = Plugin.C.UseFlight && Player.Mounted && FlightHelper.FlyingUnlocked(loc.Terri);
            Plugin.Navmesh.PathfindAndMoveTo(SchedulerMain.CurrentHint, fly);
            EzThrottler.Throttle("SH.RepathTravel", 3000); // prime: don't re-issue immediately
            Plugin.Telemetry?.Log($"travel: pathfind hint=({SchedulerMain.CurrentHint.X:0},{SchedulerMain.CurrentHint.Y:0},{SchedulerMain.CurrentHint.Z:0}) dist={Vector3.Distance(Player.Position, SchedulerMain.CurrentHint):0} fly={fly} flyUnlocked={FlightHelper.FlyingUnlocked(loc.Terri)} mounted={Player.Mounted}");
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
            // Sprint on grounded walks (unmounted). Mounts/flying don't benefit and Sprint can't
            // be used while mounted anyway.
            if (!Player.Mounted && Plugin.C.UseSprint)
                SprintHelper.TrySprint();
            // Let the single pathfind run. Only nudge it if the navmesh genuinely stalled (idle),
            // and then at most once every few seconds — never re-issue every frame.
            if (!Plugin.Navmesh.PathfindInProgress() && !Plugin.Navmesh.IsRunning()
                && EzThrottler.Throttle("SH.RepathTravel", 3000))
                Plugin.Navmesh.PathfindAndMoveTo(SchedulerMain.CurrentHint, Plugin.C.UseFlight && Player.Mounted && FlightHelper.FlyingUnlocked(loc.Terri));
            return false;
        }, "Travel to camp", new TaskManagerConfiguration { TimeLimitMS = 180000 });
    }
}

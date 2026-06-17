using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.NeoTaskManager;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using SealHunter.Helpers;
using SealHunter.IPC;
using SealHunter.Models;

namespace SealHunter.Scheduler.Tasks;

/// <summary>
/// Phase 2 vertical slice: teleport → navigate → target → kill ONE open-world monster.
/// Proves the map→world conversion, navigation, targeting, and BossMod combat end-to-end.
/// </summary>
public static class Task_KillOne
{
    public static void Enqueue(HuntingMonster monster)
    {
        var loc = monster.PrimaryLocation;
        var tm = Plugin.TaskManager;
        var engageRange = Plugin.C.MaxEngageRange;
        var searchRadius = Plugin.C.MobSearchRadius;
        IBattleNpc? target = null;
        Vector3 hint = default;

        tm.Enqueue(() =>
        {
            // Teleport to the nearest aetheryte if we're not already in the target territory.
            if (Plugin.ClientState.TerritoryType == loc.Terri)
                return true;

            if (Plugin.Teleport.IsBusy())
                return false;

            if (TeleportIPC.Installed && EzThrottler.Throttle("SH.Teleport", 2000))
            {
                var aetheryte = AetheryteResolver.NearestAetheryte(loc.Map, loc.Terri, loc.MapCoords);
                if (aetheryte != 0)
                    Plugin.Teleport.Teleport(aetheryte, 0);
            }
            return false;
        }, "Teleport to zone", new TaskManagerConfiguration { TimeLimitMS = 30000 });

        tm.Enqueue(() => Plugin.ClientState.TerritoryType == loc.Terri && !Player.IsBusy && Plugin.Navmesh.IsReady(),
            "Wait for arrival + navmesh", new TaskManagerConfiguration { TimeLimitMS = 30000 });

        tm.Enqueue(() =>
        {
            hint = MapCoords.WorldHint(loc.Terri, loc.Map, loc.xCoord, loc.yCoord);
            Plugin.Navmesh.PathfindAndMoveTo(hint, false);
            return true;
        }, "Start pathfind to camp");

        tm.Enqueue(() =>
        {
            if (Vector3.Distance(Player.Position, hint) <= searchRadius)
            {
                Plugin.Navmesh.Stop();
                return true;
            }
            return !Plugin.Navmesh.PathfindInProgress() && !Plugin.Navmesh.IsRunning();
        }, "Travel to camp", new TaskManagerConfiguration { TimeLimitMS = 120000 });

        tm.Enqueue(() =>
        {
            target = MobLocator.FindNearest(monster.Id, hint, searchRadius);
            return target != null;
        }, "Locate target", new TaskManagerConfiguration { TimeLimitMS = 15000 });

        tm.Enqueue(() =>
        {
            if (target == null) return true;
            TargetingHelper.SetTarget(target);
            if (TargetingHelper.InRange(target, engageRange))
            {
                Plugin.Navmesh.Stop();
                return true;
            }
            if (EzThrottler.Throttle("SH.Approach", 1000))
                Plugin.Navmesh.PathfindAndMoveTo(target.Position, false);
            return false;
        }, "Approach target", new TaskManagerConfiguration { TimeLimitMS = 60000 });

        tm.Enqueue(() =>
        {
            Plugin.Navmesh.Stop();
            Plugin.CombatBackend.Enable();
            return true;
        }, "Engage");

        tm.Enqueue(() => TargetingHelper.TargetIsDead(),
            "Wait for kill", new TaskManagerConfiguration { TimeLimitMS = 60000 });

        tm.Enqueue(() =>
        {
            Plugin.CombatBackend.Disable();
            return true;
        }, "Disengage");
    }
}

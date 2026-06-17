using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.NeoTaskManager;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using SealHunter.Game;
using SealHunter.Helpers;

namespace SealHunter.Scheduler.Tasks;

/// <summary>Locate, approach, and kill the current target; loop within the entry until it is complete,
/// or skip the entry after repeated failures (empty camp / unkillable).</summary>
public static class Task_Engage
{
    public static void Enqueue()
    {
        var entry = SchedulerMain.Current;
        if (entry == null)
        {
            SchedulerMain.State = BotState.NextTarget;
            return;
        }

        SchedulerMain.EngageAttempts++;
        if (SchedulerMain.EngageAttempts > Plugin.C.MaxConsecutiveScanFailures)
        {
            ActivityLog.Warn_($"Skipping {entry.Monster.Name} (no progress after {SchedulerMain.EngageAttempts - 1} attempts).", chat: false);
            SchedulerMain.Skipped.Add(entry.Monster.Id);
            SchedulerMain.State = BotState.NextTarget;
            return;
        }

        var tm = Plugin.TaskManager;
        IBattleNpc? target = null;
        var locateStart = Environment.TickCount64;
        const long locateWindowMs = 15000;
        var lastPathPos = Vector3.Zero;

        tm.Enqueue(() =>
        {
            if (Player.Mounted)
            {
                MountHelper.Dismount();
                return !Player.Mounted;
            }
            return true;
        }, "Dismount", new TaskManagerConfiguration { TimeLimitMS = 8000, AbortOnTimeout = false });

        // Locate within a bounded window; if nothing appears, treat the camp as empty for now.
        tm.Enqueue(() =>
        {
            target = MobLocator.FindNearest(entry.Monster.Id, SchedulerMain.CurrentHint, Plugin.C.MobSearchRadius);
            if (target != null)
                return true;
            return Environment.TickCount64 - locateStart > locateWindowMs;
        }, "Locate target", new TaskManagerConfiguration { TimeLimitMS = 20000, AbortOnTimeout = false });

        tm.Enqueue(() =>
        {
            if (target != null)
            {
                ActivityLog.Notify($"Found {entry.Monster.Name}; engaging.", chat: false);
                return true;
            }
            // Empty camp: try the mob's next open-world camp before giving up on it.
            var locs = SchedulerMain.CurrentLocations();
            if (SchedulerMain.LocationIndex + 1 < locs.Count)
            {
                SchedulerMain.LocationIndex++;
                SchedulerMain.EngageAttempts = 0;
                ActivityLog.Warn_($"No {entry.Monster.Name} here; trying camp {SchedulerMain.LocationIndex + 1}/{locs.Count}.", chat: false);
                SchedulerMain.State = BotState.Teleporting;
                return true;
            }
            ActivityLog.Warn_($"No {entry.Monster.Name} at any camp; skipping for now.", chat: false);
            SchedulerMain.Skipped.Add(entry.Monster.Id);
            SchedulerMain.State = BotState.NextTarget;
            return true;
        }, "Handle empty camp");

        tm.Enqueue(() =>
        {
            if (target == null) return true;
            TargetingHelper.SetTarget(target);
            var dist = Vector3.Distance(Player.Position, target.Position);
            if (dist <= Plugin.C.MaxEngageRange)
            {
                Plugin.Navmesh.Stop();
                return true;
            }
            // While far, ride the existing path (only repath if the navmesh stalled) — chasing a
            // distant mob's per-second position just fights its wander AI. Only once we're close does
            // re-tracking its movement actually matter.
            var idle = !Plugin.Navmesh.PathfindInProgress() && !Plugin.Navmesh.IsRunning();
            var close = dist <= 15f;
            var moved = Vector3.Distance(target.Position, lastPathPos) > 3f;
            if ((idle || (close && moved)) && EzThrottler.Throttle("SH.Approach", 700))
            {
                lastPathPos = target.Position;
                var dest = StandoffPoint(target.Position, Player.Position, Plugin.C.MaxEngageRange);
                Plugin.Navmesh.PathfindAndMoveTo(dest, false);
                Plugin.Telemetry?.Log($"approach repath dist={dist:0} idle={idle} close={close} moved={moved}");
            }
            return false;
        }, "Approach target", new TaskManagerConfiguration { TimeLimitMS = 60000, AbortOnTimeout = false });

        tm.Enqueue(() =>
        {
            if (target == null) return true;
            Plugin.Navmesh.Stop();
            SchedulerMain.State = BotState.Engaging;
            Plugin.CombatBackend.Enable();
            var hp = target is IBattleChara c && c.MaxHp > 0 ? (int)(c.CurrentHp * 100 / c.MaxHp) : -1;
            Plugin.Telemetry?.Log($"engage: target={target.Name} hp={hp}% dist={Vector3.Distance(Player.Position, target.Position):0} inCombat={Plugin.Condition[ConditionFlag.InCombat]}");
            return true;
        }, "Engage");

        tm.Enqueue(() => target == null || TargetingHelper.TargetIsDead(),
            "Wait for kill", new TaskManagerConfiguration { TimeLimitMS = Plugin.C.CombatTimeoutSeconds * 1000, AbortOnTimeout = false });

        // Stop attacking the instant the mob dies, so autorotation can't tag another while we wait.
        tm.Enqueue(() =>
        {
            Plugin.CombatBackend.Disable();
            return true;
        }, "Disengage");

        // The hunting-log credit lags the death by a moment. Wait for the count to actually update
        // before deciding whether another mob is needed — otherwise we over-kill by one.
        var killedBefore = entry.Killed;
        tm.Enqueue(() =>
        {
            if (target == null) return true;
            var r = HuntPlan.Refresh(entry);
            return r == null || r.Killed > killedBefore;
        }, "Wait for kill credit", new TaskManagerConfiguration { TimeLimitMS = 8000, AbortOnTimeout = false });

        tm.Enqueue(() =>
        {
            if (target == null)
            {
                Plugin.Telemetry?.Log("kill-confirm: target lost/null before kill");
                return true;
            }

            // Re-evaluate against live progress (handles stray-aggro kills that didn't advance the count).
            var refreshed = HuntPlan.Refresh(entry);
            Plugin.Telemetry?.Log($"kill-confirm: prevKilled={entry.Killed} now={(refreshed?.Killed.ToString() ?? "complete")} inCombat={Plugin.Condition[ConditionFlag.InCombat]}");
            if (refreshed == null)
            {
                // Entry complete: move on.
                ActivityLog.Good_($"Completed {entry.Monster.Name}.", chat: false);
                SchedulerMain.EngageAttempts = 0;
                SchedulerMain.State = BotState.NextTarget;
            }
            else
            {
                if (refreshed.Killed > entry.Killed)
                {
                    ActivityLog.Good_($"Killed {entry.Monster.Name} ({refreshed.Killed}/{refreshed.Required}).", chat: false);
                    SchedulerMain.EngageAttempts = 0; // real progress; reset the skip counter
                }
                SchedulerMain.Current = refreshed;
                SchedulerMain.State = BotState.Locating; // loop within entry, no re-travel
            }
            return true;
        }, "Confirm kill / loop");
    }

    /// <summary>A point ~attack-range out from the mob toward the player, snapped to the navmesh —
    /// so we walk up next to the mob instead of trying to stand on top of it.</summary>
    private static Vector3 StandoffPoint(Vector3 mob, Vector3 player, float range)
    {
        var dir = player - mob;
        dir.Y = 0;
        var len = dir.Length();
        if (len < 0.1f)
            return mob; // basically on top already; the in-range check handles stopping
        var standoff = mob + dir / len * (range * 0.8f);
        return Plugin.Navmesh.NearestPoint(standoff, 5f, 5f) ?? standoff;
    }
}

using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.NeoTaskManager;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using SealHunter.Game;
using SealHunter.Helpers;
using XivHubPluginKit.Game;

namespace SealHunter.Scheduler.Tasks;

/// <summary>Locate, approach, and kill the current target; loop within the entry until it is complete,
/// or skip the entry after repeated failures (empty camp / unkillable).</summary>
public static class Task_Engage
{
    /// <summary>How far short of attack range to leave the mount. Roughly covers a cruising-altitude
    /// descent plus the dismount animation, so we are on foot by the time we arrive.</summary>
    private const float LandingLeadDistance = 20f;

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
        const long locateWindowMs = 30000;
        var nextPatrolTick = Environment.TickCount64 + 3000; // idle 3s before first roam
        var patrolIndex = 0;
        var lastPathPos = Vector3.Zero;
        long engageStartTick = 0; // set when the Engage step fires, for kill-time stats

        // Locate within a bounded window, roaming the camp on a ring around the hint when nothing
        // spawns at the arrival point — covers more of the camp than a stationary 60y scan.
        tm.Enqueue(() =>
        {
            // Something pulled us mid-roam: kill it before circling the camp any further, and don't
            // let the fight eat the search window.
            if (!AggroGuard.Clear())
            {
                locateStart = Environment.TickCount64;
                nextPatrolTick = Environment.TickCount64 + 3000;
                return false;
            }
            // Scan around the player (not the static hint) so roaming covers more spawns. Four scans
            // a second is plenty for a spawn to be noticed, and each one walks the whole ObjectTable.
            if (EzThrottler.Throttle("SH.Scan", 250))
                target = MobLocator.FindNearest(entry.Monster.Id, Player.Position, Plugin.C.MobSearchRadius);
            if (target != null)
            {
                if (Plugin.Navmesh.IsRunning()) Plugin.Navmesh.Stop();
                return true;
            }
            if (Environment.TickCount64 - locateStart > locateWindowMs)
            {
                if (Plugin.Navmesh.IsRunning()) Plugin.Navmesh.Stop();
                return true; // give up, let "Handle empty camp" decide
            }
            // Roam: when navmesh is idle, move to the next point on a ring around the camp hint.
            if (Environment.TickCount64 >= nextPatrolTick
                && !Plugin.Navmesh.PathfindInProgress() && !Plugin.Navmesh.IsRunning())
            {
                var angle = patrolIndex * (Math.PI / 4); // 8 points around the ring
                patrolIndex++;
                var r = Plugin.C.MobSearchRadius * 0.5f;
                var off = new Vector3((float)(Math.Cos(angle) * r), 0, (float)(Math.Sin(angle) * r));
                var dest = SchedulerMain.CurrentHint + off;
                var snapped = Plugin.Navmesh.NearestPoint(dest, 5f, 5f) ?? dest;
                var fly = Plugin.C.UseFlight && Player.Mounted && FlightHelper.FlyingUnlocked(Plugin.ClientState.TerritoryType);
                Plugin.Navmesh.PathfindAndMoveTo(snapped, fly);
                nextPatrolTick = Environment.TickCount64 + 6000; // arrive + scan, then next point
                Plugin.Telemetry?.Log($"patrol: idx={patrolIndex} dest=({snapped.X:0},{snapped.Y:0},{snapped.Z:0}) fly={fly}");
            }
            return false;
        }, "Locate target (roam)", new TaskManagerConfiguration { TimeLimitMS = 90000, AbortOnTimeout = false });

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
                ActivityLog.Warn_($"No {entry.Monster.Name} found after roaming the camp; trying camp {SchedulerMain.LocationIndex + 1}/{locs.Count}.", chat: false);
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
            // The mob can die while we're busy with an add (or to someone else's AoE); walking the
            // rest of the way to a corpse just burns the step's time limit.
            if (TargetingHelper.ObjectIsDeadOrGone(target, entry.Monster.Id))
            {
                target = null;
                return true;
            }
            // Anything already on us gets killed first. Our target is exempt: it hitting us is not
            // a diversion, and the in-range check below is about to engage it anyway.
            if (!AggroGuard.Clear(target)) return false;

            TargetingHelper.SetTarget(target);
            var dist = Vector3.Distance(Player.Position, target.Position);

            // Stop where this job actually attacks from — a bard has no reason to close to melee.
            // Without line of sight, halve the remaining gap instead: terrain has to be walked
            // around, and repeating that each frame converges on a spot that can see the mob.
            var range = CombatRange.AttackRange(target);
            if (!LineOfSight.Clear(target))
                range = Math.Min(range, dist) / 2f;

            // Elevated target (e.g. Ixali platforms) needs a flying approach.
            var heightDiff = Math.Abs(target.Position.Y - Player.Position.Y);
            var needFly = Plugin.C.UseFlight && heightDiff > 5f
                          && FlightHelper.FlyingUnlocked(Plugin.ClientState.TerritoryType);

            // Getting out of a flight is a descent *and* a dismount, several seconds of it, so start
            // it before arriving rather than hovering over the mob trying to fight from the saddle.
            // Not for an elevated target: dropping out of the sky short of a platform lands us under it.
            var landEarly = !needFly && Player.Mounted && dist <= range + LandingLeadDistance;

            if (dist <= range || landEarly)
            {
                // Stop the path first — vnavmesh holding a fly route fights the descent.
                if (Plugin.Navmesh.IsRunning() || Plugin.Navmesh.PathfindInProgress())
                    Plugin.Navmesh.Stop();
                if (!MountHelper.Ground())
                    return false;
                if (dist <= range)
                    return true;
                // Landed short of the target; walk the rest below.
            }

            if (needFly && !Player.Mounted && !Player.Mounting)
            {
                if (EzThrottler.Throttle("SH.ApproachMount", 3000)) MountHelper.Mount();
                return false;
            }

            // While far, ride the existing path (only repath if stalled); only re-track once close.
            var idle = !Plugin.Navmesh.PathfindInProgress() && !Plugin.Navmesh.IsRunning();
            var closeBy = dist <= 15f;
            var moved = Vector3.Distance(target.Position, lastPathPos) > 3f;
            if ((idle || (closeBy && moved)) && EzThrottler.Throttle("SH.Approach", 700))
            {
                lastPathPos = target.Position;
                var flying = needFly && Player.Mounted;
                // Flying: head straight to the mob; grounded: stop a standoff distance short.
                var dest = flying ? target.Position : CombatPositioning.StandoffPoint(target.Position, Player.Position, range);
                Plugin.Navmesh.PathfindAndMoveTo(dest, flying);
                Plugin.Telemetry?.Log($"approach repath dist={dist:0} h={heightDiff:0} fly={flying} idle={idle}");
            }
            return false;
        }, "Approach target", new TaskManagerConfiguration { TimeLimitMS = 120000, AbortOnTimeout = false });

        tm.Enqueue(() =>
        {
            if (target == null) return true;
            // Must be grounded to fight. The approach normally lands us; this catches the case where
            // the mob wandered into range while we were still coming down.
            if (!MountHelper.Ground()) return false;

            Plugin.Navmesh.Stop();
            SchedulerMain.State = BotState.Engaging;
            Plugin.CombatBackend.Enable();
            engageStartTick = Environment.TickCount64;
            var hp = target is IBattleChara c && c.MaxHp > 0 ? (int)(c.CurrentHp * 100 / c.MaxHp) : -1;
            Plugin.Telemetry?.Log($"engage: target={target.Name} hp={hp}% dist={Vector3.Distance(Player.Position, target.Position):0} inCombat={Plugin.Condition[ConditionFlag.InCombat]}");
            return true;
        }, "Engage", new TaskManagerConfiguration { TimeLimitMS = 30000 });

        tm.Enqueue(() =>
        {
            if (target == null) return true;
            // Wait for the specific engaged mob to die/despawn — NOT the soft-target. BossMod
            // autorotation may retarget to a stray aggro mob; a stray dying must not complete this
            // step, or we'd wait forever for a hunt-log credit that never comes.
            if (TargetingHelper.ObjectIsDeadOrGone(target, entry.Monster.Id))
                return true;
            CombatPositioning.Maintain(target);
            return false;
        }, "Wait for kill", new TaskManagerConfiguration { TimeLimitMS = Plugin.C.CombatTimeoutSeconds * 1000, AbortOnTimeout = false });

        // Stop attacking the instant the mob dies, so autorotation can't tag another while we wait.
        tm.Enqueue(() =>
        {
            Plugin.CombatBackend.Disable();
            // Cancel any in-combat reposition still running toward the now-dead mob.
            if (Plugin.Navmesh.IsRunning()) Plugin.Navmesh.Stop();
            return true;
        }, "Disengage");

        // The hunting-log credit lags the death by a moment. Wait for the count to actually update
        // before deciding whether another mob is needed — otherwise we over-kill by one.
        var killedBefore = entry.Killed;
        var droppedStaleSnapshot = false;
        tm.Enqueue(() =>
        {
            if (target == null) return true;
            // Drop the pre-kill snapshot once; after that the HuntPlan TTL re-reads live progress on
            // its own. Invalidating every frame would rebuild the whole plan (a full MonsterNote walk
            // plus a fresh entry list) on each of the ~8s worth of frames this step can wait.
            if (!droppedStaleSnapshot)
            {
                HuntPlan.Invalidate();
                droppedStaleSnapshot = true;
            }
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

            // Update the rolling kill-time stat (only when we actually advanced the count).
            if (refreshed != null && refreshed.Killed > entry.Killed && engageStartTick > 0)
            {
                var killSec = (Environment.TickCount64 - engageStartTick) / 1000.0;
                SchedulerMain.TotalKills++;
                if (!SchedulerMain.SeedKillAverage(killSec))
                    SchedulerMain.UpdateKillAverage(killSec);
            }

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

}

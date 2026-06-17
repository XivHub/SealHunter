using System;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.NeoTaskManager;
using ECommons.GameHelpers;
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
            // Empty camp: skip for this pass, advance to the next target.
            ActivityLog.Warn_($"No {entry.Monster.Name} found nearby; skipping for now.", chat: false);
            SchedulerMain.Skipped.Add(entry.Monster.Id);
            SchedulerMain.State = BotState.NextTarget;
            return true;
        }, "Handle empty camp");

        tm.Enqueue(() =>
        {
            if (target == null) return true;
            TargetingHelper.SetTarget(target);
            Plugin.Navmesh.PathfindAndMoveTo(target.Position, false);
            return TargetingHelper.InRange(target, Plugin.C.MaxEngageRange);
        }, "Approach target", new TaskManagerConfiguration { TimeLimitMS = 60000, AbortOnTimeout = false });

        tm.Enqueue(() =>
        {
            if (target == null) return true;
            Plugin.Navmesh.Stop();
            SchedulerMain.State = BotState.Engaging;
            Plugin.CombatBackend.Enable();
            return true;
        }, "Engage");

        tm.Enqueue(() => target == null || TargetingHelper.TargetIsDead(),
            "Wait for kill", new TaskManagerConfiguration { TimeLimitMS = Plugin.C.CombatTimeoutSeconds * 1000, AbortOnTimeout = false });

        tm.Enqueue(() =>
        {
            Plugin.CombatBackend.Disable();
            if (target == null)
                return true;

            // Re-evaluate against live progress (handles stray-aggro kills that didn't advance the count).
            var refreshed = HuntPlan.Refresh(entry);
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

using SealHunter.Game;

namespace SealHunter.Scheduler.Tasks;

/// <summary>Picks the next open-world target from live progress, or finishes / waits for respawns.</summary>
public static class Task_SelectTarget
{
    public static void Enqueue()
    {
        var tm = Plugin.TaskManager;

        tm.Enqueue(() =>
        {
            var next = HuntPlan.Next(SchedulerMain.Skipped);
            if (next != null)
            {
                SchedulerMain.Current = next;
                SchedulerMain.EngageAttempts = 0;
                SchedulerMain.State = BotState.Teleporting;
                Plugin.ChatGui.Print($"[SealHunter] Next: {next.Monster.Name} {next.Killed}/{next.Required} (terri {next.Location.Terri}).");
                return true;
            }

            // Nothing selectable: either everything open-world is done, or the rest are skipped (empty camps).
            if (HuntPlan.IncompleteOpenWorld().Count == 0)
            {
                SchedulerMain.State = BotState.Done;
                return true;
            }

            if (Plugin.C.StopWhenNoMobs)
            {
                Plugin.ChatGui.Print("[SealHunter] No mobs available; stopping (StopWhenNoMobs).");
                SchedulerMain.State = BotState.Done;
                return true;
            }

            // Wait for respawns, then clear the skip set and re-select (State stays NextTarget).
            Plugin.ChatGui.Print($"[SealHunter] Camps empty; waiting {Plugin.C.RespawnWaitSeconds}s for respawns.");
            tm.EnqueueDelay(Plugin.C.RespawnWaitSeconds * 1000);
            tm.Enqueue(() => { SchedulerMain.Skipped.Clear(); return true; }, "Clear skip after respawn wait");
            return true;
        }, "Select next target");
    }
}

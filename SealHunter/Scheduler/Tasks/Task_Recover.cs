using ECommons.Automation.NeoTaskManager;
using ECommons.GameHelpers;
using SealHunter.Helpers;

namespace SealHunter.Scheduler.Tasks;

/// <summary>Death recovery: wait for revival, then hand back to target selection.
/// Auto-clicking the "Return" prompt is not yet implemented; the bot waits for revival
/// (self-return or a raise) and resumes from live progress.</summary>
public static class Task_Recover
{
    public static void Enqueue()
    {
        var tm = Plugin.TaskManager;

        tm.Enqueue(() =>
        {
            Plugin.CombatBackend.Disable();
            if (Plugin.Navmesh.IsRunning())
                Plugin.Navmesh.Stop();
            return true;
        }, "Recover: stop activity");

        // Wait until revived and the screen is interactive again. No hard time limit:
        // an unattended bot should sit dead rather than abort the whole run.
        tm.Enqueue(() => !Player.IsDead && SealHunterGuard.IsScreenReady() && Plugin.Navmesh.IsReady(),
            "Recover: wait for revival", new TaskManagerConfiguration { TimeLimitMS = 600000 });

        tm.Enqueue(() =>
        {
            SchedulerMain.State = BotState.NextTarget;
            return true;
        }, "Recover: resume");
    }
}

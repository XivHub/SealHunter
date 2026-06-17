using System;
using ECommons;
using ECommons.Automation.NeoTaskManager;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.UI;
using SealHunter.Helpers;

namespace SealHunter.Scheduler.Tasks;

/// <summary>Death recovery: stop activity, optionally auto-click the "Return" prompt, then hand
/// back to target selection. <para>If <c>ReturnOnDeath</c> is set and the death Yes/No dialog is
/// open, fire "Yes" (callback 0) once per attempt. The dialog only appears while dead, so the
/// dead+SelectYesno gate is enough to avoid clicking unrelated Yes/No prompts.</para></summary>
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

        // Auto-Return: while dead and the SelectYesno death prompt is up, click Yes (callback 0).
        // Throttled so we don't spam the callback every frame. No-op if the prompt isn't shown
        // (player will self-return or get raised). Bounded time limit as a safety net; if it
        // expires we just fall through to the revival wait rather than aborting the run.
        if (Plugin.C.ReturnOnDeath)
        {
            tm.Enqueue(() =>
            {
                if (!Player.IsDead)
                    return true; // already revived (raise) — nothing to click
                if (ClickReturnIfOpen())
                    ActivityLog.Notify("Clicked Return on death.", chat: false);
                return false; // keep polling until !IsDead
            }, "Recover: auto-Return", new TaskManagerConfiguration { TimeLimitMS = 30000, AbortOnTimeout = false });
        }

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

    /// <summary>Click "Yes" on the death Return prompt if it's currently open. The dialog only
    /// appears while the player is dead, so the caller's <c>Player.IsDead</c> gate is the only
    /// guard needed to avoid clicking unrelated Yes/No prompts.</summary>
    private static unsafe bool ClickReturnIfOpen()
    {
        if (!GenericHelpers.TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon))
            return false;
        addon->AtkUnitBase.FireCallbackInt(0); // 0 = Yes
        return true;
    }
}

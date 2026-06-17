using System.Collections.Generic;
using System.Numerics;
using SealHunter.Game;
using SealHunter.Helpers;
using SealHunter.IPC;
using SealHunter.Scheduler.Tasks;

namespace SealHunter.Scheduler;

/// <summary>Frame-driven state machine (ICE SchedulerMain pattern). Top-of-tick guards run every
/// frame; the per-state dispatch advances the loop when the TaskManager queue is empty.</summary>
public static class SchedulerMain
{
    public static BotState State = BotState.Idle;

    /// <summary>The target currently being worked.</summary>
    public static HuntEntry? Current;

    /// <summary>World hint (X/Z + floor Y) for the current target's camp.</summary>
    public static Vector3 CurrentHint;

    /// <summary>Monsters temporarily skipped this pass (camp empty / unkillable); cleared after a respawn wait.</summary>
    public static readonly HashSet<uint> Skipped = new();

    /// <summary>Consecutive failed engage attempts on the current entry, before it is skipped.</summary>
    public static int EngageAttempts;

    /// <summary>State to resume to after a transient pause (duty pop / player intervention) clears.</summary>
    private static BotState resumeState = BotState.NextTarget;

    public static bool Running => State != BotState.Idle;

    public static bool EnablePlugin()
    {
        var (ok, message) = Dependencies.CheckAll();
        if (!ok)
        {
            Plugin.ChatGui.PrintError($"[SealHunter] {message}");
            return false;
        }

        Current = null;
        Skipped.Clear();
        EngageAttempts = 0;
        State = BotState.NextTarget;
        Plugin.Logger.Info("SealHunter started.");
        return true;
    }

    public static bool DisablePlugin()
    {
        Plugin.TaskManager.Abort();
        Plugin.CombatBackend.Disable();
        if (NavmeshIPC.Installed && Plugin.Navmesh.IsRunning())
            Plugin.Navmesh.Stop();
        State = BotState.Idle;
        Plugin.Logger.Info("SealHunter stopped.");
        return true;
    }

    public static void Tick()
    {
        if (State == BotState.Idle)
            return;

        // --- Top-of-tick guards (every frame) ---

        if (!SealHunterGuard.IsScreenReady() || SealHunterGuard.InOrEnteringDuty())
        {
            EnterPause(BotState.PausedForDuty);
            return;
        }

        if (PlayerGuard.IsDead())
        {
            if (Plugin.C.StopOnDeath)
            {
                Plugin.ChatGui.Print("[SealHunter] Stopped on death.");
                DisablePlugin();
                return;
            }
            if (State != BotState.Recovering)
            {
                Plugin.TaskManager.Abort();
                State = BotState.Recovering;
            }
        }

        if (Plugin.C.PauseOnPlayerIntervention
            && State is BotState.Teleporting or BotState.Navigating
            && PlayerGuard.PlayerIntervened(null))
        {
            EnterPause(BotState.PausedForPlayer);
            return;
        }

        if (DurabilityGuard.NeedsRepair(Plugin.C.MinDurabilityPercent))
        {
            Plugin.ChatGui.PrintError($"[SealHunter] Gear below {Plugin.C.MinDurabilityPercent:0}% durability — stopping. Repair and restart.");
            DisablePlugin();
            return;
        }

        // Resume from a transient pause once the world is interactive again.
        if (State is BotState.PausedForDuty or BotState.PausedForPlayer)
        {
            State = resumeState;
            return;
        }

        // --- Per-state dispatch (only when nothing is queued) ---

        if (Plugin.TaskManager.NumQueuedTasks != 0)
            return;

        switch (State)
        {
            case BotState.NextTarget:
                Task_SelectTarget.Enqueue();
                break;
            case BotState.Teleporting:
            case BotState.Navigating:
                Task_Travel.Enqueue();
                break;
            case BotState.Locating:
            case BotState.Engaging:
                Task_Engage.Enqueue();
                break;
            case BotState.Recovering:
                Task_Recover.Enqueue();
                break;
            case BotState.Done:
                Plugin.ChatGui.Print("[SealHunter] GC hunting log complete (open-world targets).");
                DisablePlugin();
                break;
            case BotState.Error:
                Plugin.Logger.Warning("SealHunter entered Error state; stopping.");
                DisablePlugin();
                break;
        }
    }

    private static void EnterPause(BotState pause)
    {
        if (State is BotState.Idle or BotState.PausedForDuty or BotState.PausedForPlayer)
        {
            if (State != BotState.Idle)
                State = pause;
            return;
        }

        resumeState = State == BotState.Recovering ? BotState.Recovering : BotState.NextTarget;
        Plugin.TaskManager.Abort();
        if (NavmeshIPC.Installed && Plugin.Navmesh.IsRunning())
            Plugin.Navmesh.Stop();
        Plugin.CombatBackend.Disable();
        State = pause;
    }
}

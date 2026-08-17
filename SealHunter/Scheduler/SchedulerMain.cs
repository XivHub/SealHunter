using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using SealHunter.Game;
using SealHunter.Helpers;
using SealHunter.IPC;
using SealHunter.Models;
using SealHunter.Scheduler.Tasks;
using ZhyraPluginKit.Game;

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

    /// <summary>TickCount64 when the current target was selected, for the elapsed display.</summary>
    public static long CurrentTargetStartTick;

    /// <summary>Total kills credited this run (across all entries). Updated on kill-confirm.</summary>
    public static int TotalKills;

    /// <summary>Rolling average seconds per kill (exponential moving average), for the ETA panel.
    /// Seeded lazily on the first kill so a fresh run shows no ETA.</summary>
    public static double AverageKillSeconds;
    private static bool avgKillSeeded;

    public static int CurrentTargetElapsedSeconds
        => Current == null ? 0 : (int)Math.Max(0, (Environment.TickCount64 - CurrentTargetStartTick) / 1000);

    /// <summary>Seed the kill-time EMA with the first observed kill. Returns true if it seeded
    /// (i.e. this was the first kill). Call once per kill; subsequent kills use <see cref="UpdateKillAverage"/>.</summary>
    public static bool SeedKillAverage(double seconds)
    {
        if (avgKillSeeded) return false;
        AverageKillSeconds = seconds;
        avgKillSeeded = true;
        return true;
    }

    /// <summary>Update the kill-time EMA (α = 0.3). Only call after the first kill has seeded it.</summary>
    public static void UpdateKillAverage(double seconds)
    {
        const double alpha = 0.3;
        AverageKillSeconds = alpha * seconds + (1 - alpha) * AverageKillSeconds;
    }

    /// <summary>Current TaskManager step label ("what it's doing"), if any.</summary>
    public static string CurrentAction => Plugin.TaskManager.CurrentTask?.Name ?? "—";

    /// <summary>Monsters temporarily skipped this pass (camp empty / unkillable); cleared after a respawn wait.</summary>
    public static readonly HashSet<uint> Skipped = new();

    /// <summary>Consecutive failed engage attempts on the current entry, before it is skipped.</summary>
    public static int EngageAttempts;

    /// <summary>Which of the current monster's open-world camps we're currently trying.</summary>
    public static int LocationIndex;

    // Cached open-world camps for `Current`. Recomputed only when Current's monster identity
    // changes, so the per-frame tick doesn't re-allocate a Where().ToList() on every call.
    private static List<HuntingMonsterLocation>? cachedLocations;
    private static uint cachedLocationsMonsterId;

    public static List<HuntingMonsterLocation> CurrentLocations()
    {
        var cur = Current;
        if (cur == null)
        {
            cachedLocations = null;
            cachedLocationsMonsterId = 0;
            return new List<HuntingMonsterLocation>();
        }
        if (cachedLocations != null && cachedLocationsMonsterId == cur.Monster.Id)
            return cachedLocations;
        cachedLocations = OrderCamps(cur.Monster.Locations);
        cachedLocationsMonsterId = cur.Monster.Id;
        return cachedLocations;
    }

    /// <summary>Open-world camps, nearest first: a camp in the zone we are already standing in beats
    /// one that costs a teleport, and within that zone the closest one wins. Distance between zones
    /// is meaningless, so the remaining camps keep their dataset order (OrderBy is stable).
    /// Evaluated once per target — the list is cached per monster.</summary>
    private static List<HuntingMonsterLocation> OrderCamps(List<HuntingMonsterLocation> all)
    {
        var here = Plugin.ClientState.TerritoryType;
        var pos = Player.Available ? Player.Position : Vector3.Zero;
        return all.Where(l => l.IsOpenWorld)
            .OrderBy(l => l.Terri == here ? 0 : 1)
            .ThenBy(l => l.Terri == here
                ? Vector3.Distance(pos, MapCoords.MapToWorldFlat(l.Terri, l.Map, l.xCoord, l.yCoord) with { Y = pos.Y })
                : 0f)
            .ToList();
    }

    /// <summary>The camp currently being worked, or null.</summary>
    public static HuntingMonsterLocation? CurrentLocation()
    {
        var locs = CurrentLocations();
        return locs.Count == 0 ? null : locs[Math.Min(LocationIndex, locs.Count - 1)];
    }

    /// <summary>State to resume to after a transient pause (duty pop / player intervention) clears.</summary>
    private static BotState resumeState = BotState.NextTarget;

    /// <summary>Until this tick, keep cancelling our own residual navmesh movement after a stop.
    /// Bounded so we NEVER touch vnavmesh while idle long-term (that would hijack other plugins).</summary>
    private static long stopGuardUntil;

    // --- Anti-stuck watchdog ---
    // While travelling (Teleporting/Navigating), track player movement. If navmesh reports a move
    // is running but the player hasn't moved > 1y for StuckTimeoutSeconds, escalate:
    //   1) re-path to CurrentHint; 2) jump; 3) re-teleport (sets State=Teleporting to redo travel).
    private static Vector3 lastWatchPos;
    private static long lastWatchMovedTick;
    private static int stuckEscalation; // 0=ok, 1=repathed, 2=jumped, 3=re-teleport

    public static bool Running => State != BotState.Idle;

    public static bool EnablePlugin()
    {
        var (ok, message) = Dependencies.CheckAll();
        if (!ok)
        {
            Plugin.ChatGui.PrintError($"[SealHunter] {message}");
            return false;
        }

        // BossMod can only fight on a combat job; on a crafter/gatherer nothing would ever die.
        if (Player.Available && Sheets.ClassJobSheet.GetRow((uint)Player.Job).DohDolJobIndex >= 0)
        {
            Plugin.ChatGui.PrintError("[SealHunter] Switch to a combat job before starting.");
            return false;
        }

        ResetLoopState();
        State = BotState.NextTarget;
        Helpers.ActivityLog.Good_("Started.");
        return true;
    }

    /// <summary>Clear all per-run scratch state. Called on Start (fresh run) and on logout
    /// (so a character switch can't resume against a stale target).</summary>
    public static void ResetLoopState()
    {
        Current = null;
        Skipped.Clear();
        EngageAttempts = 0;
        LocationIndex = 0;
        cachedLocations = null;
        cachedLocationsMonsterId = 0;
        stuckEscalation = 0;
        lastWatchPos = default;
        lastWatchMovedTick = 0;
        TotalKills = 0;
        AverageKillSeconds = 0;
        avgKillSeeded = false;
    }

    public static bool DisablePlugin()
    {
        Plugin.TaskManager.Abort();
        Plugin.CombatBackend.Disable();
        // Stop unconditionally: during an async pathfind IsRunning() is false but a move is still
        // pending, so a guarded Stop() would miss it and movement would resume.
        if (NavmeshIPC.Installed)
            Plugin.Navmesh.Stop();
        stopGuardUntil = Environment.TickCount64 + 2000; // briefly cancel our own residual move only
        State = BotState.Idle;
        Helpers.ActivityLog.Warn_("Stopped.", chat: false);
        Plugin.Logger.Info("SealHunter stopped.");
        return true;
    }

    public static void Tick()
    {
        Plugin.Telemetry?.Snapshot(BuildSnapshot);

        if (State == BotState.Idle)
        {
            // Only for a brief window after WE stopped — cancel our own residual pathfind. Never
            // touch vnavmesh otherwise, or we'd hijack movement from other plugins (Questionable, etc.).
            if (Environment.TickCount64 < stopGuardUntil && NavmeshIPC.Installed && Plugin.Navmesh.IsRunning())
                Plugin.Navmesh.Stop();
            return;
        }

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
                Helpers.ActivityLog.Warn_("Stopped on death.");
                DisablePlugin();
                return;
            }
            if (State != BotState.Recovering)
            {
                Plugin.TaskManager.Abort();
                State = BotState.Recovering;
            }
        }

        // Resume from a transient pause (duty pop) once the world is interactive again.
        if (State == BotState.PausedForDuty)
        {
            State = resumeState;
            return;
        }

        // Anti-stuck watchdog: runs during travel while navmesh is actively moving us.
        if (State is BotState.Teleporting or BotState.Navigating)
            WatchdogTick();

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
                Helpers.ActivityLog.Good_("GC hunting log complete (open-world targets).");
                DisablePlugin();
                break;
            case BotState.Error:
                Plugin.Logger.Warning("SealHunter entered Error state; stopping.");
                DisablePlugin();
                break;
        }
    }

    /// <summary>Anti-stuck watchdog: if navmesh is actively moving us but the player hasn't
    /// progressed for <see cref="Configuration.StuckTimeoutSeconds"/>, escalate through
    /// re-path → jump → re-teleport. Resets escalation as soon as movement resumes.</summary>
    private static void WatchdogTick()
    {
        if (!NavmeshIPC.Installed || !Plugin.Navmesh.IsRunning())
        {
            // No active move: reset the baseline so a fresh move starts a new window.
            lastWatchPos = Player.Available ? Player.Position : default;
            lastWatchMovedTick = Environment.TickCount64;
            return;
        }

        var pos = Player.Available ? Player.Position : default;
        if (Vector3.DistanceSquared(pos, lastWatchPos) > 1f) // moved > 1y
        {
            lastWatchPos = pos;
            lastWatchMovedTick = Environment.TickCount64;
            if (stuckEscalation != 0) stuckEscalation = 0; // moving again
            return;
        }

        var stuckMs = Plugin.C.StuckTimeoutSeconds * 1000;
        if (Environment.TickCount64 - lastWatchMovedTick < stuckMs)
            return; // within the window

        // Stuck. Escalate.
        var hint = CurrentHint;
        switch (stuckEscalation)
        {
            case 0:
                ActivityLog.Warn_($"Stuck for {Plugin.C.StuckTimeoutSeconds}s; re-pathing.", chat: false);
                Plugin.Navmesh.Stop();
                var fly = Plugin.C.UseFlight && Player.Mounted
                          && FlightHelper.FlyingUnlocked(Plugin.ClientState.TerritoryType);
                Plugin.Navmesh.PathfindAndMoveTo(hint, fly);
                stuckEscalation = 1;
                lastWatchMovedTick = Environment.TickCount64; // reset window for next escalation
                break;
            case 1:
                ActivityLog.Warn_($"Still stuck; jumping.", chat: false);
                MountHelper.Jump();
                stuckEscalation = 2;
                lastWatchMovedTick = Environment.TickCount64;
                break;
            default:
                ActivityLog.Warn_("Still stuck; re-teleporting to the camp aetheryte.");
                Plugin.TaskManager.Abort();
                if (NavmeshIPC.Installed) Plugin.Navmesh.Stop();
                Plugin.CombatBackend.Disable();
                State = BotState.Teleporting; // dispatcher re-runs Task_Travel next tick
                stuckEscalation = 3;
                lastWatchMovedTick = Environment.TickCount64;
                break;
        }
    }

    private static string BuildSnapshot()
    {
        var t = Current;
        var pos = Player.Available ? Player.Position : default;
        var prog = t == null ? "" : $"{t.Killed}/{t.Required}";
        var tgt = Plugin.TargetManager.Target;
        var tgtHp = tgt is IBattleChara bc && bc.MaxHp > 0 ? (int)(bc.CurrentHp * 100 / bc.MaxHp) : -1;
        return $"state={State} action=\"{CurrentAction}\" target={t?.Monster.Name ?? "-"} prog={prog} " +
               $"elapsed={CurrentTargetElapsedSeconds}s terri={Plugin.ClientState.TerritoryType} " +
               $"pos=({pos.X:0},{pos.Y:0},{pos.Z:0}) navRun={Plugin.Navmesh.IsRunning()} " +
               $"navBusy={Plugin.Navmesh.PathfindInProgress()} inCombat={Plugin.Condition[ConditionFlag.InCombat]} " +
               $"botCombat={Plugin.CombatBackend.IsActive()} hasTgt={tgt != null} tgtHp={tgtHp}% queued={Plugin.TaskManager.NumQueuedTasks}";
    }

    private static void EnterPause(BotState pause)
    {
        if (State is BotState.Idle or BotState.PausedForDuty)
        {
            if (State != BotState.Idle)
                State = pause;
            return;
        }

        resumeState = State == BotState.Recovering ? BotState.Recovering : BotState.NextTarget;
        Plugin.TaskManager.Abort();
        if (NavmeshIPC.Installed)
            Plugin.Navmesh.Stop();
        Plugin.CombatBackend.Disable();
        State = pause;
    }
}

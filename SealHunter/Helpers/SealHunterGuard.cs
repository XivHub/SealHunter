using Dalamud.Game.ClientState.Conditions;
using ECommons;

namespace SealHunter.Helpers;

/// <summary>Environment guard: is the client in a safe state to drive automation this frame?
/// Mirrors ICE's PlayerHelper.IsScreenReady plus FATE/quest-dialog conditions.</summary>
public static class SealHunterGuard
{
    public static bool IsScreenReady() =>
        GenericHelpers.IsScreenReady()
        && !Plugin.Condition[ConditionFlag.BetweenAreas]
        && !Plugin.Condition[ConditionFlag.BetweenAreas51]
        && !Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent]
        && !Plugin.Condition[ConditionFlag.WatchingCutscene]
        && !Plugin.Condition[ConditionFlag.WatchingCutscene78]
        && !Plugin.Condition[ConditionFlag.OccupiedInQuestEvent]
        && !Plugin.Condition[ConditionFlag.OccupiedSummoningBell]
        && !Plugin.Condition[ConditionFlag.Occupied33]
        && !Plugin.Condition[ConditionFlag.Occupied38]
        && !Plugin.Condition[ConditionFlag.Occupied39];

    /// <summary>A duty/instance is active. The loop pauses until clear. Sitting in a duty finder queue
    /// (<c>WaitingForDutyFinder</c>, <c>InDutyQueue</c>) is deliberately not a pause: a roulette queue
    /// can last many minutes, and hunting while it waits is the point.</summary>
    public static bool InOrEnteringDuty() =>
        Plugin.Condition[ConditionFlag.BoundByDuty]
        || Plugin.Condition[ConditionFlag.BoundByDuty56]
        || Plugin.Condition[ConditionFlag.BoundByDuty95];
}

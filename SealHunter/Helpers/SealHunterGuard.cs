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

    /// <summary>A duty/instance is loading or active (e.g. a queue popped). The loop pauses until clear.</summary>
    public static bool InOrEnteringDuty() =>
        Plugin.Condition[ConditionFlag.WaitingForDutyFinder]
        || Plugin.Condition[ConditionFlag.InDutyQueue]
        || Plugin.Condition[ConditionFlag.BoundByDuty]
        || Plugin.Condition[ConditionFlag.BoundByDuty56]
        || Plugin.Condition[ConditionFlag.BoundByDuty95];
}

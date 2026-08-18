using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using XivHubPluginKit.Game;

namespace SealHunter.Helpers;

/// <summary>Kill whatever pulled us before getting on with the plan. Walking away from aggro does not
/// work: the mob follows, mounting is blocked in combat, and we soak the whole trip's damage without
/// ever fighting back. Travel, camp roaming and the approach to a hunt target all clear here first.</summary>
public static class AggroGuard
{
    /// <summary>How far to look for mobs that have us targeted. Anything that has aggro'd is chasing
    /// us and sits well inside this.</summary>
    private const float AttackerSearchRadius = 40f;

    /// <summary>Fight off whatever is attacking us. Returns true when we are clear and the caller may
    /// carry on, false while an attacker is being dealt with — call it every frame until it is true.
    /// <paramref name="ignore"/> is the mob the caller is already on its way to fight: that one
    /// attacking us is not a diversion, it is the plan.</summary>
    public static bool Clear(IBattleNpc? ignore = null)
    {
        if (!Plugin.Condition[ConditionFlag.InCombat])
            return Release();

        var foe = MobLocator.FindNearestAttacker(AttackerSearchRadius, ignore);
        if (foe == null)
            return Release();

        // Drop whatever we were walking toward: dragging the mob along just spreads the fight over
        // the whole route, and the destination is worthless until we are out of combat anyway.
        if (Plugin.Navmesh.IsRunning() || Plugin.Navmesh.PathfindInProgress())
            Plugin.Navmesh.Stop();
        // Nothing can be cast from the saddle: enabling the rotation while mounted just leaves us
        // sitting there taking hits. Combat does not throw us off by itself, so we do it.
        if (!MountHelper.Ground())
            return false;
        // Enabling is several IPC calls and a preset rebuild, so only on the transition.
        if (!Plugin.CombatBackend.IsActive())
            Plugin.CombatBackend.Enable();
        // Ranged jobs stripped BossMod's pathfinder, so nothing else closes the gap.
        CombatPositioning.Maintain(foe);
        return false;
    }

    private static bool Release()
    {
        if (Plugin.CombatBackend.IsActive())
            Plugin.CombatBackend.Disable();
        return true;
    }
}

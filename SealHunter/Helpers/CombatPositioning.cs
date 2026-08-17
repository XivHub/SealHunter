using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using ZhyraPluginKit.Game;

namespace SealHunter.Helpers;

/// <summary>Where to stand while fighting. SealHunter owns movement whenever the autorotation
/// backend does not, so it has to walk itself back into range and line of sight if the mob strays.</summary>
public static class CombatPositioning
{
    /// <summary>A point ~attack-range out from the mob toward the player, snapped to the navmesh —
    /// so we walk up next to the mob instead of trying to stand on top of it.</summary>
    public static Vector3 StandoffPoint(Vector3 mob, Vector3 player, float range)
    {
        var dir = player - mob;
        dir.Y = 0;
        var len = dir.Length();
        if (len < 0.1f)
            return mob; // basically on top already; the in-range check handles stopping
        var standoff = mob + dir / len * (range * 0.8f);
        return Plugin.Navmesh.NearestPoint(standoff, 5f, 5f) ?? standoff;
    }

    /// <summary>Keep the mob killable while the rotation works: re-assert the target if the backend
    /// wandered off it, and — when the backend is not moving us — walk back into range and line of
    /// sight if the mob strays. Both are no-ops in the common case where the fight stays put.</summary>
    public static void Maintain(IBattleNpc target)
    {
        if (Plugin.TargetManager.Target?.GameObjectId != target.GameObjectId
            && EzThrottler.Throttle("SH.Retarget", 1000))
            TargetingHelper.SetTarget(target);

        if (Plugin.CombatBackend.MovesPlayer)
            return; // BossMod's pathfinder owns positioning; two movers would fight each other

        var dist = Vector3.Distance(Player.Position, target.Position);
        var range = CombatRange.AttackRange(target);
        var los = LineOfSight.Clear(target);
        if (dist <= range && los)
            return;

        if (Plugin.Navmesh.PathfindInProgress() || Plugin.Navmesh.IsRunning())
            return;
        if (!EzThrottler.Throttle("SH.Reposition", 1500))
            return;

        Plugin.Navmesh.PathfindAndMoveTo(StandoffPoint(target.Position, Player.Position, range), false);
        Plugin.Telemetry?.Log($"reposition: dist={dist:0} range={range:0} los={los}");
    }
}

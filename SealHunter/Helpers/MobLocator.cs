using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using XivHubPluginKit.Game;

namespace SealHunter.Helpers;

public static class MobLocator
{
    /// <summary>Nearest live BattleNpc currently targeting us (i.e. aggro'd) within radius, or null.</summary>
    public static IBattleNpc? FindNearestAttacker(float radius)
    {
        var meId = Player.Object?.GameObjectId ?? 0;
        if (meId == 0) return null;

        IBattleNpc? best = null;
        var bestSq = radius * radius;
        foreach (var o in Plugin.ObjectTable)
        {
            if (o is not IBattleNpc npc) continue;
            if (npc.IsDead || !npc.IsTargetable || npc.TargetObjectId != meId) continue;
            var dSq = Vector3.DistanceSquared(npc.Position, Player.Position);
            if (dSq <= bestSq)
            {
                bestSq = dSq;
                best = npc;
            }
        }
        return best;
    }

    /// <summary>Nearest live, attackable BattleNpc matching the given BNpcName id within radius of the hint.
    /// Single pass over the ObjectTable, distance-squared, no LINQ allocations.
    /// <para>A mob we can see is worth more than a marginally closer one behind a cliff, so the
    /// nearest candidate with clear line of sight wins. It is only a preference: if nothing is
    /// visible we still return the nearest mob and let the approach walk around the obstruction,
    /// which is what happens in a camp seen from above or through a treeline.</para></summary>
    public static IBattleNpc? FindNearest(uint bNpcNameId, Vector3 hint, float radius)
    {
        IBattleNpc? nearest = null;
        IBattleNpc? nearestVisible = null;
        var nearestSq = radius * radius;
        var nearestVisibleSq = radius * radius;

        foreach (var o in Plugin.ObjectTable)
        {
            if (o is not IBattleNpc npc) continue;
            if (npc.NameId != bNpcNameId || npc.IsDead || !npc.IsTargetable) continue;

            var dSq = Vector3.DistanceSquared(npc.Position, hint);
            if (dSq > nearestSq && dSq > nearestVisibleSq)
                continue; // can't win either slot; skip the raycast

            if (dSq <= nearestSq)
            {
                nearestSq = dSq;
                nearest = npc;
            }
            // Raycast only for candidates that would actually improve the visible pick.
            if (dSq <= nearestVisibleSq && LineOfSight.Clear(npc))
            {
                nearestVisibleSq = dSq;
                nearestVisible = npc;
            }
        }
        return nearestVisible ?? nearest;
    }
}

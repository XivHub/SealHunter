using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using XivHubPluginKit.Game;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace SealHunter.Helpers;

public static class MobLocator
{
    /// <summary>Nearest live BattleNpc currently targeting us (i.e. aggro'd) within radius, or null.
    /// <paramref name="ignore"/> is excluded from the search, for callers that are already dealing
    /// with a particular mob and only want to know about the others.</summary>
    public static IBattleNpc? FindNearestAttacker(float radius, IBattleNpc? ignore = null)
    {
        var meId = Player.Object?.GameObjectId ?? 0;
        if (meId == 0) return null;

        IBattleNpc? best = null;
        var bestSq = radius * radius;
        foreach (var o in Plugin.ObjectTable)
        {
            if (o is not IBattleNpc npc) continue;
            if (npc.IsDead || !npc.IsTargetable || npc.TargetObjectId != meId) continue;
            if (ignore != null && npc.GameObjectId == ignore.GameObjectId) continue;
            var dSq = Vector3.DistanceSquared(npc.Position, Player.Position);
            if (dSq <= bestSq)
            {
                bestSq = dSq;
                best = npc;
            }
        }
        return best;
    }

    /// <summary>Whether the mob was spawned by a FATE. FATE spawns often reuse a hunting-log mob's name,
    /// but fighting one drags us into the FATE's level sync and its crowd, so the search skips them.</summary>
    public static bool IsFateMob(IGameObject o) => FateIdOf(o) != 0;

    private static unsafe ushort FateIdOf(IGameObject o) => ((CSGameObject*)o.Address)->FateId;

    /// <summary>Nearest live, attackable BattleNpc matching the given BNpcName id within radius of the hint,
    /// excluding FATE spawns.
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
            if (IsFateMob(npc))
            {
                if (EzThrottler.Throttle($"SH.FateSkip.{npc.GameObjectId}", 30000))
                    Plugin.Telemetry?.Log($"locate: skipping FATE mob {npc.Name} fate={FateIdOf(npc)}");
                continue;
            }

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

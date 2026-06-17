using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;

namespace SealHunter.Helpers;

public static class MobLocator
{
    /// <summary>Nearest live, attackable BattleNpc matching the given BNpcName id within radius of the hint.
    /// Single pass over the ObjectTable, distance-squared, no LINQ allocations.</summary>
    public static IBattleNpc? FindNearest(uint bNpcNameId, Vector3 hint, float radius)
    {
        IBattleNpc? best = null;
        var bestSq = radius * radius;
        foreach (var o in Plugin.ObjectTable)
        {
            if (o is not IBattleNpc npc) continue;
            if (npc.NameId != bNpcNameId || npc.IsDead || !npc.IsTargetable) continue;
            var dSq = Vector3.DistanceSquared(npc.Position, hint);
            if (dSq <= bestSq)
            {
                bestSq = dSq;
                best = npc;
            }
        }
        return best;
    }
}

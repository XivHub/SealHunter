using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;

namespace SealHunter.Helpers;

public static class MobLocator
{
    /// <summary>Nearest live, attackable BattleNpc matching the given BNpcName id within radius of the hint.</summary>
    public static IBattleNpc? FindNearest(uint bNpcNameId, Vector3 hint, float radius)
    {
        return Plugin.ObjectTable
            .OfType<IBattleNpc>()
            .Where(o => o.NameId == bNpcNameId
                        && !o.IsDead
                        && o.IsTargetable
                        && Vector3.Distance(o.Position, hint) <= radius)
            .OrderBy(o => Vector3.Distance(o.Position, hint))
            .FirstOrDefault();
    }

    public static bool AnyAlive(uint bNpcNameId, Vector3 hint, float radius)
        => FindNearest(bNpcNameId, hint, radius) != null;
}

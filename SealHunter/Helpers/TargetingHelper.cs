using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;

namespace SealHunter.Helpers;

public static class TargetingHelper
{
    public static void SetTarget(IGameObject obj) => Plugin.TargetManager.Target = obj;

    public static IGameObject? CurrentTarget => Plugin.TargetManager.Target;

    public static bool InRange(IGameObject obj, float range)
        => Vector3.Distance(Player.Position, obj.Position) <= range;

    /// <summary>Local check (not an IPC call): the current target is a dead/zero-HP battle NPC.</summary>
    public static bool TargetIsDead()
    {
        var t = Plugin.TargetManager.Target;
        if (t is IBattleChara bc)
            return bc.IsDead || bc.CurrentHp == 0;
        return t == null;
    }
}

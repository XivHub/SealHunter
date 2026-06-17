using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;

namespace SealHunter.Helpers;

/// <summary>Detects player death and manual intervention so the bot can recover or pause.</summary>
public static class PlayerGuard
{
    public static bool IsDead() => Player.Available && Player.IsDead;

    /// <summary>
    /// Heuristic manual-control detection: the player is moving while the bot has issued no
    /// navmesh movement, or has manually targeted something other than the bot's intended target.
    /// </summary>
    public static bool PlayerIntervened(IGameObject? intendedTarget)
    {
        var movingManually = Player.IsMoving
                             && !Plugin.Navmesh.IsRunning()
                             && !Plugin.Navmesh.PathfindInProgress();

        var current = Plugin.TargetManager.Target;
        var targetHijacked = intendedTarget != null
                             && current != null
                             && current.GameObjectId != intendedTarget.GameObjectId;

        return movingManually || targetHijacked;
    }
}

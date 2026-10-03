using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using XivHubPluginKit.Game;

namespace SealHunter.Helpers;

/// <summary>Get off the mount next to a mob. On the ground that is one Dismount press. In the air,
/// Dismount only descends straight down, and over water, a cliff edge or a roof the game refuses to
/// land, which leaves the character hovering. So an airborne landing first flies to a point on the
/// navmesh floor beside the mob, lands there, and moves to another such point if it still won't.</summary>
public static class Landing
{
    /// <summary>Close enough to the chosen floor point to start the descent.</summary>
    private const float ArriveDistance = 3f;

    /// <summary>Flying to the point takes a few seconds; past this it is unreachable.</summary>
    private const long ReachTimeoutMs = 12000;

    /// <summary>A descent from hover height plus the dismount animation finishes well inside this.</summary>
    private const long LandTimeoutMs = 5000;

    private static Vector3? spot;
    private static long spotIssuedAt;
    private static long landingSince;
    private static int attempt;
    private static ulong forMob;

    /// <summary>Drive every frame until it returns true: on foot and not falling.</summary>
    public static bool Step(IGameObject mob, float range)
    {
        if (mob.GameObjectId != forMob)
        {
            Reset();
            forMob = mob.GameObjectId;
        }
        if (!Player.Mounted)
        {
            Reset();
            return !Player.IsJumping;
        }

        if (!Plugin.Condition[ConditionFlag.InFlight])
        {
            if (Plugin.Navmesh.IsRunning() || Plugin.Navmesh.PathfindInProgress())
                Plugin.Navmesh.Stop();
            MountHelper.Ground();
            return false;
        }

        var now = Environment.TickCount64;
        if (spot == null)
        {
            spot = PickSpot(mob, range, attempt);
            spotIssuedAt = now;
            landingSince = 0;
            Plugin.Navmesh.PathfindAndMoveTo(spot.Value, true);
            Plugin.Telemetry?.Log($"land: attempt={attempt} spot=({spot.Value.X:0},{spot.Value.Y:0},{spot.Value.Z:0}) dist={Vector3.Distance(Player.Position, spot.Value):0}");
        }

        if (Vector3.Distance(Player.Position, spot.Value) > ArriveDistance)
        {
            if (now - spotIssuedAt > ReachTimeoutMs)
            {
                Plugin.Telemetry?.Log($"land: spot unreachable after {ReachTimeoutMs / 1000}s, trying another");
                NextSpot();
            }
            else if (!Plugin.Navmesh.IsRunning() && !Plugin.Navmesh.PathfindInProgress()
                     && EzThrottler.Throttle("SH.LandRepath", 2000))
            {
                Plugin.Navmesh.PathfindAndMoveTo(spot.Value, true);
            }
            return false;
        }

        // A held fly route pulls the character back up while it tries to descend.
        if (Plugin.Navmesh.IsRunning() || Plugin.Navmesh.PathfindInProgress())
            Plugin.Navmesh.Stop();
        if (landingSince == 0)
            landingSince = now;
        if (now - landingSince > LandTimeoutMs)
        {
            Plugin.Telemetry?.Log($"land: still airborne {LandTimeoutMs / 1000}s at spot, trying another");
            NextSpot();
            return false;
        }
        MountHelper.Ground();
        return false;
    }

    private static void NextSpot()
    {
        attempt++;
        spot = null;
    }

    private static void Reset()
    {
        spot = null;
        attempt = 0;
        landingSince = 0;
    }

    /// <summary>A floor point at attack range from the mob: on the side we approach from first, then
    /// a quarter turn further round per failed attempt. The mob's own position is the last resort,
    /// since a mob always stands on something.</summary>
    private static Vector3 PickSpot(IGameObject mob, float range, int n)
    {
        var dir = Player.Position - mob.Position;
        dir.Y = 0;
        if (dir.LengthSquared() < 0.01f)
            dir = Vector3.UnitX;
        dir = Vector3.Normalize(dir);
        var turn = n * MathF.PI / 2;
        var rotated = new Vector3(
            dir.X * MathF.Cos(turn) - dir.Z * MathF.Sin(turn), 0,
            dir.X * MathF.Sin(turn) + dir.Z * MathF.Cos(turn));
        var standoff = mob.Position + rotated * (range * 0.8f);
        return Plugin.Navmesh.NearestPoint(standoff, 5f, 5f) ?? mob.Position;
    }
}

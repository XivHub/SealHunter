using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using SealHunter.Game;

namespace SealHunter.Helpers;

/// <summary>Job-aware attack range. A bard has no reason to walk into melee, so the approach stops at
/// the range the current job actually attacks from, measured hitbox-to-hitbox like the game does.</summary>
public static class CombatRange
{
    /// <summary>Fallback player hitbox radius when the local object isn't available yet.</summary>
    private const float DefaultHitbox = 0.5f;

    /// <summary>ClassJob roles 3 (ranged DPS, physical and caster) and 4 (healer) attack from range;
    /// 1 (tank) and 2 (melee DPS) have to close.</summary>
    public static bool IsRanged
    {
        get
        {
            if (!Player.Available)
                return false;
            var role = Sheets.ClassJobSheet.GetRowOrDefault((uint)Player.Job)?.Role ?? 0;
            return role is 3 or 4;
        }
    }

    /// <summary>Configured stand-off range for the current job, before hitboxes.</summary>
    public static float BaseRange => IsRanged ? Plugin.C.RangedEngageRange : Plugin.C.MeleeEngageRange;

    /// <summary>Distance from the player's centre at which the current job can attack
    /// <paramref name="target"/>. The game measures range hitbox-to-hitbox, so both radii are added
    /// to the job range — without that, big mobs are engaged from outside their real reach.</summary>
    public static float AttackRange(IGameObject target)
        => BaseRange + (Player.Object?.HitboxRadius ?? DefaultHitbox) + target.HitboxRadius;
}

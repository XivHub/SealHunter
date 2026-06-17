using ECommons.GameHelpers;

namespace SealHunter.Helpers;

/// <summary>Detects player death so the bot can recover.</summary>
public static class PlayerGuard
{
    public static bool IsDead() => Player.Available && Player.IsDead;
}

namespace SealHunter.Combat;

/// <summary>
/// Swappable autorotation backend. Mirrors Questionable's ICombatModule shape.
/// SealHunter sets the target itself; the backend only drives the rotation on whatever is targeted.
/// </summary>
public interface ICombatBackend
{
    string Name { get; }

    /// <summary>Whether the backing plugin is installed and its IPC is reachable.</summary>
    bool Installed { get; }

    /// <summary>Begin autorotation (kills the currently-targeted enemy).</summary>
    void Enable();

    /// <summary>Stop autorotation.</summary>
    void Disable();

    /// <summary>Whether autorotation is currently active.</summary>
    bool IsActive();

    /// <summary>Whether the backend is currently repositioning the character itself. When false,
    /// SealHunter has to keep itself in range and line of sight of the mob during the fight.</summary>
    bool MovesPlayer { get; }
}

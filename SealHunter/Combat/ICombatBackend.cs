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
}

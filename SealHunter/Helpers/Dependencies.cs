using System.Collections.Generic;
using SealHunter.IPC;

namespace SealHunter.Helpers;

/// <summary>Hard-dependency gate: vnavmesh, a teleport provider, and a combat backend must all be present.</summary>
public static class Dependencies
{
    public static bool AllPresent => CheckAll().ok;

    public static (bool ok, string message) CheckAll()
    {
        var missing = new List<string>();

        if (!NavmeshIPC.Installed)
            missing.Add("vnavmesh");
        if (!TeleportIPC.Installed)
            missing.Add(TeleportIPC.Name);
        if (!Plugin.CombatBackend.Installed)
            missing.Add(Plugin.CombatBackend.Name);

        if (missing.Count == 0)
            return (true, "All dependencies present.");

        return (false, "Missing required plugins: " + string.Join(", ", missing));
    }
}

using System.Numerics;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace SealHunter.Helpers;

/// <summary>
/// Converts the dataset's 2D map coordinates into 3D world coordinates for vnavmesh.
/// World X/Z come from Dalamud's own MapLinkPayload conversion (RawX/RawY are milli-units),
/// avoiding hand-rolled map constants. Y (height) is resolved against the navmesh via PointOnFloor.
/// </summary>
public static class MapCoords
{
    /// <summary>World X/Z for a map coordinate. Y is 0 until resolved with <see cref="ResolveFloor"/>.</summary>
    public static Vector3 MapToWorldFlat(uint territoryType, uint mapId, float mapX, float mapY)
    {
        var link = new MapLinkPayload(territoryType, mapId, mapX, mapY);
        return new Vector3(link.RawX / 1000f, 0f, link.RawY / 1000f);
    }

    /// <summary>Snap a flat world point to the navmesh floor; returns the input unchanged if no mesh hit.</summary>
    public static Vector3 ResolveFloor(Vector3 flat)
    {
        // Search a generous vertical band; many overworld camps sit well above/below y=0.
        var hit = Plugin.Navmesh.PointOnFloor(flat with { Y = 1024f }, false, 5f)
                  ?? Plugin.Navmesh.NearestPoint(flat, 100f, 1000f);
        return hit ?? flat;
    }

    /// <summary>World hint (X/Z from the map link, Y snapped to the floor) for a target location.</summary>
    public static Vector3 WorldHint(uint territoryType, uint mapId, float mapX, float mapY)
        => ResolveFloor(MapToWorldFlat(territoryType, mapId, mapX, mapY));

    // --- Hunty's map<->raw helpers, used for aetheryte-marker distance comparison in map space. ---

    public static Vector2 ConvertMarkerToMap(int x, int y, float sizeFactor)
    {
        var num = sizeFactor / 100f;
        return new Vector2(
            ConvertRawToMap((int)((x - 1024) * num * 1000f), sizeFactor),
            ConvertRawToMap((int)((y - 1024) * num * 1000f), sizeFactor));
    }

    private static float ConvertRawToMap(int pos, float scale)
    {
        var num1 = scale / 100f;
        var num2 = pos * num1 / 1000.0f;
        return (40.96f / num1 * ((num2 + 1024.0f) / 2048.0f)) + 1.0f;
    }
}

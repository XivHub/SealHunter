using System.Numerics;
using SealHunter.Game;

namespace SealHunter.Helpers;

/// <summary>
/// Converts the dataset's 2D map coordinates into 3D world coordinates for vnavmesh.
/// World X/Z use the conversion Dalamud's MapLinkPayload applied (see <see cref="MapToWorldAxis"/>).
/// Y (height) is resolved against the navmesh via PointOnFloor.
/// </summary>
public static class MapCoords
{
    /// <summary>World X/Z for a map coordinate. Y is 0 until resolved with <see cref="ResolveFloor"/>.</summary>
    public static Vector3 MapToWorldFlat(uint mapId, float mapX, float mapY)
    {
        var map = Sheets.MapSheet.GetRow(mapId);
        return new Vector3(
            MapToWorldAxis(mapX, map.SizeFactor, map.OffsetX), 0f,
            MapToWorldAxis(mapY, map.SizeFactor, map.OffsetY));
    }

    /// <summary>
    /// Inverse of the game's world-to-map conversion, as Dalamud's API 15 MapLinkPayload computed it:
    /// a 0.05 fudge so a displayed "3.1" does not truncate to 3.0, the result truncated to whole
    /// milli-units (the link's RawX/RawY), then divided back to world units.
    /// </summary>
    private static float MapToWorldAxis(float mapCoord, ushort sizeFactor, short offset)
    {
        const float fudge = 0.05f;
        var scale = sizeFactor / 100f;
        var raw = (int)((((mapCoord + fudge - 1f) * scale / 41f * 2048f) - 1024f) / scale * 1000f) - (offset * 1000);
        return raw / 1000f;
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
    public static Vector3 WorldHint(uint mapId, float mapX, float mapY)
        => ResolveFloor(MapToWorldFlat(mapId, mapX, mapY));

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

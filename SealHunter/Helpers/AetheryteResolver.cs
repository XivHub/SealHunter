using System.Linq;
using System.Numerics;
using SealHunter.Game;

namespace SealHunter.Helpers;

/// <summary>Resolves the nearest aetheryte to a map location. Ported from Hunty's TeleportToNearestAetheryte
/// (originally HuntBuddy). Works in map-coordinate space, so no world conversion is needed here.</summary>
public static class AetheryteResolver
{
    /// <summary>Aetheryte RowId nearest to the given map coords, or 0 if none found.</summary>
    public static uint NearestAetheryte(uint mapId, uint terri, Vector2 mapCoords)
    {
        var map = Sheets.MapSheet.GetRow(mapId);

        var nearestMarkerId = Sheets.MapMarkerSheet
            .SelectMany(x => x)
            .Where(x => x.DataType == 3 && x.RowId == map.MapMarkerRange)
            .Select(marker => new
            {
                distance = Vector2.DistanceSquared(mapCoords, MapCoords.ConvertMarkerToMap(marker.X, marker.Y, map.SizeFactor)),
                rowId = marker.DataKey.RowId
            })
            .OrderBy(x => x.distance)
            .Select(x => (uint?)x.rowId)
            .FirstOrDefault() ?? 0;

        // Special case (kept from Hunty): some maps host their aetheryte on the parent territory.
        if (terri == 399)
            return map.TerritoryType.Value.Aetheryte.Value.RowId;

        foreach (var a in Sheets.AetheryteSheet)
        {
            if (a.IsAetheryte && a.Territory.RowId == terri && a.RowId == nearestMarkerId)
                return a.RowId;
        }

        return 0;
    }
}

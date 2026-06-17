using System.Collections.Generic;
using System.Globalization;

namespace SealHunter.Game;

/// <summary>Resolves a duty-bound hunting-log target's instance zone into a dungeon name + CFC id.
/// (Unlock detection isn't a clean single field on ContentFinderCondition, so it's left to AutoDuty.)</summary>
public static class DutyResolver
{
    public readonly record struct DutyInfo(string Name, uint CfcId, uint Zone);

    private static Dictionary<uint, (uint cfcId, string name)>? byZone;

    public static DutyInfo Resolve(uint zone)
    {
        byZone ??= Build();
        return byZone.TryGetValue(zone, out var d)
            ? new DutyInfo(d.name, d.cfcId, zone)
            : new DutyInfo("Unknown duty", 0, zone);
    }

    private static Dictionary<uint, (uint, string)> Build()
    {
        var map = new Dictionary<uint, (uint, string)>();
        foreach (var cfc in Sheets.ContentFinderConditionSheet)
        {
            var terri = cfc.TerritoryType.RowId;
            if (terri == 0 || cfc.Name.IsEmpty)
                continue;
            map[terri] = (cfc.RowId, CultureInfo.InvariantCulture.TextInfo.ToTitleCase(cfc.Name.ExtractText()));
        }
        return map;
    }
}

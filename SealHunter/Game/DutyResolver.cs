using System.Collections.Generic;
using System.Globalization;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace SealHunter.Game;

/// <summary>Resolves a duty-bound hunting-log target's instance zone into a dungeon name, CFC id,
/// and whether the player has it unlocked (UIState.IsInstanceContentUnlocked, Questionable's method).</summary>
public static class DutyResolver
{
    public readonly record struct DutyInfo(string Name, uint CfcId, uint Zone, bool Unlocked);

    private static Dictionary<uint, (uint cfcId, string name, uint contentId)>? byZone;

    public static unsafe DutyInfo Resolve(uint zone)
    {
        byZone ??= Build();
        if (!byZone.TryGetValue(zone, out var d))
            return new DutyInfo("Unknown duty", 0, zone, false);

        var unlocked = d.contentId == 0 || UIState.IsInstanceContentUnlocked(d.contentId);
        return new DutyInfo(d.name, d.cfcId, zone, unlocked);
    }

    private static Dictionary<uint, (uint, string, uint)> Build()
    {
        var map = new Dictionary<uint, (uint, string, uint)>();
        foreach (var cfc in Sheets.ContentFinderConditionSheet)
        {
            var terri = cfc.TerritoryType.RowId;
            if (terri == 0 || cfc.Name.IsEmpty)
                continue;
            var name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(cfc.Name.ExtractText());
            map[terri] = (cfc.RowId, name, cfc.Content.RowId);
        }
        return map;
    }
}

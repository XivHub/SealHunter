using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace SealHunter.Game;

/// <summary>Lumina sheet handles, mirroring Hunty's Sheets.cs.</summary>
public static class Sheets
{
    public static readonly ExcelSheet<Map> MapSheet;
    public static readonly ExcelSheet<ClassJob> ClassJobSheet;
    public static readonly ExcelSheet<BNpcName> BNpcNameSheet;
    public static readonly ExcelSheet<Aetheryte> AetheryteSheet;
    public static readonly ExcelSheet<GrandCompany> GrandCompanySheet;
    public static readonly SubrowExcelSheet<MapMarker> MapMarkerSheet;
    public static readonly ExcelSheet<TerritoryType> TerritoryTypeSheet;

    static Sheets()
    {
        MapSheet = Plugin.DataManager.GetExcelSheet<Map>()!;
        ClassJobSheet = Plugin.DataManager.GetExcelSheet<ClassJob>()!;
        BNpcNameSheet = Plugin.DataManager.GetExcelSheet<BNpcName>()!;
        AetheryteSheet = Plugin.DataManager.GetExcelSheet<Aetheryte>()!;
        GrandCompanySheet = Plugin.DataManager.GetExcelSheet<GrandCompany>()!;
        MapMarkerSheet = Plugin.DataManager.GetSubrowExcelSheet<MapMarker>()!;
        TerritoryTypeSheet = Plugin.DataManager.GetExcelSheet<TerritoryType>()!;
    }
}

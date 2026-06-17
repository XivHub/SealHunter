using FFXIVClientStructs.FFXIV.Client.Game;

namespace SealHunter.Helpers;

/// <summary>Gear-durability check, ported from ICE's PlayerHelper.NeedsRepair.</summary>
public static class DurabilityGuard
{
    /// <summary>True if any equipped item is at or below the given condition percent.</summary>
    public static unsafe bool NeedsRepair(float belowPercent)
    {
        var im = InventoryManager.Instance();
        if (im == null)
            return false;

        var equipped = im->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped == null || !equipped->IsLoaded)
            return false;

        for (var i = 0; i < equipped->Size; i++)
        {
            var item = equipped->GetInventorySlot(i);
            if (item == null)
                continue;

            var conditionPct = item->Condition / 30000.0 * 100.0;
            if (conditionPct <= belowPercent)
                return true;
        }

        return false;
    }
}

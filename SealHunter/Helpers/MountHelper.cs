using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace SealHunter.Helpers;

/// <summary>Mount/dismount via the Mount Roulette general action (id 9), as ICE does.</summary>
public static class MountHelper
{
    private const uint MountRouletteAction = 9;

    public static unsafe void Mount()
    {
        if (!Player.Mounted && !Player.Mounting && !Player.IsCasting && Player.CanMount)
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, MountRouletteAction);
    }

    public static unsafe void Dismount()
    {
        if (Player.Mounted)
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, MountRouletteAction);
    }
}

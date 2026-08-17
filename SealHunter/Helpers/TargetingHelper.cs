using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;

namespace SealHunter.Helpers;

public static class TargetingHelper
{
    public static void SetTarget(IGameObject obj) => Plugin.TargetManager.Target = obj;

    public static IGameObject? CurrentTarget => Plugin.TargetManager.Target;

    public static bool InRange(IGameObject obj, float range)
        => Vector3.Distance(Player.Position, obj.Position) <= range;

    /// <summary>Local check (not an IPC call): the current target is a dead/zero-HP battle NPC.</summary>
    public static bool TargetIsDead()
    {
        var t = Plugin.TargetManager.Target;
        if (t is IBattleChara bc)
            return bc.IsDead || bc.CurrentHp == 0;
        return t == null;
    }

    /// <summary>Whether a specific battle object is dead or has despawned from the ObjectTable.
    /// Re-resolves by <see cref="IGameObject.GameObjectId"/> each call so a stale captured reference
    /// (a despawned mob whose slot may have been reused) can't report another object's state. Use
    /// this instead of <see cref="TargetIsDead"/> when you need the death of a specific engaged mob,
    /// not whatever the soft-target happens to be — BossMod autorotation may retarget to a stray
    /// aggro mob, and a stray dying must not be mistaken for the hunt mob dying.
    /// <paramref name="expectedNameId"/> guards against ObjectTable slot-id reuse: a despawned
    /// mob's id can be recycled for a freshly-spawned different mob, so we also require the name
    /// id to match before treating the slot as "still our target".</summary>
    public static bool ObjectIsDeadOrGone(IGameObject obj, uint expectedNameId)
    {
        // Indexed lookup, not a table walk: this runs every frame for the whole fight.
        var o = Plugin.ObjectTable.SearchById(obj.GameObjectId);
        if (o == null) return true; // not in the table → despawned
        if (o is not IBattleNpc npc) return true; // slot now holds a non-battle object
        if (npc.NameId != expectedNameId) return true; // slot reused for a different mob
        return npc.IsDead || npc.CurrentHp == 0;
    }
}

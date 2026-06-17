using System.Collections.Generic;
using System.Linq;
using SealHunter.Models;

namespace SealHunter.Game;

/// <summary>One actionable hunt target: a monster with remaining kills and its location.</summary>
public sealed record HuntEntry(
    uint GcKey,
    int Rank,
    HuntingMonster Monster,
    HuntingMonsterLocation Location,
    int Killed,
    int Required)
{
    public int Remaining => Required - Killed;
    public bool IsOpenWorld => Location.IsOpenWorld;
}

/// <summary>Joins the bundled GC dataset with live MonsterNote progress.</summary>
public static class HuntPlan
{
    /// <summary>All incomplete entries for the current GC, both open-world and duty-bound.</summary>
    public static List<HuntEntry> AllIncomplete()
    {
        var entries = new List<HuntEntry>();
        var gcKey = MonsterNoteReader.CurrentGcKey();
        if (gcKey == 0 || !HuntTargetData.Data.JobRanks.TryGetValue(gcKey, out var ranks))
            return entries;

        // Only the currently-unlocked rank is farmable: lower ranks are already complete, and higher
        // ranks are gated behind Grand Company rank (killing their marks wouldn't count yet).
        var currentRank = MonsterNoteReader.CurrentRank(gcKey);
        if (currentRank < 0 || currentRank >= ranks.Count)
            return entries;

        foreach (var status in MonsterNoteReader.GetRankProgress(gcKey, currentRank, ranks[currentRank]))
        {
            if (status.Done || status.Monster.Locations.Count == 0)
                continue;

            entries.Add(new HuntEntry(
                gcKey, currentRank, status.Monster, status.Monster.PrimaryLocation,
                status.Killed, status.Monster.Count));
        }

        return entries;
    }

    /// <summary>Incomplete open-world entries, grouped by territory to minimise teleports.</summary>
    public static List<HuntEntry> IncompleteOpenWorld()
        => AllIncomplete().Where(e => e.IsOpenWorld).OrderBy(e => e.Location.Terri).ToList();

    /// <summary>Incomplete entries that live inside a duty and cannot be auto-farmed.</summary>
    public static List<HuntEntry> IncompleteDuty()
        => AllIncomplete().Where(e => !e.IsOpenWorld).ToList();

    /// <summary>Next open-world target, re-evaluated against live progress.</summary>
    public static HuntEntry? Next() => IncompleteOpenWorld().FirstOrDefault();

    /// <summary>Next open-world target whose monster is not in the temporary skip set.</summary>
    public static HuntEntry? Next(HashSet<uint> skip)
        => IncompleteOpenWorld().FirstOrDefault(e => !skip.Contains(e.Monster.Id));

    /// <summary>Current live state of a specific entry, or null if it is now complete.</summary>
    public static HuntEntry? Refresh(HuntEntry entry)
        => AllIncomplete().FirstOrDefault(e => e.GcKey == entry.GcKey && e.Monster.Id == entry.Monster.Id);
}

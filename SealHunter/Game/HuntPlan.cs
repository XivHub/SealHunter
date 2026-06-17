using System.Collections.Generic;
using System.Linq;
using SealHunter.Models;

namespace SealHunter.Game;

/// <summary>One actionable hunt target: a monster with remaining kills and its location.
/// <c>LogKey</c> is the hunting-log key (a class id, or 10001/10002/10003 for a GC).</summary>
public sealed record HuntEntry(
    uint LogKey,
    int Rank,
    HuntingMonster Monster,
    HuntingMonsterLocation Location,
    int Killed,
    int Required)
{
    public int Remaining => Required - Killed;
    public bool IsOpenWorld => Location.IsOpenWorld;
}

/// <summary>Joins the bundled dataset with live MonsterNote progress for the selected hunting log(s).</summary>
public static class HuntPlan
{
    /// <summary>Which hunting-log key(s) to farm, per the configured mode and the current GC/job.</summary>
    private static IEnumerable<uint> KeysToFarm()
    {
        var gc = MonsterNoteReader.CurrentGcKey();
        var cj = MonsterNoteReader.CurrentClassKey();
        switch (Plugin.C.Mode)
        {
            case HuntMode.GrandCompany:
                if (gc != 0) yield return gc;
                break;
            case HuntMode.ClassJob:
                if (cj != 0) yield return cj;
                break;
            case HuntMode.Both:
                if (gc != 0) yield return gc;
                if (cj != 0) yield return cj;
                break;
        }
    }

    /// <summary>All incomplete entries for the selected log(s), both open-world and duty-bound.</summary>
    public static List<HuntEntry> AllIncomplete()
    {
        var entries = new List<HuntEntry>();
        foreach (var key in KeysToFarm())
        {
            if (!HuntTargetData.Data.JobRanks.TryGetValue(key, out var ranks))
                continue;

            // Only the currently-unlocked rank is farmable: lower ranks are complete, higher ranks
            // are gated (GC rank / class level) and killing their marks wouldn't count yet.
            var currentRank = MonsterNoteReader.CurrentRank(key);
            if (currentRank < 0 || currentRank >= ranks.Count)
                continue;

            foreach (var status in MonsterNoteReader.GetRankProgress(key, currentRank, ranks[currentRank]))
            {
                if (status.Done || status.Monster.Locations.Count == 0)
                    continue;

                entries.Add(new HuntEntry(
                    key, currentRank, status.Monster, status.Monster.PrimaryLocation,
                    status.Killed, status.Monster.Count));
            }
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
        => AllIncomplete().FirstOrDefault(e => e.LogKey == entry.LogKey && e.Monster.Id == entry.Monster.Id);
}

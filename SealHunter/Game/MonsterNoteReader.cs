using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using SealHunter.Models;

namespace SealHunter.Game;

/// <summary>Reads live GC hunting-log progress from MonsterNoteManager.
/// Logic mirrors Hunty's GetMemoryProgress / GetGrandCompany.</summary>
public static unsafe class MonsterNoteReader
{
    /// <summary>Per-monster kill progress for one rank.</summary>
    public readonly record struct MonsterStatus(HuntingMonster Monster, int Killed, bool Done);

    /// <summary>Current GC key (10001/10002/10003), or 0 if the player has no Grand Company.</summary>
    public static uint CurrentGcKey()
    {
        var gc = PlayerState.Instance()->GrandCompany;
        return gc == 0 ? 0u : (uint)(gc + 10000);
    }

    /// <summary>Maps a GC key to its slot in MonsterNoteManager.RankData (Hunty's StaticData.JobInMemory).</summary>
    private static int MemoryIndex(uint gcKey) => gcKey switch
    {
        10001 => 8,
        10002 => 9,
        10003 => 10,
        _ => -1,
    };

    /// <summary>The currently-unlocked hunting-log rank for this GC (0-based, matches the dataset index).
    /// Higher ranks are gated behind Grand Company rank and are NOT yet farmable. Returns -1 if no GC.</summary>
    public static int CurrentRank(uint gcKey)
    {
        var idx = MemoryIndex(gcKey);
        if (idx < 0)
            return -1;
        return MonsterNoteManager.Instance()->RankData[idx].Rank;
    }

    /// <summary>Kill progress for every monster in the given (0-based) rank of a GC log.</summary>
    public static List<MonsterStatus> GetRankProgress(uint gcKey, int rank, HuntingRank huntingRank)
    {
        var result = new List<MonsterStatus>();
        var idx = MemoryIndex(gcKey);
        if (idx < 0)
            return result;

        var manager = MonsterNoteManager.Instance();
        var jobMemory = manager->RankData[idx];
        var progressRank = jobMemory.Rank;

        if (progressRank > rank)
        {
            foreach (var monster in huntingRank.Tasks.SelectMany(t => t.Monsters))
                result.Add(new MonsterStatus(monster, monster.Count, true));
        }
        else if (progressRank < rank)
        {
            foreach (var monster in huntingRank.Tasks.SelectMany(t => t.Monsters))
                result.Add(new MonsterStatus(monster, 0, false));
        }
        else
        {
            foreach (var (task, progress) in huntingRank.Tasks.Zip(jobMemory.RankData.ToArray()))
            {
                foreach (var (monster, i) in task.Monsters.Select((m, i) => (m, i)))
                {
                    var killed = progress.Counts[i];
                    result.Add(new MonsterStatus(monster, killed, killed >= monster.Count));
                }
            }
        }

        return result;
    }
}

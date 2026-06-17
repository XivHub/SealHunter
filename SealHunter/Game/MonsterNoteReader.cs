using System.Collections.Generic;
using System.Linq;
using ECommons.GameHelpers;
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

    /// <summary>Current class hunting-log key: the player's job mapped to its base class (ClassJobParent).
    /// 0 if the job has no hunting log (e.g. jobs without an ARR base class).</summary>
    public static uint CurrentClassKey()
    {
        if (!Player.Available)
            return 0;
        var parent = Sheets.ClassJobSheet.GetRow((uint)Player.Job).ClassJobParent.RowId;
        return MemoryIndex(parent) >= 0 ? parent : 0;
    }

    /// <summary>Maps a hunting-log key (class id or GC key) to its slot in MonsterNoteManager.RankData
    /// (Hunty's StaticData.JobInMemory). Returns -1 for keys with no log.</summary>
    private static int MemoryIndex(uint key) => key switch
    {
        1 => 0,   // Gladiator
        2 => 1,   // Pugilist
        3 => 2,   // Marauder
        4 => 3,   // Lancer
        5 => 4,   // Archer
        6 => 5,   // Conjurer
        7 => 6,   // Thaumaturge
        26 => 7,  // Arcanist
        29 => 11, // Rogue
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

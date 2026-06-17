using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SealHunter.Models;

namespace SealHunter.Game;

/// <summary>Loads the bundled GC hunting-log dataset from the plugin directory.</summary>
public static class HuntTargetData
{
    private static HuntingData? cached;

    public static HuntingData Data => cached ??= Load();

    private static HuntingData Load()
    {
        var path = Path.Combine(Plugin.PluginInterface.AssemblyLocation.Directory!.FullName, "Data", "gc_hunt_targets.json");
        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<HuntingData>(json) ?? new HuntingData();
    }

    /// <summary>First open-world monster in the dataset (used by the Phase 2 vertical-slice debug command).</summary>
    public static (uint gcKey, int rank, HuntingMonster monster)? FirstOpenWorld()
    {
        foreach (var (gcKey, ranks) in Data.JobRanks)
        {
            for (var r = 0; r < ranks.Count; r++)
            {
                var monster = ranks[r].Tasks.SelectMany(t => t.Monsters).FirstOrDefault(m => m.IsOpenWorld);
                if (monster != null)
                    return (gcKey, r, monster);
            }
        }
        return null;
    }
}

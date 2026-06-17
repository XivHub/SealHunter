using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SealHunter.Models;

namespace SealHunter.Game;

/// <summary>Loads the bundled GC hunting-log dataset (embedded resource).</summary>
public static class HuntTargetData
{
    private const string Resource = "SealHunter.Data.hunt_targets.json";

    private static HuntingData? cached;

    public static HuntingData Data => cached ??= Load();

    private static HuntingData Load()
    {
        using var stream = typeof(HuntTargetData).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"Embedded dataset {Resource} not found");
        using var reader = new StreamReader(stream);
        return JsonConvert.DeserializeObject<HuntingData>(reader.ReadToEnd()) ?? new HuntingData();
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

using System;
using System.IO;
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
}

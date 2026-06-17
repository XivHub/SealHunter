using System.Collections.Generic;
using System.Numerics;
using Newtonsoft.Json;

namespace SealHunter.Models;

/// <summary>Bundled GC hunting-log dataset (GC-filtered subset of Hunty's monsters.json).
/// JobRanks is keyed by GC key (10001 Maelstrom / 10002 Twin Adder / 10003 Immortal Flames).</summary>
public class HuntingData
{
    public Dictionary<uint, List<HuntingRank>> JobRanks { get; set; } = new();
}

public class HuntingRank
{
    public List<HuntingTask> Tasks { get; set; } = [];
}

public class HuntingTask
{
    public string Name { get; set; } = string.Empty;
    public List<HuntingMonster> Monsters { get; set; } = [];
}

public class HuntingMonster
{
    /// <summary>BNpcName RowId; equals IBattleNpc.NameId for the live world object.</summary>
    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte Count { get; set; }
    public uint Icon { get; set; }
    public List<HuntingMonsterLocation> Locations { get; set; } = [];

    [JsonIgnore] public HuntingMonsterLocation PrimaryLocation => Locations[0];
    [JsonIgnore] public bool IsOpenWorld => Locations.Count > 0 && Locations[0].IsOpenWorld;
}

public class HuntingMonsterLocation
{
    public uint Terri { get; set; }
    public uint Map { get; set; }
    public uint Zone { get; set; }
    public float xCoord { get; set; }
    public float yCoord { get; set; }

    /// <summary>GC targets with Zone != 0 live inside a duty/instance (e.g. Halatali, Wanderer's Palace)
    /// and cannot be reached in the open world.</summary>
    [JsonIgnore] public bool IsOpenWorld => Zone == 0;
    [JsonIgnore] public Vector2 MapCoords => new(xCoord, yCoord);
}

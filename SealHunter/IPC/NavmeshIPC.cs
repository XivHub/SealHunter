using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using ECommons.EzIpcManager;
using XivHubPluginKit;

namespace SealHunter.IPC;

/// <summary>vnavmesh (awgil/ffxiv_navmesh) wrapper. Copied from ICE's NavmeshIPC; hard dependency.</summary>
public class NavmeshIPC
{
    public const string Name = "vnavmesh";

    public NavmeshIPC() => EzIPC.Init(this, Name);

    public static bool Installed => PluginPresence.IsInstalled(Name);

    [EzIPC("Nav.%m")] public readonly Func<bool> IsReady = null!;
    [EzIPC("Nav.%m")] public readonly Func<float> BuildProgress = null!;
    [EzIPC("Nav.%m")] public readonly Func<bool> Reload = null!;
    [EzIPC("Nav.%m")] public readonly Func<bool> Rebuild = null!;
    [EzIPC("Nav.%m")] public readonly Func<Vector3, Vector3, bool, Task<List<Vector3>>> Pathfind = null!;

    [EzIPC("SimpleMove.%m")] public readonly Func<Vector3, bool, bool> PathfindAndMoveTo = null!;
    [EzIPC("SimpleMove.%m")] public readonly Func<bool> PathfindInProgress = null!;

    [EzIPC("Path.%m")] public readonly Action<List<Vector3>, bool> MoveTo = null!;
    [EzIPC("Path.%m")] public readonly Action Stop = null!;
    [EzIPC("Path.%m")] public readonly Action<bool> SetAlignCamera = null!;
    [EzIPC("Path.%m")] public readonly Func<bool> IsRunning = null!;
    [EzIPC("Path.%m")] public readonly Action<float> SetTolerance = null!;

    [EzIPC("Query.Mesh.%m")] public readonly Func<Vector3, float, float, Vector3?> NearestPoint = null!;
    [EzIPC("Query.Mesh.%m")] public readonly Func<Vector3, bool, float, Vector3?> PointOnFloor = null!;
    [EzIPC("Query.Mesh.%m")] public readonly Func<Vector3, float, float, Vector3?> NearestPointReachable = null!;
}

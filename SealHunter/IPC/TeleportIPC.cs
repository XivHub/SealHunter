using System;
using ECommons.EzIpcManager;
using ECommons.Reflection;

#nullable disable
namespace SealHunter.IPC;

/// <summary>
/// Teleport via Lifestream's IPC. Lifestream owns the whole teleport (cast, wait, retry) and
/// exposes <see cref="IsBusy"/>, so the loop issues ONE teleport and waits on IsBusy rather than
/// re-firing the raw game teleport each frame (which spams "another teleport is already underway").
/// </summary>
public class TeleportIPC
{
    public const string Name = "Lifestream";

    public TeleportIPC() => EzIPC.Init(this, Name, SafeWrapper.AnyException);

    public static bool Installed => DalamudReflector.TryGetDalamudPlugin(Name, out _, false, true);

    /// <summary>Lifestream.Teleport(aetheryteId, subIndex) — returns whether it started.</summary>
    [EzIPC] public Func<uint, byte, bool> Teleport;

    /// <summary>Whether Lifestream is currently busy (teleporting / moving).</summary>
    [EzIPC] public Func<bool> IsBusy;
}

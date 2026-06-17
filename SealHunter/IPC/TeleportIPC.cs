using System;
using Dalamud.Plugin.Ipc;

namespace SealHunter.IPC;

/// <summary>
/// Aetheryte teleport via the Teleporter plugin (Pohky) IPC, adapted from Hunty's TeleportConsumer.
/// Teleporter is the proven aetheryte-by-id teleport provider; presence is probed via the
/// "Teleport.ChatMessage" gate (cached 5s).
/// </summary>
public class TeleportIPC
{
    private bool available;
    private long timeSinceLastCheck;

    private readonly ICallGateSubscriber<bool> isInitialized;
    private readonly ICallGateSubscriber<uint, byte, bool> teleport;

    public TeleportIPC()
    {
        isInitialized = Plugin.PluginInterface.GetIpcSubscriber<bool>("Teleport.ChatMessage");
        teleport = Plugin.PluginInterface.GetIpcSubscriber<uint, byte, bool>("Teleport");
    }

    public bool Installed
    {
        get
        {
            if (timeSinceLastCheck + 5000 > Environment.TickCount64)
                return available;

            try
            {
                timeSinceLastCheck = Environment.TickCount64;
                isInitialized.InvokeFunc();
                available = true;
            }
            catch
            {
                available = false;
            }

            return available;
        }
    }

    /// <summary>Teleport to the given aetheryte RowId. Returns false if the provider is absent/unresponsive.</summary>
    public bool Teleport(uint aetheryteId)
    {
        try
        {
            return teleport.InvokeFunc(aetheryteId, 0);
        }
        catch
        {
            Plugin.Logger.Warning("Teleport plugin is not responding");
            return false;
        }
    }
}

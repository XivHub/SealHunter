using System;
using System.Collections.Generic;
using System.Numerics;
using XivHubPluginKit.UI;

namespace SealHunter.Helpers;

/// <summary>Rolling log of bot activity, surfaced in the main window and (optionally) chat.</summary>
public static class ActivityLog
{
    public readonly record struct Entry(string Time, string Message, Vector4 Color);

    private const int MaxEntries = 60;
    // Ring buffer: oldest entry sits at `head`; `count` tracks the live size. Avoids the O(n)
    // shift that List.RemoveRange(0, k) does on every overflow.
    private static readonly Entry[] buffer = new Entry[MaxEntries];
    private static int head;
    private static int count;

    public static IReadOnlyList<Entry> Entries
    {
        get
        {
            var snapshot = new List<Entry>(count);
            var start = (buffer.Length + head - count) % buffer.Length;
            for (var i = 0; i < count; i++)
                snapshot.Add(buffer[(start + i) % buffer.Length]);
            return snapshot;
        }
    }

    /// <summary>Add an event to the log and echo a chat line (for user-visible milestones).</summary>
    public static void Notify(string message, bool chat = true)
    {
        Add(message, HubStyle.Text);
        if (chat)
            Plugin.ChatGui.Print($"[SealHunter] {message}");
    }

    public static void Good_(string message, bool chat = true)
    {
        Add(message, HubStyle.Good);
        if (chat)
            Plugin.ChatGui.Print($"[SealHunter] {message}");
    }

    public static void Warn_(string message, bool chat = true)
    {
        Add(message, HubStyle.Warn);
        if (chat)
            Plugin.ChatGui.PrintError($"[SealHunter] {message}");
    }

    public static void Clear()
    {
        head = 0;
        count = 0;
    }

    private static void Add(string message, Vector4 color)
    {
        buffer[head] = new Entry(DateTime.Now.ToString("HH:mm:ss"), message, color);
        head = (head + 1) % buffer.Length;
        if (count < buffer.Length) count++;
        Plugin.Telemetry?.Log(message);
    }
}

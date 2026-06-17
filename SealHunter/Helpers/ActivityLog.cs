using System;
using System.Collections.Generic;
using System.Numerics;

namespace SealHunter.Helpers;

/// <summary>Rolling log of bot activity, surfaced in the main window and (optionally) chat.</summary>
public static class ActivityLog
{
    public readonly record struct Entry(string Time, string Message, Vector4 Color);

    private const int MaxEntries = 60;
    private static readonly List<Entry> entries = new();

    public static IReadOnlyList<Entry> Entries => entries;

    private static readonly Vector4 Default = new(0.85f, 0.85f, 0.85f, 1f);
    private static readonly Vector4 Good = new(0.45f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 Warn = new(0.95f, 0.75f, 0.35f, 1f);

    /// <summary>Add an event to the log and echo a chat line (for user-visible milestones).</summary>
    public static void Notify(string message, bool chat = true)
    {
        Add(message, Default);
        if (chat)
            Plugin.ChatGui.Print($"[SealHunter] {message}");
    }

    public static void Good_(string message, bool chat = true)
    {
        Add(message, Good);
        if (chat)
            Plugin.ChatGui.Print($"[SealHunter] {message}");
    }

    public static void Warn_(string message, bool chat = true)
    {
        Add(message, Warn);
        if (chat)
            Plugin.ChatGui.PrintError($"[SealHunter] {message}");
    }

    public static void Clear() => entries.Clear();

    private static void Add(string message, Vector4 color)
    {
        entries.Add(new Entry(DateTime.Now.ToString("HH:mm:ss"), message, color));
        if (entries.Count > MaxEntries)
            entries.RemoveRange(0, entries.Count - MaxEntries);
    }
}

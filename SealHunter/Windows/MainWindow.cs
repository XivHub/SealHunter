using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using SealHunter.Game;
using SealHunter.Helpers;
using SealHunter.Scheduler;

namespace SealHunter.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private static readonly Vector4 Red = new(0.92f, 0.34f, 0.34f, 1f);
        private static readonly Vector4 Green = new(0.45f, 0.82f, 0.45f, 1f);
        private static readonly Vector4 Amber = new(0.96f, 0.74f, 0.34f, 1f);
        private static readonly Vector4 Blue = new(0.40f, 0.66f, 0.95f, 1f);
        private static readonly Vector4 Dim = new(0.60f, 0.60f, 0.60f, 1f);
        private static readonly Vector4 Accent = new(0.85f, 0.74f, 0.42f, 1f);

        public MainWindow(Configuration configuration) : base("SealHunter###SealHunterMain")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(380, 380),
                MaximumSize = new Vector2(900, 1400),
            };
        }

        public void Dispose() { }

        // Cached view state, refreshed a few times a second instead of every frame (the underlying
        // calls hit reflection + game memory + LINQ).
        private long lastRefresh;
        private (bool ok, string message) deps;
        private uint gcKey;
        private List<HuntEntry> open = new();
        private List<HuntEntry> duty = new();

        private void Refresh()
        {
            var now = Environment.TickCount64;
            if (now - lastRefresh < 500 && lastRefresh != 0)
                return;
            lastRefresh = now;

            deps = Dependencies.CheckAll();
            gcKey = MonsterNoteReader.CurrentGcKey();
            if (gcKey != 0)
            {
                open = HuntPlan.IncompleteOpenWorld();
                duty = HuntPlan.IncompleteDuty();
            }
            else
            {
                open = new List<HuntEntry>();
                duty = new List<HuntEntry>();
            }
        }

        public override void Draw()
        {
            Refresh();
            DrawHeader();
            DrawControlBar();
            ImGui.Spacing();
            DrawStatusCard();
            ImGui.Spacing();
            DrawTargets();
            ImGui.Spacing();
            DrawActivityLog();
        }

        private void DrawHeader()
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Accent))
            {
                Icon(FontAwesomeIcon.Crosshairs);
                ImGui.SameLine();
                ImGui.TextUnformatted("SealHunter");
            }
            ImGui.SameLine();
            ImGui.TextDisabled("· GC hunting log");
            ImGui.Separator();
        }

        private void DrawControlBar()
        {
            var (depsOk, depsMsg) = deps;
            var running = SchedulerMain.Running;

            if (!running)
            {
                using (ImRaii.Disabled(!depsOk))
                using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.20f, 0.50f, 0.24f, 1f)))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.26f, 0.62f, 0.30f, 1f)))
                {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Start"))
                        SchedulerMain.EnablePlugin();
                }
            }
            else
            {
                using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.55f, 0.20f, 0.20f, 1f)))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.68f, 0.26f, 0.26f, 1f)))
                {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Stop, "Stop"))
                        SchedulerMain.DisablePlugin();
                }
            }

            ImGui.SameLine();
            using (ImRaii.PushColor(ImGuiCol.Text, depsOk ? Green : Red))
                ImGui.TextUnformatted(depsOk ? "Ready" : depsMsg);
        }

        private void DrawStatusCard()
        {
            ImGui.TextDisabled("State");
            ImGui.SameLine(110);
            using (ImRaii.PushColor(ImGuiCol.Text, StateColor(SchedulerMain.State)))
                ImGui.TextUnformatted(SchedulerMain.State.ToString());

            ImGui.TextDisabled("Doing");
            ImGui.SameLine(110);
            ImGui.TextUnformatted(SchedulerMain.Running ? SchedulerMain.CurrentAction : "—");

            var cur = SchedulerMain.Current;
            ImGui.TextDisabled("Target");
            ImGui.SameLine(110);
            if (cur != null)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, Blue))
                    ImGui.TextUnformatted($"{cur.Monster.Name}  {cur.Killed}/{cur.Required}");
                ImGui.SameLine();
                ImGui.TextDisabled($"({SchedulerMain.CurrentTargetElapsedSeconds}s)");
            }
            else
            {
                ImGui.TextUnformatted("—");
            }
        }

        private void DrawTargets()
        {
            if (gcKey == 0)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, Red))
                    ImGui.TextUnformatted("No Grand Company joined.");
                return;
            }

            ImGui.TextUnformatted($"Open-world remaining: {open.Count}");
            using (ImRaii.PushColor(ImGuiCol.PlotHistogram, Green))
            {
                foreach (var e in open)
                {
                    var frac = e.Required == 0 ? 1f : (float)e.Killed / e.Required;
                    ImGui.ProgressBar(frac, new Vector2(-1, ImGui.GetTextLineHeight() + 4), $"{e.Monster.Name}  {e.Killed}/{e.Required}");
                }
            }

            if (duty.Count > 0)
            {
                ImGui.Spacing();
                if (ImGui.CollapsingHeader($"Duty-bound (manual) — {duty.Count}###duty"))
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, Dim))
                        foreach (var e in duty)
                            ImGui.BulletText($"{e.Monster.Name}  {e.Killed}/{e.Required} — run the dungeon manually");
                }
            }
        }

        private void DrawActivityLog()
        {
            ImGui.TextDisabled("Activity");
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 6f);
            using var child = ImRaii.Child("##log", new Vector2(-1, 150), true);
            if (!child) return;

            var entries = ActivityLog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                using (ImRaii.PushColor(ImGuiCol.Text, Dim))
                    ImGui.TextUnformatted(e.Time);
                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Text, e.Color))
                    ImGui.TextWrapped(e.Message);
            }

            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1f)
                ImGui.SetScrollHereY(1f);
        }

        private static void Icon(FontAwesomeIcon icon)
        {
            using (Plugin.PluginInterface.UiBuilder.IconFontHandle.Push())
                ImGui.TextUnformatted(icon.ToIconString());
        }

        private static Vector4 StateColor(BotState s) => s switch
        {
            BotState.Idle => Dim,
            BotState.Done => Green,
            BotState.Error => Red,
            BotState.Recovering => Amber,
            BotState.PausedForDuty => Amber,
            BotState.PausedForPlayer => Amber,
            _ => Blue,
        };
    }
}

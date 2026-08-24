using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using SealHunter.Game;
using SealHunter.Helpers;
using SealHunter.IPC;
using SealHunter.Scheduler;
using XivHubPluginKit.UI;

namespace SealHunter.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private readonly Configuration cfg;

        public MainWindow(Configuration configuration) : base("SealHunter###SealHunterMain")
        {
            cfg = configuration;
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
        private List<HuntEntry> open = new();
        private List<HuntEntry> duty = new();
        private List<DutyGroup> dutyGroups = new();

        /// <summary>One dungeon's worth of duty-bound marks, resolved and formatted once per refresh
        /// instead of per frame — the grouping, the sheet lookups and the joined mark list are all
        /// too expensive to redo while the section is just sitting on screen.</summary>
        private readonly record struct DutyGroup(uint Zone, string Name, bool Unlocked, bool Runnable, string Marks);

        private void Refresh()
        {
            var now = Environment.TickCount64;
            if (now - lastRefresh < 500 && lastRefresh != 0)
                return;
            lastRefresh = now;

            deps = Dependencies.CheckAll();
            open = HuntPlan.IncompleteOpenWorld();
            duty = HuntPlan.IncompleteDuty();

            var autoDuty = AutoDutyIPC.Installed;
            dutyGroups = duty.GroupBy(e => e.Location.Zone)
                .Select(g =>
                {
                    var info = DutyResolver.Resolve(g.Key);
                    return new DutyGroup(g.Key, info.Name, info.Unlocked, info.Unlocked && autoDuty,
                        string.Join(", ", g.Select(e => $"{e.Monster.Name} {e.Killed}/{e.Required}")));
                })
                .ToList();
        }

        public override void Draw()
        {
            Refresh();
            DrawHeader();
            DrawControlBar();
            DrawModeSelector();
            ImGui.Spacing();
            DrawStatusCard();
            ImGui.Spacing();
            DrawTargets();
            ImGui.Spacing();
            DrawActivityLog();
        }

        private void DrawHeader()
        {
            using (ImRaii.PushColor(ImGuiCol.Text, HubStyle.Accent))
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

            // Start and Stop are never on screen together, so the one that is showing is the
            // window's single primary action.
            using (HubStyle.Primary())
            {
                if (!running)
                {
                    using (ImRaii.Disabled(!depsOk))
                    {
                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Start"))
                            SchedulerMain.EnablePlugin();
                    }
                }
                else if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Stop, "Stop"))
                {
                    SchedulerMain.DisablePlugin();
                }
            }

            ImGui.SameLine();
            using (ImRaii.PushColor(ImGuiCol.Text, depsOk ? HubStyle.Good : HubStyle.Bad))
                ImGui.TextUnformatted(depsOk ? "Ready" : depsMsg);
        }

        private void DrawModeSelector()
        {
            ImGui.SetNextItemWidth(220);
            var mode = (int)cfg.Mode;
            // Locked while running so the active loop can't have its target set shuffled mid-run.
            using (ImRaii.Disabled(SchedulerMain.Running))
            {
                if (ImGui.Combo("Hunt", ref mode, ConfigWindow.ModeLabels, ConfigWindow.ModeLabels.Length))
                {
                    cfg.Mode = (HuntMode)mode;
                    cfg.Save();
                }
            }
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
                using (ImRaii.PushColor(ImGuiCol.Text, HubStyle.Info))
                    ImGui.TextUnformatted($"{cur.Monster.Name}  {cur.Killed}/{cur.Required}");
                ImGui.SameLine();
                ImGui.TextDisabled($"({SchedulerMain.CurrentTargetElapsedSeconds}s)");
            }
            else
            {
                ImGui.TextUnformatted("—");
            }

            DrawRunCard();
        }

        private void DrawRunCard()
        {
            var kills = SchedulerMain.TotalKills;
            var remaining = open.Sum(e => e.Remaining);
            var avg = SchedulerMain.AverageKillSeconds;

            ImGui.Separator();
            ImGui.TextDisabled("Run");
            ImGui.SameLine(110);
            ImGui.TextUnformatted($"{kills} kills this run");
            ImGui.TextDisabled("Remaining");
            ImGui.SameLine(110);
            ImGui.TextUnformatted($"{remaining} kill{(remaining == 1 ? "" : "s")}");
            ImGui.TextDisabled("ETA");
            ImGui.SameLine(110);
            if (avg > 0 && remaining > 0)
            {
                var eta = remaining * avg;
                ImGui.TextUnformatted(eta >= 3600 ? $"{eta / 3600:0.0}h" : $"{eta / 60:0}m{(int)eta % 60}s");
                ImGui.SameLine();
                ImGui.TextDisabled($"({avg:0.0}s/kill avg)");
            }
            else
            {
                ImGui.TextUnformatted("—");
            }
        }

        private void DrawTargets()
        {
            if (open.Count == 0 && duty.Count == 0)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, HubStyle.Faint))
                    ImGui.TextWrapped("Nothing to hunt for the selected mode. Check the mode in settings; for GC you must have joined a Grand Company, for Class/Job you must be on a class that has a hunting log.");
                return;
            }

            ImGui.TextUnformatted($"Open-world remaining: {open.Count}");
            foreach (var e in open)
            {
                var frac = e.Required == 0 ? 1f : (float)e.Killed / e.Required;
                ImGui.ProgressBar(frac, new Vector2(-1, ImGui.GetTextLineHeight() + 4), $"{e.Monster.Name}  {e.Killed}/{e.Required}");
            }

            if (duty.Count > 0)
            {
                ImGui.Spacing();
                if (ImGui.CollapsingHeader($"Duty-bound — {duty.Count}###duty"))
                    DrawDutyGroups();
            }
        }

        private void DrawDutyGroups()
        {
            // Marks are grouped by their instance zone in Refresh() — one dungeon run clears all of them.
            foreach (var group in dutyGroups)
            {
                ImGui.TextUnformatted(group.Name);
                if (!group.Unlocked)
                {
                    ImGui.SameLine();
                    using (ImRaii.PushColor(ImGuiCol.Text, HubStyle.Warn))
                        ImGui.TextUnformatted("(locked)");
                }
                else if (group.Runnable)
                {
                    ImGui.SameLine();
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, $"Run##{group.Zone}"))
                        Plugin.AutoDuty.Run(group.Zone);
                }

                var hint = !group.Unlocked ? " — unlock this dungeon first"
                    : group.Runnable ? "" : " — run the dungeon manually (AutoDuty not installed)";
                using (ImRaii.PushColor(ImGuiCol.Text, HubStyle.Faint))
                    ImGui.BulletText(group.Marks + hint);
            }
        }

        private void DrawActivityLog()
        {
            ImGui.TextDisabled("Activity");
            using var child = ImRaii.Child("##log", new Vector2(-1, 150), true);
            if (!child) return;

            var entries = ActivityLog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                using (ImRaii.PushColor(ImGuiCol.Text, HubStyle.Faint))
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
            BotState.Idle => HubStyle.Faint,
            BotState.Done => HubStyle.Good,
            BotState.Error => HubStyle.Bad,
            BotState.Recovering => HubStyle.Warn,
            BotState.PausedForDuty => HubStyle.Warn,
            _ => HubStyle.Info,
        };
    }
}

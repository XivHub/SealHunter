using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using SealHunter.Game;
using SealHunter.Helpers;
using SealHunter.Scheduler;

namespace SealHunter.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private static readonly Vector4 Red = new(0.9f, 0.3f, 0.3f, 1f);
        private static readonly Vector4 Green = new(0.4f, 0.8f, 0.4f, 1f);

        public MainWindow(Configuration configuration) : base("SealHunter")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(360, 320),
                MaximumSize = new Vector2(900, 1200),
            };
        }

        public void Dispose() { }

        public override void Draw()
        {
            var (depsOk, depsMsg) = Dependencies.CheckAll();

            if (!depsOk)
                ImGui.TextColored(Red, depsMsg);
            else
                ImGui.TextColored(Green, "All dependencies present.");

            ImGui.Separator();

            var running = SchedulerMain.Running;
            if (!running)
            {
                using (ImRaii.Disabled(!depsOk))
                {
                    if (ImGui.Button("Start", new Vector2(120, 0)))
                        SchedulerMain.EnablePlugin();
                }
            }
            else
            {
                if (ImGui.Button("Stop", new Vector2(120, 0)))
                    SchedulerMain.DisablePlugin();
            }

            ImGui.SameLine();
            ImGui.TextUnformatted($"State: {SchedulerMain.State}");

            var current = SchedulerMain.Current;
            if (current != null)
                ImGui.TextUnformatted($"Target: {current.Monster.Name}  {current.Killed}/{current.Required}");

            ImGui.Separator();

            if (MonsterNoteReader.CurrentGcKey() == 0)
            {
                ImGui.TextColored(Red, "No Grand Company joined.");
                return;
            }

            var open = HuntPlan.IncompleteOpenWorld();
            var duty = HuntPlan.IncompleteDuty();

            ImGui.TextUnformatted($"Open-world targets remaining: {open.Count}");
            foreach (var e in open)
            {
                var frac = e.Required == 0 ? 1f : (float)e.Killed / e.Required;
                ImGui.ProgressBar(frac, new Vector2(-1, 0), $"{e.Monster.Name}  {e.Killed}/{e.Required}");
            }

            if (duty.Count > 0)
            {
                ImGui.Separator();
                ImGui.TextColored(Red, $"Duty-bound targets (not automated): {duty.Count}");
                foreach (var e in duty)
                    ImGui.BulletText($"{e.Monster.Name}  {e.Killed}/{e.Required} — run the dungeon manually");
            }
        }
    }
}

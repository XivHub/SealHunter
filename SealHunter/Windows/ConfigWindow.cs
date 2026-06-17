using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SealHunter.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private readonly Configuration cfg;

        public ConfigWindow(Configuration configuration) : base("SealHunter Settings")
        {
            cfg = configuration;
        }

        public void Dispose() { }

        internal static readonly string[] ModeLabels = { "Grand Company log (seals)", "Class/Job log (XP)", "Both (GC, then class)" };

        public override void Draw()
        {
            ImGui.TextDisabled("Hunt");
            var mode = (int)cfg.Mode;
            if (ImGui.Combo("What to hunt", ref mode, ModeLabels, ModeLabels.Length))
            {
                cfg.Mode = (HuntMode)mode;
                cfg.Save();
            }

            ImGui.Separator();
            ImGui.TextDisabled("Combat & search");
            FloatInput("Max engage range (y)", () => cfg.MaxEngageRange, v => cfg.MaxEngageRange = v);
            FloatInput("Mob search radius (y)", () => cfg.MobSearchRadius, v => cfg.MobSearchRadius = v);
            IntInput("Combat timeout (s)", () => cfg.CombatTimeoutSeconds, v => cfg.CombatTimeoutSeconds = v);
            IntInput("Max attempts before skipping a target", () => cfg.MaxConsecutiveScanFailures, v => cfg.MaxConsecutiveScanFailures = v);

            ImGui.Separator();
            ImGui.TextDisabled("Safety");
            BoolInput("Stop on death", () => cfg.StopOnDeath, v => cfg.StopOnDeath = v);
            BoolInput("Return to home point on death", () => cfg.ReturnOnDeath, v => cfg.ReturnOnDeath = v);
            BoolInput("Pause when I take manual control", () => cfg.PauseOnPlayerIntervention, v => cfg.PauseOnPlayerIntervention = v);

            ImGui.Separator();
            ImGui.TextDisabled("Loop");
            BoolInput("Use a mount for long travel", () => cfg.UseMount, v => cfg.UseMount = v);
            BoolInput("Fly when the zone allows it", () => cfg.UseFlight, v => cfg.UseFlight = v);
            BoolInput("Stop when camps are empty", () => cfg.StopWhenNoMobs, v => cfg.StopWhenNoMobs = v);
            IntInput("Respawn wait (s)", () => cfg.RespawnWaitSeconds, v => cfg.RespawnWaitSeconds = v);

            ImGui.Separator();
            if (ImGui.CollapsingHeader("Developer"))
            {
                ImGui.TextDisabled("Streams live activity + state snapshots to a local log server.");
                BoolInput("Enable dev telemetry", () => cfg.DevLog, v => cfg.DevLog = v);
                var url = cfg.DevLogUrl;
                if (ImGui.InputText("Log server URL", ref url, 256))
                {
                    cfg.DevLogUrl = url;
                    cfg.Save();
                }
                ImGui.TextDisabled("e.g. http://192.168.88.248:9999/log");
            }
        }

        private void FloatInput(string label, Func<float> get, Action<float> set)
        {
            var v = get();
            if (ImGui.InputFloat(label, ref v))
            {
                set(v);
                cfg.Save();
            }
        }

        private void IntInput(string label, Func<int> get, Action<int> set)
        {
            var v = get();
            if (ImGui.InputInt(label, ref v))
            {
                set(v);
                cfg.Save();
            }
        }

        private void BoolInput(string label, Func<bool> get, Action<bool> set)
        {
            var v = get();
            if (ImGui.Checkbox(label, ref v))
            {
                set(v);
                cfg.Save();
            }
        }
    }
}

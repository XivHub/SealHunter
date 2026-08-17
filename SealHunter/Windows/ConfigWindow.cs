using System;
using System.Numerics;
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
        private static readonly string[] BackendLabels = { "BossMod Reborn", "RotationSolver Reborn" };
        private static readonly string[] BossModMovementLabels = { "Auto (only for melee jobs)", "Always", "Never" };

        // The backend presence probe is an IPC round-trip; poll it a couple of times a second
        // instead of every frame the window is open.
        private long lastBackendCheck;
        private bool backendInstalled;

        public override void Draw()
        {
            var now = Environment.TickCount64;
            if (now - lastBackendCheck > 500 || lastBackendCheck == 0)
            {
                lastBackendCheck = now;
                backendInstalled = Plugin.CombatBackend.Installed;
            }

            ImGui.TextDisabled("Hunt");
            var mode = (int)cfg.Mode;
            if (ImGui.Combo("What to hunt", ref mode, ModeLabels, ModeLabels.Length))
            {
                cfg.Mode = (HuntMode)mode;
                cfg.Save();
            }

            ImGui.Separator();
            ImGui.TextDisabled("Combat & search");

            var backend = (int)cfg.Backend;
            if (ImGui.Combo("Autorotation plugin", ref backend, BackendLabels, BackendLabels.Length))
            {
                Plugin.SwitchCombatBackend((CombatBackendKind)backend);
                lastBackendCheck = 0; // re-probe the newly selected plugin immediately
            }
            ImGui.SameLine();
            ImGui.TextColored(backendInstalled ? new Vector4(0.4f, 0.9f, 0.4f, 1f) : new Vector4(0.9f, 0.4f, 0.4f, 1f),
                backendInstalled ? "installed" : "not found");

            if (cfg.Backend == CombatBackendKind.BossMod)
            {
                var movement = (int)cfg.BossModMovement;
                if (ImGui.Combo("Let BossMod move me in combat", ref movement, BossModMovementLabels, BossModMovementLabels.Length))
                {
                    cfg.BossModMovement = (BossModMovementMode)movement;
                    cfg.Save();
                }
                ImGui.TextDisabled("Auto keeps melee uptime but leaves ranged jobs where they stand.");
            }

            FloatInput("Melee engage range (y)", () => cfg.MeleeEngageRange, v => cfg.MeleeEngageRange = v);
            FloatInput("Ranged engage range (y)", () => cfg.RangedEngageRange, v => cfg.RangedEngageRange = v);
            ImGui.TextDisabled("Stand-off distance per role; hitbox sizes are added on top.");
            FloatInput("Mob search radius (y)", () => cfg.MobSearchRadius, v => cfg.MobSearchRadius = v);
            IntInput("Combat timeout (s)", () => cfg.CombatTimeoutSeconds, v => cfg.CombatTimeoutSeconds = v);
            IntInput("Max attempts before skipping a target", () => cfg.MaxConsecutiveScanFailures, v => cfg.MaxConsecutiveScanFailures = v);

            ImGui.Separator();
            ImGui.TextDisabled("Safety");
            BoolInput("Stop on death", () => cfg.StopOnDeath, v => cfg.StopOnDeath = v);
            BoolInput("Return to home point on death", () => cfg.ReturnOnDeath, v => cfg.ReturnOnDeath = v);

            ImGui.Separator();
            ImGui.TextDisabled("Loop");
            BoolInput("Use a mount for long travel", () => cfg.UseMount, v => cfg.UseMount = v);
            BoolInput("Fly when the zone allows it", () => cfg.UseFlight, v => cfg.UseFlight = v);
            BoolInput("Sprint on grounded walks", () => cfg.UseSprint, v => cfg.UseSprint = v);
            BoolInput("Stop when camps are empty", () => cfg.StopWhenNoMobs, v => cfg.StopWhenNoMobs = v);
            IntInput("Respawn wait (s)", () => cfg.RespawnWaitSeconds, v => cfg.RespawnWaitSeconds = v);
            IntInput("Stuck timeout (s) before re-pathing", () => cfg.StuckTimeoutSeconds, v => cfg.StuckTimeoutSeconds = v);

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

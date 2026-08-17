using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace SealHunter
{
    public enum HuntMode
    {
        GrandCompany,   // the current GC's hunting log (seals)
        ClassJob,       // the current class/job's hunting log (XP)
        Both,           // GC first, then the current class log
    }

    public enum CombatBackendKind
    {
        BossMod,                // BossMod Reborn, preset-driven
        RotationSolverReborn,   // RSR in Manual mode
    }

    public enum BossModMovementMode
    {
        Auto,     // melee/tank: BossMod moves for uptime; ranged/healer: it stays put
        Always,   // always let BossMod reposition in combat
        Never,    // never; SealHunter owns all movement
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public HuntMode Mode { get; set; } = HuntMode.GrandCompany;

        // Combat / search
        public CombatBackendKind Backend { get; set; } = CombatBackendKind.BossMod;
        public BossModMovementMode BossModMovement { get; set; } = BossModMovementMode.Auto;

        // Stand-off distance per role, before hitbox radii. Melee mirrors the game's ~3y reach;
        // ranged sits inside the 25y action range with room for the mob to drift.
        public float MeleeEngageRange { get; set; } = 2.9f;
        public float RangedEngageRange { get; set; } = 20f;

        public float MobSearchRadius { get; set; } = 60f;
        public int CombatTimeoutSeconds { get; set; } = 60;
        public int MaxConsecutiveScanFailures { get; set; } = 5;

        // Safety
        public bool StopOnDeath { get; set; } = false;
        public bool ReturnOnDeath { get; set; } = true;

        // Loop behaviour
        public bool StopWhenNoMobs { get; set; } = false;
        public int RespawnWaitSeconds { get; set; } = 30;
        public bool UseMount { get; set; } = true;
        public bool UseFlight { get; set; } = true;
        public bool UseSprint { get; set; } = true;
        public int StuckTimeoutSeconds { get; set; } = 8;

        // Developer: live telemetry to a local log server (off by default).
        public bool DevLog { get; set; } = false;
        public string DevLogUrl { get; set; } = "";

        [NonSerialized]
        private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }
    }
}

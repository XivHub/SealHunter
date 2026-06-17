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

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public HuntMode Mode { get; set; } = HuntMode.GrandCompany;

        // Combat / search
        public float MaxEngageRange { get; set; } = 3.5f;
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

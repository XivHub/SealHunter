using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace SealHunter
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        // Combat / search
        public float MaxEngageRange { get; set; } = 3.5f;
        public float MobSearchRadius { get; set; } = 60f;
        public int CombatTimeoutSeconds { get; set; } = 60;
        public int MaxConsecutiveScanFailures { get; set; } = 5;

        // Safety
        public float MinDurabilityPercent { get; set; } = 20f;
        public bool StopOnDeath { get; set; } = false;
        public bool ReturnOnDeath { get; set; } = true;
        public bool PauseOnPlayerIntervention { get; set; } = true;

        // Loop behaviour
        public bool StopWhenNoMobs { get; set; } = false;
        public int RespawnWaitSeconds { get; set; } = 30;
        public bool UseMount { get; set; } = true;

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

using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation.NeoTaskManager;
using XivHubPluginKit;
using SealHunter.Combat;
using SealHunter.Game;
using SealHunter.Helpers;
using SealHunter.IPC;
using SealHunter.Scheduler;
using SealHunter.Windows;

namespace SealHunter
{
    public sealed class Plugin : IDalamudPlugin
    {
        public static string Name => "SealHunter";

        private const string commandName = "/sealhunter";

        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static IDataManager DataManager { get; private set; } = null!;
        [PluginService] public static IPluginLog Logger { get; private set; } = null!;
        [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
        [PluginService] public static IClientState ClientState { get; private set; } = null!;
        [PluginService] public static ICondition Condition { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static ITargetManager TargetManager { get; private set; } = null!;
        [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;

        public static NavmeshIPC Navmesh { get; private set; } = null!;
        public static TeleportIPC Teleport { get; private set; } = null!;
        public static AutoDutyIPC AutoDuty { get; private set; } = null!;

        // Both backends are constructed up front (each only holds IPC gates) so the choice is a
        // config read rather than a rebuild, and switching can stop the one being left behind.
        private static ICombatBackend[] combatBackends = null!;

        public static ICombatBackend CombatBackend => combatBackends[(int)C.Backend];

        /// <summary>Switch autorotation plugin, stopping whichever backend was in use.</summary>
        public static void SwitchCombatBackend(CombatBackendKind kind)
        {
            if (C.Backend == kind)
                return;
            CombatBackend.Disable();
            C.Backend = kind;
            C.Save();
        }

        public static TaskManager TaskManager { get; private set; } = null!;
        public static DevTelemetry Telemetry { get; private set; } = null!;

        public Configuration Configuration { get; init; }
        public static Configuration C { get; private set; } = null!;
        public WindowSystem WindowSystem = new("SealHunter");
        private readonly MainWindow mainWindow;
        private readonly ConfigWindow configWindow;

        public Plugin()
        {
            ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector);
            KitServices.Init(DataManager, Logger, ChatGui, $"[{Name}]");

            // Config first: the combat backend is selected from it.
            this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(PluginInterface);
            C = this.Configuration;

            Navmesh = new NavmeshIPC();
            Teleport = new TeleportIPC();
            AutoDuty = new AutoDutyIPC();
            combatBackends = [new BossModIPC(), new RotationSolverIPC()];
            TaskManager = new TaskManager(new TaskManagerConfiguration { TimeLimitMS = 20000, ShowDebug = false });

            Telemetry = new DevTelemetry("SealHunter", () => C.DevLog, () => C.DevLogUrl,
                err => Logger.Debug($"DevLog post failed: {err}"));

            mainWindow = new MainWindow(this.Configuration);
            configWindow = new ConfigWindow(this.Configuration);
            WindowSystem.AddWindow(mainWindow);
            WindowSystem.AddWindow(configWindow);

            CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the SealHunter window."
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
            Framework.Update += OnFrameworkUpdate;
            ClientState.Logout += OnLogout;
        }

        private void OnFrameworkUpdate(IFramework framework) => SchedulerMain.Tick();

        // Stop on logout so a character switch never resumes the loop on a different character.
        // DisablePlugin stops the loop; ResetLoopState clears the stale target/skip set so the UI
        // and any next Start don't see the previous character's progress.
        private void OnLogout(int type, int code)
        {
            SchedulerMain.DisablePlugin();
            SchedulerMain.ResetLoopState();
        }

        public void Dispose()
        {
            Framework.Update -= OnFrameworkUpdate;
            ClientState.Logout -= OnLogout;
            SchedulerMain.DisablePlugin();
            Telemetry.Dispose();

            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
            PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

            WindowSystem.RemoveAllWindows();
            mainWindow.Dispose();
            configWindow.Dispose();

            CommandManager.RemoveHandler(commandName);

            ECommonsMain.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            var arg = args.Trim().ToLowerInvariant();
            switch (arg)
            {
                case "start":
                    SchedulerMain.EnablePlugin();
                    break;
                case "stop":
                    SchedulerMain.DisablePlugin();
                    break;
                case "dump":
                    RunDump();
                    break;
                default:
                    mainWindow.IsOpen = true;
                    break;
            }
        }

        private static void RunDump()
        {
            var open = Game.HuntPlan.IncompleteOpenWorld();
            var duty = Game.HuntPlan.IncompleteDuty();
            ChatGui.Print($"[SealHunter] Mode {C.Mode}: {open.Count} open-world target(s) remaining, {duty.Count} duty-bound (skipped).");
            if (open.Count == 0 && duty.Count == 0)
                ChatGui.Print("  Nothing to hunt — check mode / GC membership / class hunting log.");
            foreach (var e in open)
                ChatGui.Print($"  [rank {e.Rank + 1}] {e.Monster.Name}: {e.Killed}/{e.Required} (terri {e.Location.Terri})");
            foreach (var e in duty)
                ChatGui.Print($"  [duty] {e.Monster.Name}: {e.Killed}/{e.Required} — run the dungeon manually");
        }

        private void ToggleMainUi() => mainWindow.Toggle();

        private void ToggleConfigUi() => configWindow.Toggle();

        private void DrawUI() => WindowSystem.Draw();
    }
}

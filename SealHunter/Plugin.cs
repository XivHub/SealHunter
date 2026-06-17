using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation.NeoTaskManager;
using SealHunter.Combat;
using SealHunter.Game;
using SealHunter.Helpers;
using SealHunter.IPC;
using SealHunter.Scheduler;
using SealHunter.Scheduler.Tasks;
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
        public static ICombatBackend CombatBackend { get; private set; } = null!;
        public static TaskManager TaskManager { get; private set; } = null!;

        public Configuration Configuration { get; init; }
        public static Configuration C { get; private set; } = null!;
        public WindowSystem WindowSystem = new("SealHunter");
        private readonly MainWindow mainWindow;
        private readonly ConfigWindow configWindow;

        public Plugin()
        {
            ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector);

            Navmesh = new NavmeshIPC();
            Teleport = new TeleportIPC();
            CombatBackend = new BossModIPC();
            TaskManager = new TaskManager(new TaskManagerConfiguration { TimeLimitMS = 20000, ShowDebug = false });

            this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(PluginInterface);
            C = this.Configuration;

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
        }

        private void OnFrameworkUpdate(IFramework framework) => SchedulerMain.Tick();

        public void Dispose()
        {
            Framework.Update -= OnFrameworkUpdate;
            SchedulerMain.DisablePlugin();

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
                case "killone":
                    RunKillOne();
                    break;
                case "dump":
                    RunDump();
                    break;
                default:
                    mainWindow.IsOpen = true;
                    break;
            }
        }

        private static void RunKillOne()
        {
            var (ok, message) = Dependencies.CheckAll();
            if (!ok)
            {
                ChatGui.PrintError($"[SealHunter] {message}");
                return;
            }

            var first = HuntTargetData.FirstOpenWorld();
            if (first is not { } t)
            {
                ChatGui.PrintError("[SealHunter] No open-world GC target found in dataset.");
                return;
            }

            ChatGui.Print($"[SealHunter] killone: {t.monster.Name} (x{t.monster.Count}).");
            Task_KillOne.Enqueue(t.monster);
        }

        private static void RunDump()
        {
            var gc = Game.MonsterNoteReader.CurrentGcKey();
            if (gc == 0)
            {
                ChatGui.Print("[SealHunter] No Grand Company joined.");
                return;
            }

            var open = Game.HuntPlan.IncompleteOpenWorld();
            var duty = Game.HuntPlan.IncompleteDuty();
            ChatGui.Print($"[SealHunter] GC {gc}: {open.Count} open-world target(s) remaining, {duty.Count} duty-bound (skipped).");
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

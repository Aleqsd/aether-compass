using AetherCompass.Core;
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AetherCompass;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public Preferences Preferences { get; set; } = new();
    public bool Locked { get; set; }
}

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IUnlockState Unlocks { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    private readonly WindowSystem windows = new("AetherCompass");
    private readonly GameSnapshotReader reader;
    private readonly MainWindow window;
    private readonly string progressPath;
    private readonly ProgressStore progress;
    private readonly Configuration config;
    private DateTimeOffset nextRead;
    private bool dirty;

    public Plugin()
    {
        config = Pi.GetPluginConfig() as Configuration ?? new Configuration();
        config.Preferences ??= new Preferences();
        progressPath = Path.Combine(Pi.GetPluginConfigDirectory(), "progress.json");
        try { progress = ProgressStore.Load(progressPath); }
        catch (Exception ex)
        {
            Log.Error(ex, "Cannot load progression; preserving original file.");
            // Never overwrite an unreadable user file on the next edit.
            progressPath = Path.Combine(Pi.GetPluginConfigDirectory(), $"progress-recovery-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
            progress = new ProgressStore();
        }
        reader = new GameSnapshotReader(Client, Objects, Data, Condition, Player, Unlocks, Log);
        window = new MainWindow(config, progress, () => dirty = true);
        windows.AddWindow(window);
        Commands.AddHandler("/aethercompass", new CommandInfo(OnCommand) { HelpMessage = "Ouvrir les objectifs de progression. /aethercompass weekly : suivi hebdomadaire." });
        Commands.AddHandler("/goals", new CommandInfo(OnCommand) { HelpMessage = "Ouvrir Aether Compass." });
        Pi.UiBuilder.Draw += Draw;
        Pi.UiBuilder.OpenMainUi += Open;
        Pi.UiBuilder.OpenConfigUi += Open;
        Framework.Update += Update;
    }

    private void OnCommand(string command, string args) { window.RequestWeekly = args.Trim().Equals("weekly", StringComparison.OrdinalIgnoreCase); Open(); }
    private void Open() { window.Expand(); window.IsOpen = true; }
    private void Draw() => windows.Draw();
    private void Update(IFramework framework)
    {
        if (dirty)
        {
            try { Pi.SavePluginConfig(config); progress.Save(progressPath); dirty = false; }
            catch (Exception ex) { Log.Error(ex, "Cannot save progression."); dirty = false; window.SaveError = "Enregistrement impossible ; consulte le journal Dalamud."; }
        }
        var now = DateTimeOffset.UtcNow;
        // Clear immediately on logout, without retaining another character's screen.
        if (!Client.IsLoggedIn) { window.SetSnapshot(null, null); return; }
        if (now < nextRead) return;
        nextRead = now.AddSeconds(2);
        var snapshot = reader.Read();
        window.SetSnapshot(snapshot, reader.LastError);
    }

    public void Dispose()
    {
        Framework.Update -= Update;
        Pi.UiBuilder.Draw -= Draw;
        Pi.UiBuilder.OpenMainUi -= Open;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/aethercompass");
        Commands.RemoveHandler("/goals");
        windows.RemoveAllWindows();
        if (dirty)
        {
            try { Pi.SavePluginConfig(config); progress.Save(progressPath); }
            catch (Exception ex) { Log.Error(ex, "Cannot save progression on unload."); }
        }
    }
}

using DiceModders.UiKit.Native;

namespace DiceModders.UiKit;

/// <summary>The native-screen half of a scope: top bar, Settings, scoreboard and Rules screen.</summary>
public sealed class NativeApi {
    internal NativeApi(UiScope scope) {
        TopBar = new TopBarApi(scope);
        Settings = new SettingsApi(scope);
        Scoreboard = new ScoreboardApi(scope);
        Rules = new RulesApi(scope);
    }

    public TopBarApi TopBar { get; }
    public SettingsApi Settings { get; }
    public ScoreboardApi Scoreboard { get; }
    public RulesApi Rules { get; }
}

using DiceModders.UiKit.Native;

namespace DiceModders.UiKit;

/// <summary>The native-screen half of a scope: top bar, settings, scoreboard and rules.</summary>
public sealed class NativeApi {
    internal NativeApi(UiScope scope) {
        TopBar = new TopBar(scope);
        Settings = new Settings(scope);
        Scoreboard = new Scoreboard(scope);
        Rules = new Rules(scope);
    }

    public TopBar TopBar { get; }
    public Settings Settings { get; }
    public Scoreboard Scoreboard { get; }
    public Rules Rules { get; }
}

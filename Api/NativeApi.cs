using DiceModders.UiKit.Native;

namespace DiceModders.UiKit;

/// <summary>The native-screen half of a scope: the top bar and the Settings screen.</summary>
public sealed class NativeApi {
    internal NativeApi(UiScope scope) {
        TopBar = new TopBar(scope);
        Settings = new Settings(scope);
    }

    public TopBar TopBar { get; }
    public Settings Settings { get; }
}

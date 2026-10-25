namespace DiceModders.UiKit;

/// <summary>Process-wide settings of the library. Host-only: mods never create a runtime or options.</summary>
internal sealed class UiOptions {
    /// <summary>How custom widgets are dressed. Native-first with a managed fallback by default.</summary>
    public TemplatePolicy Templates { get; init; } = TemplatePolicy.PreferNative;

    /// <summary>The layer used by windows that do not call <c>UiWindow.Layer(...)</c>.</summary>
    public Layer DefaultLayer { get; init; } = Layer.AboveGame;

    /// <summary>
    /// Shows the built-in demo windows on the first scene load. Off by default; the UiKit plugin enables it from
    /// <c>BepInEx/config/DiceModders.UiKit.cfg</c> or the <c>--uikit-demo</c> command-line flag.
    /// </summary>
    public bool EnableDemo { get; init; }
}

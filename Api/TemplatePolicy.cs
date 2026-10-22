namespace DiceModders.UiKit;

/// <summary>
/// How custom widgets are dressed.
/// </summary>
public enum TemplatePolicy {
    /// <summary>Use the game's own widget when its template is available, otherwise build a managed fallback.</summary>
    PreferNative,

    /// <summary>Never touch the game's templates; always build the managed widgets (useful for tests and overlays).</summary>
    ManagedOnly,

    /// <summary>Use only the game's widgets; a missing template is an error.</summary>
    NativeOnly,
}

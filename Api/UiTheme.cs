using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DiceModders.UiKit;

/// <summary>
/// Read-only access to the game's own look: font, material, colours and sprites. Shared by every scope.
/// Lookups never throw; a missing or not-yet-loaded asset is returned as <c>null</c>.
/// </summary>
public sealed class UiTheme {
    private readonly AssetCatalog _assets;

    internal UiTheme(AssetCatalog assets) => _assets = assets;

    public TMP_FontAsset? Font => UiGuard.Func("theme.font", () => _assets.Font);

    public Material? FontMaterial => UiGuard.Func("theme.material", () => _assets.FontShadowMaterial);

    public Color TextColor => AssetCatalog.DefaultTextColor;

    public Sprite? Sprite(string? name) => UiGuard.Func("theme.sprite", () => _assets.Sprite(name ?? string.Empty));

    public Sprite? Icon(string? name) => UiGuard.Func("theme.icon", () => _assets.Sprite(name ?? string.Empty));

    public Sprite? Ornament(OrnamentStyle style) => UiGuard.Func("theme.ornament", () => _assets.Ornament(style));

    /// <summary>Names of the game's sprites that contain <paramref name="contains"/> (all of them when null).</summary>
    public IEnumerable<string> SpriteNames(string? contains = null) {
        try {
            return new List<string>(_assets.SpriteNames(contains));
        }
        catch (Exception e) {
            UiGuard.Report("theme.sprite-names", e);
            return Array.Empty<string>();
        }
    }

    internal Sprite? Panel => UiGuard.Func("theme.panel", () => _assets.PanelSprite);

    internal Sprite? Button(AssetCatalog.ButtonVariant variant, AssetCatalog.ButtonState state)
        => UiGuard.Func("theme.button", () => _assets.ButtonSprite(variant, state));
}

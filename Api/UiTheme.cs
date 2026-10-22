using TMPro;
using UnityEngine;

namespace DiceModders.UiKit;

/// <summary>
/// Read-only access to the game's own look: font, material, colours and sprites. Shared by every scope.
/// Use it when a widget needs an icon by the game's sprite name; the library never asks mods to scan assets.
/// </summary>
public sealed class UiTheme {
    private readonly AssetCatalog _assets;

    internal UiTheme(AssetCatalog assets) => _assets = assets;

    public TMP_FontAsset Font => _assets.Font;
    public Material FontMaterial => _assets.FontShadowMaterial;
    public Color TextColor => AssetCatalog.DefaultTextColor;

    public Sprite Sprite(string name) => _assets.Sprite(name);
    public Sprite Icon(string name) => _assets.Sprite(name);
    public Sprite Ornament(OrnamentStyle style) => _assets.Ornament(style);

    /// <summary>Names of the game's sprites that contain <paramref name="contains"/> (all of them when null).</summary>
    public System.Collections.Generic.IEnumerable<string> SpriteNames(string contains = null)
        => _assets.SpriteNames(contains);

    internal Sprite Panel => _assets.PanelSprite;
    internal Sprite Button(AssetCatalog.ButtonVariant variant, AssetCatalog.ButtonState state)
        => _assets.ButtonSprite(variant, state);
}

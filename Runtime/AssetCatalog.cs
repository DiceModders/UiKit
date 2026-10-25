using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace DiceModders.UiKit;

/// <summary>
/// One cache of the game's sprites, materials and fonts for the whole process. Mods never scan assets themselves.
/// Lookups are lazy; a missing or destroyed asset triggers at most one rescan every few seconds, and only when something asks for it.
/// </summary>
internal sealed class AssetCatalog {
    public static readonly Color DefaultTextColor = new(0.969f, 0.940f, 0.893f, 1f);

    private readonly Dictionary<string, Sprite> _sprites = new();
    private readonly Dictionary<string, Material> _materials = new();
    private readonly Dictionary<string, TMP_FontAsset> _fonts = new();

    private bool _scanned;
    private float _nextRescan;
    private TMP_FontAsset? _font;

    public void Invalidate() {
        _sprites.Clear();
        _materials.Clear();
        _fonts.Clear();
        _scanned = false;
        _nextRescan = 0f;
        _font = null;
    }

    private void Scan() {
        _sprites.Clear();
        _materials.Clear();
        _fonts.Clear();

        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.From(typeof(Sprite))))
            if (o?.TryCast<Sprite>() is { } s && !string.IsNullOrEmpty(s.name))
                _sprites.TryAdd(s.name, s);

        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.From(typeof(Material))))
            if (o?.TryCast<Material>() is { } m && !string.IsNullOrEmpty(m.name))
                _materials.TryAdd(m.name, m);

        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.From(typeof(TMP_FontAsset))))
            if (o?.TryCast<TMP_FontAsset>() is { } f && !string.IsNullOrEmpty(f.name))
                _fonts.TryAdd(f.name, f);

        _scanned = true;
        _nextRescan = Time.realtimeSinceStartup + 3f;
    }

    private void EnsureScanned() {
        if (!_scanned) Scan();
    }

    private T? Lookup<T>(Dictionary<string, T> map, string? name) where T : UnityEngine.Object {
        if (string.IsNullOrEmpty(name)) return null;
        EnsureScanned();
        if (map.TryGetValue(name, out var found) && found != null) return found;

        if (Time.realtimeSinceStartup < _nextRescan) return null;
        Scan();
        return map.TryGetValue(name, out found) && found != null ? found : null;
    }

    public Sprite? Sprite(string? name) => Lookup(_sprites, name);
    public Material? Material(string? name) => Lookup(_materials, name);
    public TMP_FontAsset? FontAsset(string? name) => Lookup(_fonts, name);

    public IEnumerable<string> SpriteNames(string? contains = null) {
        EnsureScanned();
        foreach (var pair in _sprites) {
            if (pair.Value == null) continue;
            if (contains == null || pair.Key.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
                yield return pair.Key;
        }
    }

    public TMP_FontAsset? Font {
        get {
            if (_font != null) return _font;

            var named = FontAsset("Main-Font-Normal");
            if (named != null) return _font = named;

            var live = FontFromLive();
            if (live != null) return _font = live;

            try { return _font = TMP_Settings.defaultFontAsset; }
            catch { return null; }
        }
    }

    public Material? FontShadowMaterial => Material("Main-Font-Normal-Shadow");

    public Sprite? PanelSprite => Sprite("Background-3") ?? Sprite("Background-4");

    public Sprite? ButtonSprite(ButtonVariant variant, ButtonState state)
        => Sprite($"Button-{(int)variant}-{state}");

    public Sprite? Ornament(OrnamentStyle style) => style switch {
        OrnamentStyle.Border => Sprite("Ornament-1"),
        OrnamentStyle.BorderWithTwoCorners => Sprite("Ornament-2"),
        OrnamentStyle.BorderWithFourCorners => Sprite("Ornament-5"),
        OrnamentStyle.Classic => Sprite("Ornament-3"),
        _ => null,
    };

    private TMP_FontAsset? FontFromLive() {
        try {
            foreach (var text in UnityEngine.Object.FindObjectsOfType<TMP_Text>()) {
                if (text == null || text.font == null || !text.gameObject.activeInHierarchy) continue;
                return text.font;
            }
        }
        catch {
            // The game's UI may not exist yet. The named lookup will try again later.
        }
        return null;
    }

    internal enum ButtonVariant { Regular = 1, Selected = 2 }
    internal enum ButtonState { Ready, Hovered, Pressed, Disabled }
}

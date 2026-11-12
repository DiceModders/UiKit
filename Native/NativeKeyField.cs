using System;
using DiceModders.UiKit.Custom;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameKeySetting = DiceKingdoms.Meta.SettingInterfaceInputPath;

namespace DiceModders.UiKit.Native;

/// <summary>
/// A key box cloned from the game's own key setting (Settings > Input). The game's binding component is removed before the clone wakes up,
/// so nothing is rebound in the game; the art and the key text stay. A click on the box calls the row's callback (a mod then captures the key
/// and sets the text).
/// </summary>
internal sealed class NativeKeyField {
    private static GameKeySetting? _template;
    private static float _nextSearch;

    public GameObject? Go { get; private set; }
    public RectTransform? Rt { get; private set; }
    private TMP_Text? _text;

    public static NativeKeyField? Create(Transform? parent, string text, Action? clicked) {
        var prefab = Template();
        if (prefab == null) return null;

        var go = UnityEngine.Object.Instantiate(prefab.gameObject, Staging.Root);
        go.name = "DM Key";
        NativeButton.StripForeign(go);

        var binding = go.GetComponent<GameKeySetting>();
        var keyRect = binding != null ? binding.key : null;
        TMP_Text? label = binding != null && binding.keyLabel != null ? binding.keyLabel.visual : null;
        if (binding != null) UnityEngine.Object.DestroyImmediate(binding);
        label ??= go.GetComponentInChildren<TMP_Text>(true);

        var area = keyRect != null ? keyRect : go.GetComponent<RectTransform>();
        var graphic = area != null ? area.GetComponent<Graphic>() : null;
        if (graphic == null && area != null) {
            var hit = area.gameObject.AddComponent<Image>();   // an invisible hit area when the box itself has no graphic
            hit.color = new Color(0f, 0f, 0f, 0f);
            graphic = hit;
        }
        if (graphic != null) graphic.raycastTarget = true;

        go.transform.SetParent(parent, false);
        go.SetActive(true);

        var field = new NativeKeyField { Go = go, Rt = go.GetComponent<RectTransform>(), _text = label };
        field.SetText(text);
        if (area != null) {
            var receiver = area.gameObject.AddComponent<PointerReceiver>();
            receiver.Bind(area);
            PointerRegistry.SetPointer(receiver, e => {
                if (e.Kind != UiPointerKind.Click || e.Button != 0) return;
                try { clicked?.Invoke(); }
                catch (Exception ex) { UiLog.Error("native key field: " + ex); }
            });
        }
        return field;
    }

    public void SetText(string? text) {
        if (_text != null) _text.text = string.IsNullOrEmpty(text) ? "-" : text;
    }

    public void Destroy() {
        if (Go != null) UnityEngine.Object.Destroy(Go);
        Go = null;
    }

    /// <summary>The game's key setting prefab (FindObjectsOfTypeAll also returns prefab assets the settings screen references).</summary>
    private static GameKeySetting? Template() {
        if (_template != null) return _template;
        float now = Time.realtimeSinceStartup;
        if (now < _nextSearch) return null;
        _nextSearch = now + 1f;
        try {
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<GameKeySetting>())) {
                var control = o?.TryCast<GameKeySetting>();
                if (control != null && !Staging.IsOurs(control.transform)) return _template = control;
            }
        }
        catch (Exception e) { UiLog.Once("templates:key", "native key template search failed: " + e.Message); }
        return null;
    }
}

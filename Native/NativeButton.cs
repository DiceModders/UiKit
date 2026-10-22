using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GameAudioSources = DiceKingdoms.Interface.AudioSources;
using GameButton = DiceKingdoms.Interface.BaseButton;
using GameButtonIcon = DiceKingdoms.Interface.ButtonIcon;
using GameButtonLabel = DiceKingdoms.Interface.ButtonLabel;
using GameToolTip = DiceKingdoms.Interface.HasToolTip;

namespace DiceModders.UiKit.Native;

/// <summary>
/// A button that is a real clone of one of the game's own buttons. Keeping the game's <c>BaseButton</c> gives the clone the
/// native hover/pressed/disabled art, cursor, sounds, drop shadow and label tint. Clicks use the game's own <c>onClick</c>
/// event; the donor's persistent Unity listeners are dropped before the clone wakes up.
/// </summary>
internal sealed class NativeButton {
    public const float Height = 32f;

    /// <summary>Sliced Button-1 art has 16-unit borders; exactly 32 leaves hairlines from rounding, so stay a hair below.</summary>
    public const float SlicedFit = 31.75f;

    private const float IconFill = 0.82f;
    private const float BarIconFill = 0.7f;

    public GameObject Go { get; private set; }
    public RectTransform Rt { get; private set; }
    public Image Face { get; private set; }
    public Button UnityButton { get; private set; }
    public GameButton Game { get; private set; }
    public TMP_Text Text { get; private set; }
    public GameToolTip Tip { get; private set; }

    public bool Selected { get; private set; } = true;
    public bool IsAlive => Go != null;

    private float _designSize;
    private Vector2 _iconBox = new(24f, 24f);
    private float _iconScale = 1f;
    private float _iconFill = IconFill;

    private Image _dim;
    private GameButton.State _state = GameButton.State.READY;
    private bool _stateEvents;
    private Action _click;
    private bool _clickWired;

    private NativeButton() { }

    // ---------------------------------------------------------------- creation

    /// <summary>Text button, cloned from the Settings Close button.</summary>
    public static NativeButton CreateLabel(TemplateCatalog templates, Transform parent, string name, string text,
                                          float fontSize, Action click, string tooltip = null) {
        var template = templates?.LabelButton();
        if (template == null) return null;

        var button = Clone(template, parent, name, tooltip);
        if (button == null) return null;

        var label = button.Go.GetComponent<GameButtonLabel>();
        button.Text = label != null && label.label != null ? label.label.visual : null;
        if (button.Text == null) button.Text = button.Go.GetComponentInChildren<TMP_Text>(true);
        if (button.Text != null) {
            button.Text.text = text ?? string.Empty;
            FitInRect(button.Text, fontSize);
        }

        button.SetClick(click);
        return button;
    }

    /// <summary>
    /// Icon button: the same label-button clone as <see cref="CreateLabel"/>, with an icon in place of the text, so it has
    /// exactly the native size, outline, shadow, hover/pressed art, cursor and sounds.
    /// </summary>
    public static NativeButton CreateIcon(TemplateCatalog templates, Transform parent, string name, Sprite icon,
                                          Action click, string tooltip) {
        var template = templates?.LabelButton();
        if (template == null) return null;

        var button = Clone(template, parent, name, tooltip);
        if (button == null) return null;

        var label = button.Go.GetComponent<GameButtonLabel>();
        var text = label != null && label.label != null
            ? label.label.visual
            : button.Go.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = string.Empty;

        var top = button.Go.transform.Find("Top");
        var holder = top != null ? top.GetComponent<RectTransform>() : button.Rt;
        var iconRect = UiFactory.NewRect("Icon", holder);
        var image = iconRect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        button._designSize = SlicedFit;
        button._iconBox = new Vector2(24f, 24f);
        button.SetIcon(icon);
        button.SetClick(click);
        return button;
    }

    /// <summary>Top-bar icon button, cloned from the game's own bar button (native 48-unit art).</summary>
    public static NativeButton CreateBarIcon(TemplateCatalog templates, Transform parent, string name, Sprite icon,
                                             Action click, string tooltip) {
        var template = templates?.IconButton();
        if (template == null) return null;

        var button = Clone(template, parent, name, tooltip);
        if (button == null) return null;

        button._iconFill = BarIconFill;
        var templateRect = template.GetComponent<RectTransform>();
        if (templateRect != null)
            button._designSize = Mathf.Min(templateRect.rect.width, templateRect.rect.height);

        var donorImage = FindIconImageIn(template);
        if (donorImage != null) {
            var size = donorImage.rectTransform.rect.size;
            button._iconBox = new Vector2(Mathf.Min(size.x, size.y), Mathf.Min(size.x, size.y));
        }

        button.SetIcon(icon);
        button.SetClick(click);
        return button;
    }

    private static NativeButton Clone(GameObject template, Transform parent, string name, string tooltip) {
        // Parked under an inactive parent: the clone's scripts do not run until it enters the real UI.
        var go = UnityEngine.Object.Instantiate(template, Staging.Root);
        go.name = name;
        StripForeign(go);

        var unity = go.GetComponent<Button>();
        if (unity == null) {
            UnityEngine.Object.Destroy(go);
            return null;
        }

        unity.onClick = new Button.ButtonClickedEvent();
        unity.interactable = true;

        var tip = go.GetComponent<GameToolTip>();
        if (string.IsNullOrWhiteSpace(tooltip)) {
            if (tip != null) {
                UnityEngine.Object.DestroyImmediate(tip);
                tip = null;
            }
        }
        else {
            tip = ConfigureTooltip(go, tip, tooltip);
        }

        // A template may contain more than one BaseButton (a row, its checkbox, an info icon, ...). Repair every one
        // before anything can wake up; a null AudioSources reference makes BaseButton.Update throw on every hover.
        RepairBaseButtons(go);

        var game = go.GetComponent<GameButton>();

        go.transform.SetParent(parent, false);
        go.SetActive(true);

        // Awake may have cleared the reference or the prefab's sounds may be missing. Re-check after activation,
        // then wrap the game's own state callback so a bad sound reference can never kill Update every frame.
        if (game != null && game.audioSources == null) {
            UiLog.Once("native:no-audio-late:" + go.name,
                $"cloned control '{go.name}' lost its AudioSources after Awake; removed BaseButton to avoid hover errors");
            UnityEngine.Object.DestroyImmediate(game);
            game = null;
        }
        GuardStateEvents(game, go);

        var button = new NativeButton {
            Go = go,
            Rt = go.GetComponent<RectTransform>(),
            Face = go.GetComponent<Image>(),
            UnityButton = unity,
            Game = game,
            Tip = tip,
        };

        if (button.Game == null)
            UiLog.Once("native:no-base:" + template.name, $"native button '{template.name}' has no BaseButton; cursor/sounds will be missing.");
        if (tip != null) tip.UpdateInitialText(tooltip);
        return button;
    }

    /// <summary>
    /// Repairs references that are valid on a live donor but can be null when the donor is a prefab. Every cloned
    /// BaseButton is handled, including ones on children of composite controls (toggles, sliders, dropdowns).
    /// BaseButton.Update plays hover/click sounds through AudioSources; when that reference cannot be restored the
    /// component is removed before Awake so it can never throw every frame.
    /// </summary>
    internal static void RepairBaseButtons(GameObject root) {
        if (root == null) return;

        GameButton[] buttons;
        try { buttons = root.GetComponentsInChildren<GameButton>(true); }
        catch (Exception e) {
            UiLog.Once("native:repair-scan", "could not scan cloned controls for BaseButton: " + e.Message);
            return;
        }

        var donor = FindLiveDonor();
        GameAudioSources audio = donor != null ? donor.audioSources : null;

        foreach (var game in buttons) {
            if (game == null) continue;

            bool safe = false;
            try {
                var unity = game.GetComponent<Button>();
                if (unity != null) {
                    if (game.unityButton == null) game.unityButton = unity;

                    if (game.audioSources == null) {
                        if (audio == null) audio = FindAudioSources();
                        if (audio != null) game.audioSources = audio;
                    }

                    // A prefab can omit one of the sounds; AudioSources.Play is not defensive about that.
                    if (game.hoverSound == null && donor != null) game.hoverSound = donor.hoverSound;
                    if (game.clickSound == null && donor != null) game.clickSound = donor.clickSound;

                    safe = game.audioSources != null && game.hoverSound != null && game.clickSound != null;
                }
            }
            catch (Exception e) {
                UiLog.Once("native:repair-refs", "could not repair a cloned BaseButton: " + e.Message);
            }

            if (safe) continue;

            UiLog.Once("native:no-audio:" + root.name,
                $"cloned control '{root.name}' had a BaseButton without usable audio references; removed it to avoid hover errors");
            UnityEngine.Object.DestroyImmediate(game);
        }
    }

    private static GameButton FindLiveDonor() {
        try {
            foreach (var button in UnityEngine.Object.FindObjectsOfType<GameButton>()) {
                if (button == null || button.gameObject == null || Staging.IsOurs(button.transform)) continue;
                if (button.audioSources != null && button.hoverSound != null && button.clickSound != null)
                    return button;
            }
        }
        catch {
            // Fall through to the singleton lookup.
        }
        return null;
    }

    /// <summary>
    /// Replaces BaseButton's own state callback with a guarded wrapper. The game callback plays hover/click sounds and
    /// can throw from inside native code when a cloned prefab has an incomplete audio reference; catching it here keeps
    /// BaseButton.Update alive and avoids the infinite exception loop that freezes the game.
    /// </summary>
    private static void GuardStateEvents(GameButton game, GameObject go) {
        if (game == null || go == null) return;

        string label = go.name;
        try {
            var original = game.onStateChange;
            Action<GameButton.State, GameButton.State> safe = (previous, next) => {
                try {
                    if (game == null || game.audioSources == null ||
                        game.hoverSound == null || game.clickSound == null) return;
                    original?.Invoke(previous, next);
                }
                catch (Exception e) {
                    UiLog.Once("native:state:" + label, "native button state callback failed: " + e.Message);
                }
            };

            var native = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GameButton.State, GameButton.State>>(safe);
            game.onStateChange = native;
            Il2CppDelegates.Hold(go, new object[] { safe, native, original });
        }
        catch (Exception e) {
            UiLog.Once("native:state-install:" + label, "could not guard native button state callback: " + e.Message);
        }
    }

    private static GameAudioSources FindAudioSources() {
        try {
            var audio = GameAudioSources.instance;
            if (audio != null) return audio;
        }
        catch {
            // Fall through to a scene lookup.
        }

        try { return UnityEngine.Object.FindObjectOfType<GameAudioSources>(); }
        catch { return null; }
    }

    private static GameToolTip ConfigureTooltip(GameObject go, GameToolTip tip, string text) {
        if (tip == null) tip = go.AddComponent<GameToolTip>();
        tip.localizedInitialText = new UnityEngine.Localization.LocalizedString();
        tip.initialText = text ?? string.Empty;
        return tip;
    }

    /// <summary>Removes leftovers of earlier clones and anything that would re-localize or re-label the clone.</summary>
    internal static void StripForeign(GameObject go) {
        var transform = go.transform;
        for (int i = transform.childCount - 1; i >= 0; i--) {
            var child = transform.GetChild(i);
            if (child != null && child.name.StartsWith("DM")) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        try {
            foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (behaviour == null) continue;
                string description = behaviour.ToString();
                if (description != null && description.Contains("Localize"))
                    UnityEngine.Object.DestroyImmediate(behaviour);
            }
        }
        catch (Exception e) {
            UiLog.Once("native:strip-localization", "could not strip localization components: " + e.Message);
        }
    }

    // ---------------------------------------------------------------- content

    public void SetText(string value) {
        if (Text != null) Text.text = value ?? string.Empty;
    }

    public void SetIcon(Sprite sprite) {
        if (sprite == null || Go == null) return;

        ReduceAtlasBleed(sprite);
        var nativeIcon = Go.GetComponent<GameButtonIcon>();
        if (nativeIcon != null) {
            nativeIcon.Icon = sprite;
        }
        else {
            var image = EnsureIconImage();
            if (image != null) image.sprite = sprite;
        }
        NormalizeIcon();
    }

    /// <summary>The icon atlas is tightly packed; sharper mips keep neighbouring entries from bleeding into small icons.</summary>
    private static void ReduceAtlasBleed(Sprite sprite) {
        try {
            var texture = sprite.texture;
            if (texture == null || texture.mipMapBias <= -2f) return;
            texture.mipMapBias = -2f;
        }
        catch (Exception e) {
            UiLog.Once("native:atlas-bias", "could not adjust the icon atlas: " + e.Message);
        }
    }

    private Image EnsureIconImage() {
        var existing = FindIconImageIn(Go);
        if (existing != null) return existing;
        if (Rt == null) return null;

        var top = Go.transform.Find("Top");
        var holder = top != null ? top.GetComponent<RectTransform>() : Rt;
        var iconRect = UiFactory.NewRect("Icon", holder);
        var image = iconRect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private static Image FindIconImageIn(GameObject root) {
        if (root == null) return null;

        var direct = root.transform.Find("Top/Icon");
        var image = direct != null ? direct.GetComponent<Image>() : null;
        if (image != null) return image;

        Image fallback = null;
        foreach (var candidate in root.GetComponentsInChildren<Image>(true)) {
            if (candidate == null || candidate.gameObject == root) continue;
            if (Staging.IsOurs(candidate.transform)) continue;
            if (candidate.name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;

            string name = candidate.name.ToLowerInvariant();
            if (name.Contains("shadow") || name.Contains("background") ||
                name.Contains("outline") || name.Contains("border")) continue;
            if (fallback == null) fallback = candidate;
        }
        return fallback;
    }

    private void NormalizeIcon() {
        var image = FindIconImageIn(Go);
        if (image == null || _iconBox.x < 1f) return;

        var rect = image.rectTransform;
        image.preserveAspect = true;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.sizeDelta = _iconBox * (_iconFill * _iconScale);
    }

    public void FitIcon(float buttonSize) {
        if (_designSize < 1f) return;
        _iconScale = Mathf.Clamp(buttonSize / _designSize, 0.25f, 4f);
        NormalizeIcon();
    }

    public void SetTooltip(string text) {
        if (Go == null || string.IsNullOrWhiteSpace(text)) return;

        if (Tip == null) {
            Tip = ConfigureTooltip(Go, null, text);
            return;
        }
        Tip.UpdateInitialText(text);
    }

    public void SetVisible(bool visible) {
        if (Go != null && Go.activeSelf != visible) Go.SetActive(visible);
    }

    public void SetInteractable(bool on) {
        if (Go == null) return;
        if (Game != null) Game.Interactable = on;
        else if (UnityButton != null) UnityButton.interactable = on;
        UpdateDim();
    }

    // ---------------------------------------------------------------- click

    public void SetClick(Action click) {
        _click = click;
        if (_clickWired || Go == null) return;
        _clickWired = true;

        Action wrapped = OnClick;
        if (Game != null) {
            var native = Il2CppDelegates.ToIl2CppAction(wrapped);
            Game.onClick = native;
            Il2CppDelegates.Hold(Go, new object[] { wrapped, native });
        }
        else {
            var native = Il2CppDelegates.ToUnityAction(wrapped);
            UnityButton.onClick.AddListener(native);
            Il2CppDelegates.Hold(Go, new object[] { wrapped, native });
        }
    }

    private void OnClick() {
        try { _click?.Invoke(); }
        catch (Exception e) { UiLog.Error("native button click: " + e); }
        Deselect();
    }

    /// <summary>Unity keeps a clicked button selected until the selection is cleared.</summary>
    public static void Deselect() {
        try {
            var events = EventSystem.current;
            if (events != null) events.SetSelectedGameObject(null);
        }
        catch {
            // The event system is optional during teardown.
        }
    }

    // ---------------------------------------------------------------- layout

    /// <summary>Sets layout and rect size. The art's 9-slice borders are scaled down below the native height.</summary>
    public void SetSize(float width, float height) {
        if (Go == null) return;

        width = Fit(width);
        height = Fit(height);

        var layout = UiFactory.Layout(Rt);
        if (layout != null) {
            layout.minWidth = layout.preferredWidth = width;
            layout.minHeight = layout.preferredHeight = height;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;
        }
        if (Rt != null) Rt.sizeDelta = new Vector2(width, height);

        if (Face != null && height < 31f)
            Face.pixelsPerUnitMultiplier = 32f / Mathf.Max(8f, height);
        if (_dim != null && Face != null)
            _dim.pixelsPerUnitMultiplier = Face.pixelsPerUnitMultiplier;
    }

    public static float Fit(float value) => value > 31f && value < 33f ? SlicedFit : value;

    // ---------------------------------------------------------------- selected / unselected

    /// <summary>
    /// Selected uses the game's bright art; unselected uses the dark disabled art as an overlay, hidden while hovering
    /// or pressing so the native hover/pressed art still shows.
    /// </summary>
    public void SetSelected(bool selected) {
        Selected = selected;
        if (!selected) EnsureDim();
        UpdateDim();
    }

    private void EnsureDim() {
        if (_dim != null || Go == null || UnityButton == null) return;

        var dark = UnityButton.spriteState.disabledSprite;
        if (dark == null || Face == null) return;

        var rect = UiFactory.NewRect("DM Dim", Rt);
        UiFactory.Stretch(rect);
        rect.SetAsFirstSibling();

        _dim = rect.gameObject.AddComponent<Image>();
        _dim.sprite = dark;
        _dim.type = Face.type;
        _dim.pixelsPerUnitMultiplier = Face.pixelsPerUnitMultiplier;
        _dim.raycastTarget = false;

        if (Game == null) return;
        try {
            Action<GameButton.State, GameButton.State> changed = (previous, next) => {
                _state = next;
                UpdateDim();
            };
            var native = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GameButton.State, GameButton.State>>(changed);
            Game.onStateChange = Il2CppSystem.Delegate.Combine(Game.onStateChange, native)
                .Cast<Il2CppSystem.Action<GameButton.State, GameButton.State>>();
            Il2CppDelegates.Hold(Go, new object[] { changed, native });
            _stateEvents = true;
        }
        catch (Exception e) {
            UiLog.Once("native:state-events", "native button state events unavailable: " + e.Message);
        }
    }

    private void UpdateDim() {
        if (_dim == null) return;
        bool show = !Selected && UnityButton != null && UnityButton.interactable &&
                    (!_stateEvents || _state == GameButton.State.READY);
        if (_dim.gameObject.activeSelf != show) _dim.gameObject.SetActive(show);
    }

    private static void FitInRect(TMP_Text text, float maxSize) {
        if (text == null) return;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = 8f;
        text.fontSizeMax = maxSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    public void Destroy() {
        if (Go != null) UnityEngine.Object.Destroy(Go);
        Go = null;
    }
}

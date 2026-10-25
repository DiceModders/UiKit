using System;
using UnityEngine;
using UIAspect = UnityEngine.UI.AspectRatioFitter;
using UIRawImage = UnityEngine.UI.RawImage;

namespace DiceModders.UiKit.Custom;

/// <summary>A picture on a window, useful for maps and other custom drawings. Pointer and wheel events arrive through Unity's event interfaces.</summary>
public sealed class Image : Element {
    private readonly UIRawImage _image;
    private readonly UIAspect _fitter;
    private readonly PointerReceiver _receiver;

    internal Image(RectTransform root, UIRawImage image, UIAspect fitter, PointerReceiver receiver) : base(root) {
        _image = image;
        _fitter = fitter;
        _receiver = receiver;
    }

    internal static Image? Create(Container parent, Texture? texture) {
        var root = UiFactory.NewRect("DM Image", parent.Root);

        try {
            var picture = UiFactory.NewRect("DM Picture", root);
            UiFactory.Stretch(picture);

            var image = picture.gameObject.AddComponent<UIRawImage>();
            image.texture = texture;
            image.raycastTarget = true;

            var fitter = picture.gameObject.AddComponent<UIAspect>();
            fitter.aspectMode = UIAspect.AspectMode.FitInParent;
            fitter.aspectRatio = AspectOf(texture);

            var receiver = picture.gameObject.AddComponent<PointerReceiver>();
            receiver.Bind(picture);

            var layout = UiFactory.Layout(root);
            layout.preferredHeight = 120f;
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 0f;

            return new Image(root, image, fitter, receiver);
        }
        catch (Exception e) {
            UiGuard.Report("image.create", e);
            UiFactory.Destroy(root.gameObject);
            return null;
        }
    }

    private static float AspectOf(Texture? texture) {
        if (texture == null || texture.height <= 0) return 1f;
        return (float)texture.width / texture.height;
    }

    public Texture? Texture => _image != null ? _image.texture : null;

    public Image SetTexture(Texture? texture) {
        if (!IsAlive || _image == null) return this;
        UiGuard.Run("image.texture", () => {
            _image.texture = texture;
            if (_fitter != null) _fitter.aspectRatio = AspectOf(texture);
        });
        return this;
    }

    public Image Filter(FilterMode mode) {
        if (!IsAlive) return this;
        UiGuard.Run("image.filter", () => {
            if (_image != null && _image.texture != null) _image.texture.filterMode = mode;
        });
        return this;
    }

    public Image Refresh() {
        if (!IsAlive || _image == null || _image.texture == null) return this;
        UiGuard.Run("image.refresh", () => {
            var texture = _image.texture.TryCast<Texture2D>();
            if (texture != null) texture.Apply(false);
        });
        return this;
    }

    public Image KeepAspect(bool on = true) {
        if (!IsAlive || _fitter == null || _image == null) return this;

        UiGuard.Run("image.keep-aspect", () => {
            _fitter.enabled = on;
            if (on) _fitter.aspectRatio = AspectOf(_image.texture);
            else UiFactory.Stretch(_image.rectTransform);
        });
        return this;
    }

    public Image OnPointer(Action<PointerEvent>? handler) {
        if (!IsAlive) return this;
        UiGuard.Run("image.pointer", () => PointerRegistry.SetPointer(_receiver, handler));
        return this;
    }

    public Image OnScroll(Action<float>? handler) {
        if (!IsAlive) return this;
        UiGuard.Run("image.scroll", () => PointerRegistry.SetScroll(_receiver, handler));
        return this;
    }
}

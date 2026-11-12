using System;
using UnityEngine;
using UnityEngine.UI;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Pictures for the game's dialog that its text icons cannot give sharply (a text icon is a small glyph scaled up to the icon box). Drawn once in code
/// at 256 px from exact shapes (signed distances, one pixel of anti-aliasing), so they stay sharp at any size.
/// </summary>
internal static class DialogIcons {
    private const int Size = 256;
    private const string OverlayName = "DM Dialog Icon";
    private static Sprite? _warning;

    // the game's palette: cream frame, amber body, the dark blue of its panels
    private static readonly Color Rim = new(0.95f, 0.87f, 0.68f, 1f);
    private static readonly Color Top = new(0.98f, 0.79f, 0.33f, 1f);
    private static readonly Color Bottom = new(0.88f, 0.58f, 0.18f, 1f);
    private static readonly Color Mark = new(0.12f, 0.14f, 0.2f, 1f);

    public static Sprite Warning => _warning != null ? _warning : _warning = Make(DrawWarning(), "uikit-warning");

    /// <summary>Shows <paramref name="sprite"/> over the dialog's icon label, or hides our picture when null.</summary>
    public static void Show(Component? iconLabel, Sprite? sprite) {
        if (iconLabel == null) return;
        var parent = iconLabel.transform;
        var found = parent.Find(OverlayName);
        Image? image = found != null ? found.GetComponent<Image>() : null;
        if (sprite == null) {
            if (image != null) image.gameObject.SetActive(false);
            return;
        }
        if (image == null) {
            var go = new GameObject(OverlayName);
            go.layer = iconLabel.gameObject.layer;
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.12f, 0.12f);
            rt.anchorMax = new Vector2(0.88f, 0.88f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            image = go.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        image.sprite = sprite;
        image.gameObject.SetActive(true);
    }

    private static Sprite Make(Texture2D texture, string name) {
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    private static Texture2D DrawWarning() {
        var px = new Color32[Size * Size];
        // equilateral triangle, point up; its centroid sits low so the shape's box is centred
        const float half = 112f, round = 18f, rim = 12f;
        var c = new Vector2(Size * 0.5f, Size * 0.395f);
        for (int y = 0; y < Size; y++) {
            for (int x = 0; x < Size; x++) {
                var p = new Vector2(x + 0.5f - c.x, y + 0.5f - c.y);
                float tri = Triangle(p, half - round) - round;            // rounded outer edge
                if (tri > 1f) { px[y * Size + x] = new Color32(0, 0, 0, 0); continue; }

                float t = Mathf.Clamp01((y - (c.y - half * 0.6f)) / (half * 1.5f));
                Color body = Color.Lerp(Bottom, Top, t);
                Color col = Mix(Rim, body, Cover(tri + rim));             // the cream rim, then the amber body

                // the "!": a rounded bar and a dot
                float bar = Capsule(p, new Vector2(0f, 52f), new Vector2(0f, 2f), 12f);
                float dot = (p - new Vector2(0f, -28f)).magnitude - 13f;
                col = Mix(col, Mark, Cover(Mathf.Min(bar, dot)));

                col.a *= Cover(tri);
                px[y * Size + x] = col;
            }
        }
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, name = "uikit-warning" };
        tex.SetPixels32(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32>(px));
        tex.Apply(true, true);
        tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return tex;
    }

    /// <summary>1 inside, 0 outside, a one-pixel ramp on the edge of a signed distance.</summary>
    private static float Cover(float distance) => Mathf.Clamp01(0.5f - distance);

    private static Color Mix(Color under, Color over, float k) => new(
        under.r + (over.r - under.r) * k, under.g + (over.g - under.g) * k, under.b + (over.b - under.b) * k, 1f);

    /// <summary>Signed distance to an equilateral triangle (point up) with half-width <paramref name="r"/>.</summary>
    private static float Triangle(Vector2 p, float r) {
        const float k = 1.7320508f;
        p.x = Mathf.Abs(p.x) - r;
        p.y = p.y + r / k;       // point up (texture rows go up)
        if (p.x + k * p.y > 0f) p = new Vector2(p.x - k * p.y, -k * p.x - p.y) / 2f;
        p.x -= Mathf.Clamp(p.x, -2f * r, 0f);
        return -p.magnitude * Mathf.Sign(p.y);
    }

    private static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r) {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }
}

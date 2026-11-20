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

    // Plain C# math only: Unity's Vector2 / Mathf / Color methods are calls into the game under IL2CPP, and this loop runs for 65,536 pixels.
    private static Texture2D DrawWarning() {
        var px = new Color32[Size * Size];
        // equilateral triangle, point up; its centroid sits low so the shape's box is centred
        const float half = 112f, round = 18f, rim = 12f;
        const float cx = Size * 0.5f, cy = Size * 0.395f;
        float[] rimC = { Rim.r, Rim.g, Rim.b }, topC = { Top.r, Top.g, Top.b }, botC = { Bottom.r, Bottom.g, Bottom.b }, markC = { Mark.r, Mark.g, Mark.b };
        var col = new float[3];
        for (int y = 0; y < Size; y++) {
            float t = Clamp01((y - (cy - half * 0.6f)) / (half * 1.5f));
            for (int x = 0; x < Size; x++) {
                float px0 = x + 0.5f - cx, py0 = y + 0.5f - cy;
                float tri = Triangle(px0, py0, half - round) - round;     // rounded outer edge
                if (tri > 1f) continue;                                    // stays transparent

                float kRim = Cover(tri + rim);                             // the cream rim, then the amber body
                float bar = Capsule(px0, py0, 0f, 52f, 0f, 2f, 12f);       // the "!": a rounded bar and a dot
                float dot = MathF.Sqrt(px0 * px0 + (py0 + 28f) * (py0 + 28f)) - 13f;
                float kMark = Cover(MathF.Min(bar, dot));
                for (int i = 0; i < 3; i++) {
                    float body = botC[i] + (topC[i] - botC[i]) * t;
                    float c = rimC[i] + (body - rimC[i]) * kRim;
                    col[i] = c + (markC[i] - c) * kMark;
                }
                Color32 o = default;                                       // fields, not the constructor: no call into the game
                o.r = B(col[0]); o.g = B(col[1]); o.b = B(col[2]); o.a = B(Cover(tri));
                px[y * Size + x] = o;
            }
        }
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, name = "uikit-warning" };
        tex.SetPixels32(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32>(px));
        tex.Apply(true, true);
        tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return tex;
    }

    /// <summary>1 inside, 0 outside, a one-pixel ramp on the edge of a signed distance.</summary>
    private static float Cover(float distance) => Clamp01(0.5f - distance);

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    private static byte B(float v) => (byte)(Clamp01(v) * 255f + 0.5f);

    /// <summary>Signed distance to an equilateral triangle (point up) with half-width <paramref name="r"/>.</summary>
    private static float Triangle(float x, float y, float r) {
        const float k = 1.7320508f;
        x = MathF.Abs(x) - r;
        y = y + r / k;           // point up (texture rows go up)
        if (x + k * y > 0f) { float nx = (x - k * y) / 2f, ny = (-k * x - y) / 2f; x = nx; y = ny; }
        x -= Math.Clamp(x, -2f * r, 0f);
        float len = MathF.Sqrt(x * x + y * y);
        return y >= 0f ? -len : len;      // Mathf.Sign(0) is 1
    }

    private static float Capsule(float px, float py, float ax, float ay, float bx, float by, float r) {
        float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
        float h = Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
        float dx = pax - bax * h, dy = pay - bay * h;
        return MathF.Sqrt(dx * dx + dy * dy) - r;
    }
}

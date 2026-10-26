using TMPro;
using UnityEngine;

namespace DiceModders.UiKit;

/// <summary>
/// TMP can throw from measurement while its font atlas is being rebuilt or its shared material has been released.
/// These helpers keep that failure inside the managed layer and let callers fall back to a safe layout size.
/// </summary>
internal static class TmpTextSafety {
    public static bool EnsureMeasurable(TMP_Text? text) {
        if (text == null) return false;

        try {
            if (text.font == null) return false;
            if (text.fontSharedMaterial == null) {
                var material = text.font.material;
                if (material == null) return false;
                text.fontSharedMaterial = material;
            }
            return true;
        }
        catch {
            return false;
        }
    }

    public static bool TryPreferredValues(TMP_Text? text, string? value, float width, float height,
                                          out Vector2 result) {
        result = default;
        if (!EnsureMeasurable(text)) return false;

        try {
            result = text!.GetPreferredValues(value ?? string.Empty, width, height);
            if (float.IsNaN(result.x) || float.IsInfinity(result.x) ||
                float.IsNaN(result.y) || float.IsInfinity(result.y)) {
                result = default;
                return false;
            }
            return true;
        }
        catch {
            return false;
        }
    }
}

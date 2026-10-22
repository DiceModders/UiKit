using System;

namespace DiceModders.UiKit;

/// <summary>Shared interpretation of <see cref="TemplatePolicy"/> for the widget factories.</summary>
internal static class SkinPolicy {
    public static bool AllowNative(UiKitRuntime runtime)
        => runtime != null && runtime.Options.Templates != TemplatePolicy.ManagedOnly;

    public static bool AllowManaged(UiKitRuntime runtime)
        => runtime == null || runtime.Options.Templates != TemplatePolicy.NativeOnly;

    /// <summary>Runs a native template/clone operation, turning unexpected game-side failures into a managed fallback.</summary>
    public static T TryNative<T>(string widget, Func<T> create) where T : class {
        try {
            return create();
        }
        catch (Exception e) {
            UiLog.OnceError("skin:error:" + widget, $"native {widget} could not be created: {e}");
            return null;
        }
    }

    /// <summary>
    /// Called after a native template search failed. In <see cref="TemplatePolicy.PreferNative"/> this logs once and lets the
    /// caller build the managed widget; in <see cref="TemplatePolicy.NativeOnly"/> it logs an error and the widget is skipped.
    /// </summary>
    public static bool MissingNative(UiKitRuntime runtime, string widget) {
        if (AllowManaged(runtime)) {
            UiLog.Once("skin:fallback:" + widget,
                $"native {widget} template not found; using the managed fallback");
            return true;
        }

        UiLog.OnceError("skin:missing:" + widget,
            $"native {widget} template not found and TemplatePolicy is NativeOnly; the widget was skipped");
        return false;
    }

    public static bool MissingManaged(UiKitRuntime runtime, string widget) {
        UiLog.OnceError("skin:managed-disabled:" + widget,
            $"{widget} needs a managed fallback, but TemplatePolicy is NativeOnly; the widget was skipped");
        return false;
    }
}

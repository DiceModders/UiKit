using System;
using Il2CppInterop.Runtime;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace DiceModders.UiKit;

/// <summary>
/// Converts managed callbacks to Unity delegate types outside the injected MonoBehaviour classes. Keeping these
/// signatures off the components avoids IL2CPP registration warnings for unsupported <see cref="Action"/> parameters.
/// </summary>
internal static class EventRelay {
    public static UnityAction ToUnity(Action action) {
        if (action == null) return null;

        Action guarded = () => {
            try { action(); }
            catch (Exception e) { UiLog.Error("UI callback failed: " + e); }
        };
        return DelegateSupport.ConvertDelegate<UnityAction>(guarded);
    }

    public static UnityAction<float> ToUnity(Action<float> action) {
        if (action == null) return null;

        Action<float> guarded = value => {
            try { action(value); }
            catch (Exception e) { UiLog.Error("UI callback failed: " + e); }
        };
        return DelegateSupport.ConvertDelegate<UnityAction<float>>(guarded);
    }

    public static UnityAction<BaseEventData> ToEvent(Action<BaseEventData> action) {
        if (action == null) return null;

        Action<BaseEventData> guarded = data => {
            try { action(data); }
            catch (Exception e) { UiLog.Error("UI event callback failed: " + e); }
        };
        return DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>(guarded);
    }
}

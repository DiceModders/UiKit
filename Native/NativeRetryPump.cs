using System;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>
/// One tiny persistent component for the work that cannot be purely event driven: the one-time UI readiness check,
/// scoreboard layout and retrying native registrations whose target or asset was not available yet. After readiness and
/// with nothing registered, the per-frame work is a few float comparisons; the delegate sweep runs every ten seconds.
/// </summary>
internal sealed class NativeRetryPump : MonoBehaviour {
    public NativeRetryPump(IntPtr ptr) : base(ptr) { }

    private UiKitRuntime? _runtime;
    private float _nextLayout;
    private float _nextRetry;
    private float _nextSweep;
    private float _nextReady;

    public void Init(UiKitRuntime runtime) => _runtime = runtime;

    public void Update() {
        var runtime = _runtime;
        if (runtime == null || runtime.IsDisposed) return;

        float now = Time.unscaledTime;
        if (!runtime.IsUiReady && now >= _nextReady) {
            _nextReady = now + 0.25f;
            UiGuard.Run("ui.ready", runtime.TryMarkUiReady, "runtime");
        }

        if (now >= _nextLayout) {
            _nextLayout = now + 0.25f;
            if (runtime.NativeUi.HasScoreboardColumns)
                UiGuard.Run("native.scoreboard-layout", runtime.NativeUi.TickScoreboard, "runtime");
        }

        if (now >= _nextRetry) {
            _nextRetry = now + 1f;
            if (runtime.NativeUi.HasPending)
                UiGuard.Run("native.retry", runtime.NativeUi.Retry, "runtime");
        }

        if (now >= _nextSweep) {
            _nextSweep = now + 10f;
            UiGuard.Run("native.delegates", Il2CppDelegates.Sweep, "runtime");
        }
    }

    public void OnDestroy() => _runtime = null;
}

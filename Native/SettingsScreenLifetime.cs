using System;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>Reports Settings-screen destruction without scene polling, so stale chrome cannot outlive its screen.</summary>
internal sealed class SettingsScreenLifetime : MonoBehaviour {
    public SettingsScreenLifetime(IntPtr ptr) : base(ptr) { }

    private NativeUiHost? _host;
    private DiceKingdoms.Meta.SettingSetsInterface? _settings;

    public void Bind(NativeUiHost host, DiceKingdoms.Meta.SettingSetsInterface settings) {
        _host = host;
        _settings = settings;
    }

    private void OnDestroy() {
        var host = _host;
        var settings = _settings;
        _host = null;
        _settings = null;
        try { host?.OnSettingsScreenDestroyed(settings); }
        catch (Exception e) { UiLog.Once("settings:lifetime", "settings screen lifetime cleanup failed: " + e.Message); }
    }
}

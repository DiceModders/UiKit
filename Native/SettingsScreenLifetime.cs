using System;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>Reports Settings-screen destruction without scene polling, so stale chrome cannot outlive its screen.</summary>
internal sealed class SettingsScreenLifetime : MonoBehaviour {
    public SettingsScreenLifetime(IntPtr ptr) : base(ptr) { }

    private void OnDestroy() => SettingsScreenLifetimeRegistry.Detach(this);
}

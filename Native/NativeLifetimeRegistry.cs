using System;
using System.Runtime.CompilerServices;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Keeps host/row and host/settings bindings outside the injected lifetime components. Passing managed-only types
/// through methods of an injected MonoBehaviour produces Il2CppInterop registration warnings.
/// </summary>
internal static class ScoreboardRowLifetimeRegistry {
    private sealed class Binding {
        public NativeUiHost? Host;
        public DiceKingdoms.Interface.ScoreboardRow? Row;
    }

    private static readonly ConditionalWeakTable<ScoreboardRowLifetime, Binding> Table = new();

    public static void Bind(ScoreboardRowLifetime? owner, NativeUiHost? host,
                            DiceKingdoms.Interface.ScoreboardRow? row) {
        if (owner == null) return;

        if (Table.TryGetValue(owner, out var binding)) {
            binding.Host = host;
            binding.Row = row;
            return;
        }

        Table.Add(owner, new Binding { Host = host, Row = row });
    }

    public static void Detach(ScoreboardRowLifetime? owner) {
        if (owner == null || !Table.TryGetValue(owner, out var binding)) return;
        Table.Remove(owner);

        try { binding.Host?.OnScoreboardRowDestroyed(binding.Row); }
        catch (Exception e) { UiLog.Once("scoreboard:lifetime", "scoreboard row lifetime cleanup failed: " + e.Message); }
    }
}

internal static class SettingsScreenLifetimeRegistry {
    private sealed class Binding {
        public NativeUiHost? Host;
        public DiceKingdoms.Meta.SettingSetsInterface? Settings;
    }

    private static readonly ConditionalWeakTable<SettingsScreenLifetime, Binding> Table = new();

    public static void Bind(SettingsScreenLifetime? owner, NativeUiHost? host,
                            DiceKingdoms.Meta.SettingSetsInterface? settings) {
        if (owner == null) return;

        if (Table.TryGetValue(owner, out var binding)) {
            binding.Host = host;
            binding.Settings = settings;
            return;
        }

        Table.Add(owner, new Binding { Host = host, Settings = settings });
    }

    public static void Detach(SettingsScreenLifetime? owner) {
        if (owner == null || !Table.TryGetValue(owner, out var binding)) return;
        Table.Remove(owner);

        try { binding.Host?.OnSettingsScreenDestroyed(binding.Settings); }
        catch (Exception e) { UiLog.Once("settings:lifetime", "settings screen lifetime cleanup failed: " + e.Message); }
    }
}

using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace DiceModders.UiKit;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BasePlugin {
    public override void Load() {
        try {
            LoadCore();
        }
        catch (Exception e) {
            Log.LogError("UiKit plugin failed to load; the library will stay unavailable: " + e);
        }
    }

    private void LoadCore() {
        var demo = Config.Bind("Demo", "Enabled", false,
            "Show the library's built-in test windows after a scene with the game UI loads. " +
            "Use the Rebuild button inside the demo after entering a match to re-scan native templates. " +
            "Can also be enabled with the command-line flag --uikit-demo.");

        var policyText = Config.Bind("Widgets", "Templates", nameof(TemplatePolicy.PreferNative),
            "Widget skin policy: PreferNative, ManagedOnly or NativeOnly.");

        var policy = TemplatePolicy.PreferNative;
        if (!Enum.TryParse(policyText.Value, true, out policy) || !Enum.IsDefined(typeof(TemplatePolicy), policy)) {
            Log.LogWarning($"unknown Widgets/Templates value '{policyText.Value}'; using {TemplatePolicy.PreferNative}");
            policy = TemplatePolicy.PreferNative;
        }

        var runtime = UiKit.Initialize(this, new UiOptions {
            Templates = policy,
            EnableDemo = demo.Value || HasCommandLineFlag("--uikit-demo"),
        });

        if (runtime == null) {
            Log.LogError("UiKit runtime initialization failed; mods calling UiKit.CreateScope will receive null");
            return;
        }

        // ConfigurationManager can change this entry without restarting the game.
        demo.SettingChanged += (_, _) => {
            try {
                if (demo.Value) UiKit.ShowDemo();
                else UiKit.HideDemo();
            }
            catch (Exception e) {
                Log.LogError("UiKit demo toggle failed: " + e);
            }
        };
    }

    public override bool Unload() {
        try {
            UiKit.Shutdown();
        }
        catch (Exception e) {
            Log.LogError("UiKit shutdown failed: " + e);
        }
        return true;
    }

    private static bool HasCommandLineFlag(string flag) {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++) {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}

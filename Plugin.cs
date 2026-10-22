using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace DiceModders.UiKit;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BasePlugin {
    public override void Load() {
        var demo = Config.Bind("Demo", "Enabled", false,
            "Show the library's built-in test windows after a scene with the game UI loads. " +
            "Use the Rebuild button inside the demo after entering a match to re-scan native templates. " +
            "Can also be enabled with the command-line flag --uikit-demo.");

        var policyText = Config.Bind("Widgets", "Templates", nameof(TemplatePolicy.PreferNative),
            "Widget skin policy: PreferNative, ManagedOnly or NativeOnly.");

        var policy = TemplatePolicy.PreferNative;
        if (!Enum.TryParse(policyText.Value, true, out policy)) {
            Log.LogWarning($"unknown Widgets/Templates value '{policyText.Value}'; using {TemplatePolicy.PreferNative}");
            policy = TemplatePolicy.PreferNative;
        }

        UiKit.Initialize(this, new UiOptions {
            Templates = policy,
            EnableDemo = demo.Value || HasCommandLineFlag("--uikit-demo"),
        });

        // ConfigurationManager can change this entry without restarting the game.
        demo.SettingChanged += (_, _) => {
            if (demo.Value) UiKit.ShowDemo();
            else UiKit.HideDemo();
        };
    }

    public override bool Unload() {
        UiKit.Shutdown();
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

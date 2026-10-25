using System;
using System.Collections.Generic;
using DiceModders.UiKit.Custom;
using DiceModders.UiKit.Native;
using Il2CppInterop.Runtime;
using UnityEngine;
using GameCivilization = DiceKingdoms.Interface.Civilization;
using GameIsland = DiceKingdoms.Game.Island;
using GameTechnologies = DiceKingdoms.Game.Technologies;
using GameTechTree = DiceKingdoms.Game.TechTree;

namespace DiceModders.UiKit;

/// <summary>
/// The library's own test screens. They are created only when the plugin config enables the demo or a mod calls
/// <see cref="UiKit.ShowDemo"/>, and they use the same public API as any other mod.
/// </summary>
internal sealed class DemoController {
    private const string DemoScopeId = "dicemodders.uikit.demo";

    private readonly UiKitRuntime _runtime;
    private readonly List<UiWindow> _windows = new();
    private UiScope? _scope;
    private UiWindow? _main;
    private ScoreboardColumnHandle? _scoreboard;
    private bool _requested;
    private bool _visible;
    private bool _built;

    public DemoController(UiKitRuntime runtime) => _runtime = runtime;

    public bool IsVisible => _visible && _built && _main != null && _main.IsAlive;

    /// <summary>Marks the demo for creation on the next scene load.</summary>
    public void Request() => _requested = true;

    public void Show() {
        _requested = true;
        if (!_built || _scope == null || _main == null || !_main.IsAlive) {
            DestroyScope();
            Build();
            return;
        }
        SetVisible(true);
    }

    public void Hide() {
        _requested = false;
        SetVisible(false);
    }

    public void Toggle() {
        if (IsVisible) Hide();
        else Show();
    }

    /// <summary>Disposes the demo scope and builds it again. Useful after entering a scene with the game's native UI loaded.</summary>
    public void Rebuild() {
        _requested = true;
        DestroyScope();
        Build();
    }

    public void OnSceneLoaded() {
        if (!_requested) return;

        try {
            if (GameUi.FindActiveBar() == null) {
                UiLog.Info("demo is enabled and will be created after a scene with the game UI loads");
                return;
            }

            // The canvas found during sceneLoaded can still be the previous scene's. Now that the game's bar exists,
            // re-read its render mode and sorting orders before building or keeping the demo on screen.
            _runtime.Layers.Refresh();
            if (_built && _main != null && _main.IsAlive) {
                RefreshDemoScoreboard();
                return;
            }
            DestroyScope();
            Build();
        }
        catch (Exception e) {
            UiLog.Error("demo scene hook failed: " + e);
        }
    }

    public void Dispose() {
        _requested = false;
        DestroyScope();
    }

    private void Build() {
        if (_built) return;

        try {
            _runtime.Layers.Refresh();
            var scope = _runtime.CreateScope(DemoScopeId);
            if (scope == null) {
                UiLog.Error("demo could not create its UiKit scope");
                return;
            }

            _scope = scope;
            _built = true;
            _visible = true;
            _windows.Clear();

            BuildMain();
            scope.Native.TopBar.AddIconButton("hammer", "UiKit demo", Toggle);
            BuildDemoSettings();
            BuildNativeDemo(scope);
            BuildProbe(Layer.Game, "This window is on Layer.Game. It should sit in the HUD band, below the game's popups.",
                       new Vector2(700f, 80f), OrnamentStyle.BorderWithTwoCorners);
            BuildProbe(Layer.BehindGame, "This window is on Layer.BehindGame. The game's own UI should draw over it.",
                       new Vector2(760f, 300f), OrnamentStyle.None);

            UiLog.Info("demo windows created; use Rebuild inside the demo after the game's UI has loaded to re-scan native skins");
        }
        catch (Exception e) {
            UiLog.Error("demo build failed: " + e);
            DestroyScope();
        }
    }

    private void BuildMain() {
        var scope = _scope;
        if (scope == null) return;
        UiText? status = null;

        _main = scope.Custom.Window(w => {
            w.Title("UiKit demo")
             .Size(620f, 640f)
             .Position(80f, 80f)
             .Draggable()
             .Fullscreenable()
             .Closable()
             .Ornament(OrnamentStyle.Border);
            w.OnClose(Close);

            w.Body.Scroll(body => {
                body.Paragraph(
                    "This is the library's built-in smoke test. It uses the same public API as a mod. " +
                    "The widgets below are native clones when their game templates are available, otherwise managed fallbacks. " +
                    "Use Rebuild after entering a match to re-scan the native templates.");
                body.Text($"Template policy: {_runtime.Options.Templates}");

                status = body.Text("Ready.");
                status?.Color(new Color(0.95f, 0.83f, 0.55f, 1f));

                body.Separator();
                body.Text("Buttons");
                body.Row(row => {
                    row.Button("Click me", () => { if (status != null) status.Value = "Text button clicked."; });
                    var disabled = row.Button("Disabled");
                    disabled?.Interactable(false);
                    row.IconButton("hammer", () => { if (status != null) status.Value = "Icon button clicked."; }, "UiKit demo icon button");
                });

                body.Text("Toggle, slider and dropdown");
                body.Toggle("Toggle me", true, value => { if (status != null) status.Value = "Toggle: " + value; });
                var slider = body.Slider(0f, 100f, 50f, value => { if (status != null) status.Value = "Slider: " + value.ToString("0"); });
                slider?.Format("0");
                body.Dropdown(new[] { "First option", "Second option", "Third option" }, 0,
                              index => { if (status != null) status.Value = "Dropdown index: " + index; });

                body.Text("Grid");
                body.Grid(3, 90f, 32f, grid => {
                    for (int i = 0; i < 6; i++) {
                        int number = i;
                        grid.Button(number.ToString(), () => { if (status != null) status.Value = "Grid button " + number; });
                    }
                }).Height(72f);

                body.Text("Window ornament");
                body.Row(row => {
                    row.Button("Border", () => w.Ornament(OrnamentStyle.Border));
                    row.Button("Corners", () => w.Ornament(OrnamentStyle.BorderWithFourCorners));
                    row.Button("Classic", () => w.Ornament(OrnamentStyle.Classic));
                    row.Button("None", () => w.Ornament(OrnamentStyle.None));
                });

                body.Separator();
                body.Button("Rebuild (re-scan native skins)", Rebuild);
                body.Button("Hide demo", Hide);

                body.Separator();
                body.Text("Scroll content");
                for (int i = 1; i <= 30; i++) body.Text("Scroll row " + i);
            }).FillHeight();
        });

        if (_main != null) _windows.Add(_main);
    }

    private void BuildDemoSettings() {
        var scope = _scope;
        if (scope == null) return;
        scope.Native.Settings.AddTab("uikit.demo", "UiKit", tab => {
            tab.AddHeader("UiKit native settings");
            tab.AddToggle("Demo toggle", true, value => UiLog.Info("demo settings toggle: " + value));
            tab.AddSlider("Demo slider", 0f, 100f, 50f, value => UiLog.Info("demo settings slider: " + value.ToString("0")), "0");
            tab.AddDropdown("Demo dropdown", new[] { "First", "Second", "Third" }, 0,
                            index => UiLog.Info("demo settings dropdown: " + index));
            tab.AddButton("Demo button", "Click", () => UiLog.Info("demo settings button clicked"));
        });
    }

    private void BuildNativeDemo(UiScope scope) {
        _scoreboard = scope.Native.Scoreboard.AddColumn("uikit.demo.score", "UiKit demo column");
        RefreshDemoScoreboard();

        var tree = FindDemoTree();
        if (tree == null) {
            UiLog.Info("demo: no game tech tree is available, the Rules civilization demo is skipped");
            return;
        }

        scope.Native.Rules.AddCivilization("uikit.demo.civ", "UiKit civilization", () => tree, 0);
    }

    private void RefreshDemoScoreboard() {
        var column = _scoreboard;
        if (column == null || column.IsDisposed) return;

        var values = new Dictionary<GameIsland, string>();
        try {
            foreach (var island in UnityEngine.Object.FindObjectsOfType<GameIsland>()) {
                if (island == null || Staging.IsOurs(island.transform)) continue;
                values[island] = (Math.Abs(island.GetInstanceID()) % 100).ToString();
            }
        }
        catch (Exception e) {
            UiLog.OnceError("demo:scoreboard", "demo scoreboard values failed: " + e);
        }
        column.SetValues(values);
    }

    private static GameTechTree? FindDemoTree() {
        try {
            foreach (var candidate in UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppType.Of<GameTechnologies>())) {
                var technologies = candidate?.TryCast<GameTechnologies>();
                if (technologies == null) continue;
                return technologies.TechTree(GameCivilization.EUROPE);
            }
        }
        catch (Exception e) {
            UiLog.OnceError("demo:tech-tree", "could not read a game tech tree: " + e);
        }
        return null;
    }

    private void BuildProbe(Layer layer, string text, Vector2 position, OrnamentStyle ornament) {
        var scope = _scope;
        if (scope == null) return;
        var probe = scope.Custom.Window(w => {
            w.Title("Layer." + layer)
             .Size(340f, 240f)
             .Position(position.x, position.y)
             .Layer(layer)
             .Draggable()
             .Closable()
             .Ornament(ornament);

            w.Body.Paragraph(text);
            w.Body.Button("Bring to front", w.BringToFront);
        });

        if (probe != null) _windows.Add(probe);
    }

    private void Close() {
        _requested = false;
        DestroyScope();
    }

    private void SetVisible(bool visible) {
        _visible = visible;
        foreach (var window in _windows) {
            if (window == null || !window.IsAlive) continue;
            if (visible) window.Show();
            else window.Hide();
        }
    }

    private void DestroyScope() {
        _built = false;
        _visible = false;
        _main = null;
        _scoreboard = null;
        _windows.Clear();

        var scope = _scope;
        _scope = null;
        if (scope == null) return;

        try { scope.Dispose(); }
        catch (Exception e) { UiLog.Error("demo scope dispose failed: " + e); }
    }
}

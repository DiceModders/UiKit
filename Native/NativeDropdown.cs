using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using GameDropdown = DiceKingdoms.Interface.Dropdown;

namespace DiceModders.UiKit.Native;

/// <summary>
/// A drop-down list cloned from the game's Language setting: native frame, arrow, popup template and animation. The game's
/// wrapper is removed before the clone wakes up, so its value list and selection callback are not used.
/// </summary>
internal sealed class NativeDropdown {
    public RectTransform Rt { get; private set; }
    public TMP_Dropdown Dropdown { get; private set; }
    public int Index { get; private set; }
    public float Height { get; private set; } = 32f;
    public bool IsAlive => Rt != null && Dropdown != null;

    private Action<int> _changed;
    private UnityAction<int> _unity;
    private bool _silent;
    private int _count;

    private NativeDropdown() { }

    public static NativeDropdown Create(TemplateCatalog templates, Transform parent,
                                        IEnumerable<string> options, int index) {
        var prefab = templates?.Dropdown();
        if (prefab == null) return null;

        var go = UnityEngine.Object.Instantiate(prefab.gameObject, Staging.Root);
        go.name = "DM Dropdown";
        NativeButton.StripForeign(go);

        var wrapper = go.GetComponent<GameDropdown>();
        if (wrapper != null) UnityEngine.Object.DestroyImmediate(wrapper);
        NativeButton.RepairBaseButtons(go);

        var dropdown = go.GetComponent<TMP_Dropdown>();
        if (dropdown == null) {
            UnityEngine.Object.Destroy(go);
            return null;
        }

        // Drop the donor's listeners while the clone is still inactive.
        dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();

        var rt = go.GetComponent<RectTransform>();
        float height = rt != null ? rt.rect.height : 0f;
        if (height <= 1f && rt != null) height = rt.sizeDelta.y;
        height = Mathf.Clamp(height > 1f ? height : 32f, 28f, 44f);

        go.transform.SetParent(parent, false);
        go.SetActive(true);

        var result = new NativeDropdown {
            Rt = rt,
            Dropdown = dropdown,
            Height = height,
        };
        result.SetOptions(options, index);
        return result;
    }

    public void SetChanged(Action<int> changed) {
        _changed = changed;
        if (Dropdown == null) return;

        if (_unity != null) Dropdown.onValueChanged.RemoveListener(_unity);

        if (changed == null) {
            _unity = null;
            return;
        }

        Action<int> handler = OnValueChanged;
        _unity = Il2CppDelegates.ToUnityActionInt(handler);
        Dropdown.onValueChanged.AddListener(_unity);
        Il2CppDelegates.Hold(Dropdown.gameObject, new object[] { handler, _unity });
    }

    public void SetOptions(IEnumerable<string> options, int index) {
        if (Dropdown == null) return;

        var values = new List<string>();
        if (options != null) {
            foreach (var option in options) values.Add(option ?? string.Empty);
        }
        if (values.Count == 0) values.Add(string.Empty);

        _count = values.Count;
        Index = Mathf.Clamp(index, 0, _count - 1);

        _silent = true;
        try {
            Dropdown.options.Clear();
            foreach (var value in values) Dropdown.options.Add(new TMP_Dropdown.OptionData(value));
            Dropdown.SetValueWithoutNotify(Index);
            Dropdown.RefreshShownValue();
        }
        finally {
            _silent = false;
        }
    }

    public void SetSilently(int index) {
        if (Dropdown == null) return;
        Index = Mathf.Clamp(index, 0, Mathf.Max(0, _count - 1));

        _silent = true;
        try {
            Dropdown.SetValueWithoutNotify(Index);
            Dropdown.RefreshShownValue();
        }
        finally {
            _silent = false;
        }
    }

    public void SetInteractable(bool on) {
        if (Dropdown != null) Dropdown.interactable = on;
    }

    private void OnValueChanged(int index) {
        if (_silent) return;
        Index = index;
        try { _changed?.Invoke(index); }
        catch (Exception e) { UiLog.Error("native dropdown: " + e); }
        NativeButton.Deselect();
    }
}

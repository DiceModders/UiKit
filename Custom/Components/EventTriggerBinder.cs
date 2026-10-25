using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace DiceModders.UiKit.Custom;

/// <summary>Builds EventTrigger entries outside the injected components, keeping delegate parameters off their methods.</summary>
internal static class EventTriggerBinder {
    public static void Add(EventTrigger trigger, List<UnityAction<BaseEventData>>? keep, EventTriggerType type,
                           UnityAction<BaseEventData>? action) {
        if (trigger == null || action == null) return;

        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
        keep?.Add(action);
    }
}

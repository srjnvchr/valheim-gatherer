using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimGatherer
{
    /// <summary>
    /// Keeps the loaded resource objects and pins them once the local player gets close,
    /// the same way a player would drop a pin after spotting something.
    /// </summary>
    internal static class Tracker
    {
        private sealed class Entry
        {
            public Component Obj;
            public ResourceRule Rule;
            public bool Handled;
        }

        private static readonly AccessTools.FieldRef<Minimap, List<Minimap.PinData>> PinsRef =
            AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");

        private static readonly List<Entry> Entries = new List<Entry>();

        public static void Register(Component obj, ResourceRule rule)
        {
            if (obj && rule != null)
                Entries.Add(new Entry { Obj = obj, Rule = rule });
        }

        public static void Scan()
        {
            var player = Player.m_localPlayer;
            var minimap = Minimap.instance;
            if (!player || !minimap)
                return;

            var playerPos = player.transform.position;
            var radius = Plugin.DiscoveryRadius.Value;
            var radiusSqr = radius * radius;

            for (var i = Entries.Count - 1; i >= 0; i--)
            {
                var entry = Entries[i];
                if (!entry.Obj)
                {
                    Entries.RemoveAt(i);
                    continue;
                }
                if (entry.Handled)
                    continue;

                var category = Plugin.Categories[entry.Rule.Category];
                if (!category.Enabled.Value)
                    continue;

                // Full 3D distance so dungeon interiors (high above the world) only count once you're inside.
                var pos = entry.Obj.transform.position;
                if ((pos - playerPos).sqrMagnitude > radiusSqr)
                    continue;

                entry.Handled = true;

                if (Plugin.IgnoreInsidePlayerBase.Value &&
                    EffectArea.IsPointInsideArea(pos, EffectArea.Type.PlayerBase, 0f))
                    continue;

                if (FindPin(minimap, entry.Rule.Label, pos, category.MergeRadius.Value) != null)
                    continue;

                minimap.AddPin(pos, category.Icon.Value, entry.Rule.Label, true, false, 0L, default);
            }
        }

        /// <summary>Called right before the game destroys a networked object (mined out, picked up, ...).</summary>
        public static void OnDestroyed(GameObject go)
        {
            var index = Entries.FindIndex(e => e.Obj && e.Obj.gameObject == go);
            if (index < 0)
                return;

            var entry = Entries[index];
            Entries.RemoveAt(index);

            var minimap = Minimap.instance;
            if (!minimap || !entry.Handled)
                return;

            var category = Plugin.Categories[entry.Rule.Category];
            if (!category.RemoveWhenDepleted.Value)
                return;

            var mergeRadius = category.MergeRadius.Value;
            var pin = FindPin(minimap, entry.Rule.Label, go.transform.position, mergeRadius);
            if (pin == null)
                return;

            // Keep the pin while other objects of the same kind it covers are still around.
            foreach (var other in Entries)
            {
                if (other.Obj && other.Rule.Label == entry.Rule.Label &&
                    Utils.DistanceXZ(other.Obj.transform.position, pin.m_pos) <= mergeRadius)
                    return;
            }

            minimap.RemovePin(pin);
        }

        private static Minimap.PinData FindPin(Minimap minimap, string label, Vector3 pos, float radius)
        {
            Minimap.PinData best = null;
            var bestDist = radius;
            foreach (var pin in PinsRef(minimap))
            {
                if (!string.Equals(pin.m_name, label, StringComparison.OrdinalIgnoreCase))
                    continue;
                var dist = Utils.DistanceXZ(pin.m_pos, pos);
                if (dist <= bestDist)
                {
                    best = pin;
                    bestDist = dist;
                }
            }
            return best;
        }
    }
}

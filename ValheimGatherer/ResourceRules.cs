using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimGatherer
{
    internal enum ResourceCategory
    {
        Ores,
        Pickables,
        Plants,
        InfoStones,
        Locations,
        Custom,
    }

    internal sealed class ResourceRule
    {
        public readonly string Prefix;
        public readonly string Label;
        public readonly ResourceCategory Category;

        public ResourceRule(string prefix, string label, ResourceCategory category)
        {
            Prefix = prefix.ToLowerInvariant();
            Label = label;
            Category = category;
        }
    }

    /// <summary>
    /// Maps prefab names to pin labels. Matching is case-insensitive by prefix; the longest prefix wins,
    /// so "pickable_mushroom_yellow" beats "pickable_mushroom".
    /// </summary>
    internal static class ResourceRules
    {
        private static readonly ResourceRule[] Defaults =
        {
            // Mineable deposits (finite; pins are removed when depleted if enabled).
            new ResourceRule("rock4_copper", "Copper", ResourceCategory.Ores),
            new ResourceRule("MineRock_Tin", "Tin", ResourceCategory.Ores),
            new ResourceRule("rock3_silver", "Silver", ResourceCategory.Ores),
            new ResourceRule("silvervein", "Silver", ResourceCategory.Ores),
            new ResourceRule("MineRock_Obsidian", "Obsidian", ResourceCategory.Ores),
            new ResourceRule("MineRock_Meteorite", "Flametal", ResourceCategory.Ores),
            new ResourceRule("mudpile", "Scrap Iron", ResourceCategory.Ores),
            new ResourceRule("Leviathan", "Chitin", ResourceCategory.Ores),
            new ResourceRule("giant_", "Giant Remains", ResourceCategory.Ores),
            new ResourceRule("giant_brain", "Soft Tissue", ResourceCategory.Ores),

            // One-off pickups (destroyed when picked).
            new ResourceRule("Pickable_Tin", "Tin", ResourceCategory.Pickables),
            new ResourceRule("Pickable_DragonEgg", "Dragon Egg", ResourceCategory.Pickables),
            new ResourceRule("Pickable_MountainCaveCrystal", "Crystal", ResourceCategory.Pickables),
            new ResourceRule("Pickable_BlackCoreStand", "Black Core", ResourceCategory.Pickables),

            // Respawning plants.
            new ResourceRule("RaspberryBush", "Raspberries", ResourceCategory.Plants),
            new ResourceRule("BlueberryBush", "Blueberries", ResourceCategory.Plants),
            new ResourceRule("CloudberryBush", "Cloudberries", ResourceCategory.Plants),
            new ResourceRule("Pickable_Mushroom", "Mushrooms", ResourceCategory.Plants),
            new ResourceRule("Pickable_Mushroom_yellow", "Yellow Mushrooms", ResourceCategory.Plants),
            new ResourceRule("Pickable_Mushroom_blue", "Blue Mushrooms", ResourceCategory.Plants),
            new ResourceRule("Pickable_Mushroom_JotunPuffs", "Jotun Puffs", ResourceCategory.Plants),
            new ResourceRule("Pickable_Mushroom_Magecap", "Magecap", ResourceCategory.Plants),
            new ResourceRule("Pickable_SmokePuff", "Smoke Puffs", ResourceCategory.Plants),
            new ResourceRule("Pickable_Thistle", "Thistle", ResourceCategory.Plants),
            new ResourceRule("Pickable_Dandelion", "Dandelion", ResourceCategory.Plants),
            new ResourceRule("Pickable_SeedCarrot", "Carrot Seeds", ResourceCategory.Plants),
            new ResourceRule("Pickable_SeedTurnip", "Turnip Seeds", ResourceCategory.Plants),
            new ResourceRule("Pickable_SeedOnion", "Onion Seeds", ResourceCategory.Plants),
            new ResourceRule("Pickable_Barley_Wild", "Barley", ResourceCategory.Plants),
            new ResourceRule("Pickable_Flax_Wild", "Flax", ResourceCategory.Plants),
            new ResourceRule("Pickable_Fiddlehead", "Fiddlehead", ResourceCategory.Plants),
            new ResourceRule("Pickable_RoyalJelly", "Royal Jelly", ResourceCategory.Plants),
            new ResourceRule("Beehive", "Beehive", ResourceCategory.Plants),

            // Locations, matched against the Location prefab name.
            new ResourceRule("TarPit", "Tar Pit", ResourceCategory.Locations),
        };

        internal static readonly ResourceRule RuneStone = new ResourceRule("RuneStone", "Runestone", ResourceCategory.InfoStones);
        internal static readonly ResourceRule Vegvisir = new ResourceRule("Vegvisir", "Vegvisir", ResourceCategory.InfoStones);

        private static List<ResourceRule> _rules = new List<ResourceRule>();
        private static HashSet<string> _disabled = new HashSet<string>();

        // Prefab hash -> rule (null when the prefab is not tracked). Avoids string work in ZNetView.Awake.
        private static readonly Dictionary<int, ResourceRule> PrefabCache = new Dictionary<int, ResourceRule>();

        public static void Rebuild(string customRules, string disabledPrefixes)
        {
            var rules = new List<ResourceRule>(Defaults);
            foreach (var entry in Split(customRules))
            {
                var eq = entry.IndexOf('=');
                if (eq <= 0 || eq == entry.Length - 1)
                {
                    Plugin.Log.LogWarning($"Ignoring malformed custom rule '{entry}' (expected PrefabPrefix=Label)");
                    continue;
                }
                rules.Add(new ResourceRule(entry.Substring(0, eq).Trim(), entry.Substring(eq + 1).Trim(), ResourceCategory.Custom));
            }
            rules.Sort((a, b) => b.Prefix.Length.CompareTo(a.Prefix.Length));

            _rules = rules;
            _disabled = new HashSet<string>(Split(disabledPrefixes), StringComparer.OrdinalIgnoreCase);
            PrefabCache.Clear();
        }

        /// <summary>Rule for a networked object, cached per prefab.</summary>
        public static ResourceRule MatchPrefab(int prefabHash, GameObject go)
        {
            if (PrefabCache.TryGetValue(prefabHash, out var cached))
                return cached;

            var rule = MatchName(Utils.GetPrefabName(go), r => r.Category != ResourceCategory.Locations);
            if (rule == null && go.GetComponent<RuneStone>())
                rule = RuneStone;
            if (rule == null && go.GetComponent<Vegvisir>())
                rule = Vegvisir;

            PrefabCache[prefabHash] = rule;
            return rule;
        }

        /// <summary>Rule for a Location prefab. Custom rules apply to both objects and locations.</summary>
        public static ResourceRule MatchLocation(string prefabName) =>
            MatchName(prefabName, r => r.Category == ResourceCategory.Locations || r.Category == ResourceCategory.Custom);

        private static ResourceRule MatchName(string name, Func<ResourceRule, bool> filter)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            var lower = name.ToLowerInvariant();
            // Fragments spawned while a rock breaks apart are temporary.
            if (lower.EndsWith("_frac"))
                return null;

            foreach (var rule in _rules)
            {
                if (!filter(rule))
                    continue;
                if (!lower.StartsWith(rule.Prefix, StringComparison.Ordinal))
                    continue;
                return IsDisabled(lower) ? null : rule;
            }
            return null;
        }

        private static bool IsDisabled(string lowerName)
        {
            foreach (var prefix in _disabled)
                if (lowerName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static IEnumerable<string> Split(string value)
        {
            if (string.IsNullOrEmpty(value))
                yield break;
            foreach (var part in value.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0)
                    yield return trimmed;
            }
        }
    }
}

using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ValheimGatherer
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "srjn.valheimgatherer";
        public const string Name = "Valheim Gatherer";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> DiscoveryRadius;
        internal static ConfigEntry<float> ScanInterval;
        internal static ConfigEntry<bool> IgnoreInsidePlayerBase;
        internal static ConfigEntry<string> CustomRules;
        internal static ConfigEntry<string> DisabledPrefabs;

        internal static readonly Dictionary<ResourceCategory, CategoryConfig> Categories =
            new Dictionary<ResourceCategory, CategoryConfig>();

        private Harmony _harmony;
        private float _scanTimer;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true, "Automatically pin discovered resources on the map.");
            DiscoveryRadius = Config.Bind("General", "DiscoveryRadius", 50f,
                new ConfigDescription("A resource is pinned once you come within this many meters of it.",
                    new AcceptableValueRange<float>(5f, 300f)));
            ScanInterval = Config.Bind("General", "ScanInterval", 1f,
                new ConfigDescription("Seconds between proximity checks.", new AcceptableValueRange<float>(0.1f, 10f)));
            IgnoreInsidePlayerBase = Config.Bind("General", "IgnoreInsidePlayerBase", true,
                "Skip resources inside a player base (workbench range), e.g. crops and mushrooms you planted.");
            CustomRules = Config.Bind("Rules", "CustomRules", "",
                "Extra prefab rules, comma separated, as PrefabPrefix=Label. Matches objects and locations. " +
                "Example: Pickable_VoltureEgg=Volture Egg, Crypt=Crypt");
            DisabledPrefabs = Config.Bind("Rules", "DisabledPrefabs", "",
                "Comma separated prefab prefixes that should never be pinned, e.g. Pickable_Dandelion, Beehive");

            BindCategory(ResourceCategory.Ores, Minimap.PinType.Icon2, 15f, removeWhenDepleted: true);
            BindCategory(ResourceCategory.Pickables, Minimap.PinType.Icon3, 10f, removeWhenDepleted: true);
            BindCategory(ResourceCategory.Plants, Minimap.PinType.Icon3, 30f, removeWhenDepleted: false);
            BindCategory(ResourceCategory.InfoStones, Minimap.PinType.Icon4, 10f, removeWhenDepleted: false);
            BindCategory(ResourceCategory.Locations, Minimap.PinType.Icon0, 30f, removeWhenDepleted: false);
            BindCategory(ResourceCategory.Custom, Minimap.PinType.Icon3, 15f, removeWhenDepleted: false);

            RebuildRules();
            CustomRules.SettingChanged += (_, __) => RebuildRules();
            DisabledPrefabs.SettingChanged += (_, __) => RebuildRules();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{Name} {Version} loaded");
        }

        private void BindCategory(ResourceCategory category, Minimap.PinType icon, float mergeRadius, bool removeWhenDepleted)
        {
            var section = "Category." + category;
            Categories[category] = new CategoryConfig
            {
                Enabled = Config.Bind(section, "Enabled", true, $"Pin {category}."),
                Icon = Config.Bind(section, "Icon", icon, "Map pin icon."),
                MergeRadius = Config.Bind(section, "MergeRadius", mergeRadius,
                    "Skip a new pin if a pin with the same label already exists within this many meters (groups clusters)."),
                RemoveWhenDepleted = Config.Bind(section, "RemoveWhenDepleted", removeWhenDepleted,
                    "Remove the pin when you destroy/collect the last tracked object it covers."),
            };
        }

        private static void RebuildRules() => ResourceRules.Rebuild(CustomRules.Value, DisabledPrefabs.Value);

        private void Update()
        {
            if (!Enabled.Value)
                return;
            _scanTimer -= UnityEngine.Time.deltaTime;
            if (_scanTimer > 0f)
                return;
            _scanTimer = ScanInterval.Value;
            Tracker.Scan();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }

    internal sealed class CategoryConfig
    {
        public ConfigEntry<bool> Enabled;
        public ConfigEntry<Minimap.PinType> Icon;
        public ConfigEntry<float> MergeRadius;
        public ConfigEntry<bool> RemoveWhenDepleted;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;

namespace ValheimGatherer
{
    /// <summary>
    /// The "gatherer" command. Works in chat as /gatherer and in the F5 console as gatherer.
    /// Every change goes through the BepInEx config, so it is saved to the .cfg file immediately.
    /// </summary>
    internal static class Commands
    {
        private const string Name = "gatherer";
        private static ConfigFile _config;

        private static readonly string[] Usage =
        {
            "gatherer on | off | toggle - enable or disable auto pinning",
            "gatherer radius <meters> - set the discovery radius",
            "gatherer <category> on | off - category: " + string.Join(", ", Enum.GetNames(typeof(ResourceCategory))).ToLowerInvariant(),
            "gatherer settings - list every setting and its value",
            "gatherer set <Section.Key> <value> - change any setting, e.g. set Category.Ores.MergeRadius 20",
            "gatherer reload - re-read the .cfg file after editing it by hand",
        };

        public static void Register(ConfigFile config)
        {
            _config = config;
            // The constructor adds the command to Terminal's static command table used by both chat and console.
            new Terminal.ConsoleCommand(Name, "Valheim Gatherer settings. Type 'gatherer help' for usage.", Run,
                optionsFetcher: () => new List<string> { "on", "off", "toggle", "radius", "settings", "set", "reload", "help" }
                    .Concat(Enum.GetNames(typeof(ResourceCategory)).Select(n => n.ToLowerInvariant()))
                    .ToList());
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            var parts = args.Args.Where(a => a.Length > 0).ToArray();
            var context = args.Context;
            var sub = parts.Length > 1 ? parts[1].ToLowerInvariant() : "status";

            switch (sub)
            {
                case "on":
                    Plugin.Enabled.Value = true;
                    Reply(context, "auto pinning enabled");
                    return;
                case "off":
                    Plugin.Enabled.Value = false;
                    Reply(context, "auto pinning disabled");
                    return;
                case "toggle":
                    Plugin.Enabled.Value = !Plugin.Enabled.Value;
                    Reply(context, "auto pinning " + (Plugin.Enabled.Value ? "enabled" : "disabled"));
                    return;
                case "status":
                    Reply(context, Status());
                    return;
                case "help":
                    Reply(context, string.Join("\n", Usage));
                    return;
                case "settings":
                    Reply(context, ListSettings());
                    return;
                case "reload":
                    _config.Reload();
                    Reply(context, "config reloaded from " + _config.ConfigFilePath);
                    return;
                case "radius":
                    if (parts.Length < 3)
                    {
                        Reply(context, "usage: gatherer radius <meters>");
                        return;
                    }
                    Reply(context, Set(Plugin.DiscoveryRadius, parts[2]));
                    return;
                case "set":
                    if (parts.Length < 4)
                    {
                        Reply(context, "usage: gatherer set <Section.Key> <value>");
                        return;
                    }
                    var entry = FindEntry(parts[2]);
                    if (entry == null)
                    {
                        Reply(context, $"unknown setting '{parts[2]}'. Use 'gatherer settings' to list them.");
                        return;
                    }
                    // Everything after the key, so values with spaces (CustomRules labels) survive.
                    Reply(context, Set(entry, string.Join(" ", parts.Skip(3))));
                    return;
            }

            if (Enum.TryParse(sub, true, out ResourceCategory category) && Enum.IsDefined(typeof(ResourceCategory), category))
            {
                var enabled = Plugin.Categories[category].Enabled;
                if (parts.Length < 3 || !TryParseOnOff(parts[2], out var value))
                {
                    Reply(context, $"{category} is {(enabled.Value ? "on" : "off")}. Usage: gatherer {sub} on | off");
                    return;
                }
                enabled.Value = value;
                Reply(context, $"{category} {(value ? "enabled" : "disabled")}");
                return;
            }

            Reply(context, $"unknown option '{parts[1]}'.\n" + string.Join("\n", Usage));
        }

        private static string Set(ConfigEntryBase entry, string value)
        {
            var name = $"{entry.Definition.Section}.{entry.Definition.Key}";
            try
            {
                // Same parser BepInEx uses for the .cfg file.
                TomlTypeConverter.ConvertToValue(value, entry.SettingType);
            }
            catch (Exception)
            {
                return $"{name} unchanged ({entry.GetSerializedValue()}). '{value}' is not a valid {entry.SettingType.Name}.";
            }
            // Clamps to the acceptable range, fires SettingChanged and saves the .cfg file.
            entry.SetSerializedValue(value);
            return $"{name} = {entry.GetSerializedValue()}";
        }

        private static ConfigEntryBase FindEntry(string fullName)
        {
            // Sections contain dots ("Category.Ores"), so the key is whatever follows the last dot.
            var dot = fullName.LastIndexOf('.');
            if (dot <= 0 || dot == fullName.Length - 1)
                return null;
            var section = fullName.Substring(0, dot);
            var key = fullName.Substring(dot + 1);
            return _config.Keys
                .Where(d => string.Equals(d.Section, section, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase))
                .Select(d => _config[d])
                .FirstOrDefault();
        }

        private static string Status()
        {
            var sb = new StringBuilder();
            sb.Append($"auto pinning {(Plugin.Enabled.Value ? "ON" : "OFF")}, radius {Plugin.DiscoveryRadius.Value} m. Categories:");
            foreach (var pair in Plugin.Categories)
                sb.Append($" {pair.Key.ToString().ToLowerInvariant()}={(pair.Value.Enabled.Value ? "on" : "off")}");
            sb.Append("\nType 'gatherer help' for commands.");
            return sb.ToString();
        }

        private static string ListSettings()
        {
            var sb = new StringBuilder();
            foreach (var definition in _config.Keys.OrderBy(d => d.Section).ThenBy(d => d.Key))
                sb.Append($"\n{definition.Section}.{definition.Key} = {_config[definition].GetSerializedValue()}");
            return sb.ToString().TrimStart('\n');
        }

        private static bool TryParseOnOff(string text, out bool value)
        {
            switch (text.ToLowerInvariant())
            {
                case "on": case "true": case "1": case "enable":
                    value = true;
                    return true;
                case "off": case "false": case "0": case "disable":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        private static void Reply(Terminal context, string text) =>
            context.AddString("<color=#9ad26c>[Gatherer]</color> " + text);
    }
}

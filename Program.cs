using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Plugins;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace RenameHeavyArmoryWeapons
{
    public class Program
    {
        // Dictionary to store our name replacements
        private static readonly Dictionary<string, string> NameReplacements = new()
        {
            // Original capitalization
            { "Spear", "Half pike" },
            { "Spears", "Half pikes" },
            { "spear", "half pike" },
            { "spears", "half pikes" },
            
            { "Halberd", "Poleaxe" },
            { "Halberds", "Poleaxes" },
            { "halberd", "poleaxe" },
            { "halberds", "poleaxes" },

            { "Quarterstaff", "Shortstaff" },
            { "Quarterstaffs", "Shortstaffs" },
            { "quarterstaff", "shortstaff" },
            { "quarterstaffs", "shortstaffs" },

            // Blades weapons - reordered to process longer names first
            { "Dai-Katana", "Greatsword" },
            { "Wakizashi", "Shortsword" },
            { "Katana", "Sword" },
            { "Tanto", "Dagger" }
        };

        // Add this new dictionary after the existing NameReplacements dictionary
        private static readonly List<KeyValuePair<string, string>> PerkDescriptionReplacements = new()
        {
            // Ordered list of replacements, processed in this exact order
            new("greatswords and two-handed pole weapons", "greatswords and two-handed pole weapons"),
            new("greatswords", "greatswords and two-handed pole weapons"),
            new("Greatswords", "Greatswords and two-handed pole weapons"),
            new("greatsword and two-handed pole weapon", "greatsword and two-handed pole weapon"),
            new("greatsword", "greatsword and two-handed pole weapon"),
            new("Greatsword", "Greatsword and two-handed pole weapon"),
            
            // One-handed weapons
            new("swords and daggers", "one-handed swords, one-handed spears, claws and daggers"),
            new("Swords and daggers", "One-handed swords, one-handed spears, claws and daggers"),
            new("swords", "one-handed swords and spears"),
            new("Swords", "One-handed swords and spears"),
            new("sword", "one-handed sword and spear"),
            new("Sword", "One-handed sword and spear"),
            new("mace", "one-handed blunt weapon"),
            new("Mace", "One-handed blunt weapon"),
            new("War Axe", "One-handed axe"),
            new("War axe", "One-handed axe"),
            new("war axe", "one-handed axe"),
            
            // Daggers
            new("daggers", "claws and daggers"),
            new("Daggers", "Claws and daggers"),
            new("dagger", "claw or dagger"),
            new("Dagger", "Claw or dagger")
        };

        private static bool ContainsWord(string source, string word)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(word))
                return false;

            // For hyphenated words, treat them as a single unit
            if (word.Contains("-"))
            {
                return source.Contains(word, StringComparison.OrdinalIgnoreCase);
            }

            // For non-hyphenated words, split and check each word
            var words = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Any(w => w.Equals(word, StringComparison.OrdinalIgnoreCase));
        }

        private static string ReplaceOnce(string? text, List<KeyValuePair<string, string>> replacements)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Keep track of which parts of the text have been replaced
            var replacedRegions = new HashSet<(int start, int end)>();

            string result = text;
            foreach (var replacement in replacements)
            {
                int index = 0;
                while ((index = result.IndexOf(replacement.Key, index, StringComparison.Ordinal)) != -1)
                {
                    // Check if this region overlaps with any already replaced region
                    bool canReplace = true;
                    foreach (var region in replacedRegions)
                    {
                        if (index < region.end && (index + replacement.Key.Length) > region.start)
                        {
                            canReplace = false;
                            break;
                        }
                    }

                    if (canReplace)
                    {
                        result = result.Remove(index, replacement.Key.Length)
                                     .Insert(index, replacement.Value);
                        replacedRegions.Add((index, index + replacement.Value.Length));
                    }
                    
                    index += replacement.Value.Length;
                }
            }

            return result;
        }

        private static Lazy<Settings> _settings = null!;

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "RenameHeavyArmoryWeapons.esp")
                .SetAutogeneratedSettings("settings", "settings.json", out _settings)
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            Console.WriteLine("RenameHeavyArmoryWeapons v1.0.0");
            Console.WriteLine($"Beginning patching process...");
            int weaponsChanged = 0;
            int perksChanged = 0;

            try
            {
                // Log plugin status from settings
                Console.WriteLine("\nChecking plugins from settings:");
                var targetPlugins = _settings.Value.TargetPlugins
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .ToList();

                foreach (var plugin in targetPlugins)
                {
                    if (state.LoadOrder.ContainsKey(ModKey.FromFileName(plugin)))
                    {
                        Console.WriteLine($"Found plugin: {plugin}");
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Plugin not found in load order: {plugin}");
                    }
                }
                Console.WriteLine(); // Empty line for readability

                // Get all weapons from target plugins AND vanilla weapons they override
                var vanillaOverrides = state.LoadOrder.PriorityOrder
                    .Where(mod => targetPlugins.Contains(mod.ModKey.FileName.String))
                    .SelectMany(mod => mod.Mod?.Weapons.Records ?? Enumerable.Empty<IWeaponGetter>())
                    .Where(w => w.FormKey.ModKey.FileName.String == "Skyrim.esm")
                    .Select(w => w.FormKey)
                    .ToHashSet();

                var targetWeapons = state.LoadOrder.PriorityOrder
                    .WinningOverrides<IWeaponGetter>()
                    .Where(weapon => 
                        targetPlugins.Contains(weapon.FormKey.ModKey.FileName.String, StringComparer.OrdinalIgnoreCase) || // Original weapons from our plugins
                        (weapon.FormKey.ModKey.FileName.String == "Skyrim.esm" && // Vanilla weapons
                         vanillaOverrides.Contains(weapon.FormKey))); // That are overridden by our target plugins

                Console.WriteLine("\nStarting weapon renaming...");
                foreach (var weaponGetter in targetWeapons)
                {
                    if (string.IsNullOrEmpty(weaponGetter.Name?.String))
                        continue;

                    string originalName = weaponGetter.Name!.String;
                    string newName = originalName;

                    // Check if the weapon name contains any of our target strings as whole words
                    foreach (var replacement in NameReplacements)
                    {
                        // Skip replacements based on settings
                        if (!_settings.Value.RenameSpears && (replacement.Key.Contains("Spear") || replacement.Key.Contains("spear")))
                            continue;
                        if (!_settings.Value.RenameHalberds && (replacement.Key.Contains("Halberd") || replacement.Key.Contains("halberd")))
                            continue;
                        if (!_settings.Value.RenameQuarterstaffs && (replacement.Key.Contains("Quarterstaff") || replacement.Key.Contains("quarterstaff")))
                            continue;
                        if (!_settings.Value.RenameBladesSwords && (
                            replacement.Key.Contains("Katana") || 
                            replacement.Key.Contains("Wakizashi") || 
                            replacement.Key.Contains("Tanto") ||
                            replacement.Key.Contains("Dai-Katana")))
                            continue;

                        if (ContainsWord(originalName, replacement.Key))
                        {
                            // Handle hyphenated replacements differently
                            if (replacement.Key.Contains("-"))
                            {
                                newName = originalName.Replace(replacement.Key, replacement.Value, StringComparison.OrdinalIgnoreCase);
                            }
                            else
                            {
                                // Split only on spaces for non-hyphenated words
                                var words = originalName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                for (int i = 0; i < words.Length; i++)
                                {
                                    if (words[i].Equals(replacement.Key, StringComparison.OrdinalIgnoreCase))
                                    {
                                        words[i] = replacement.Value;
                                    }
                                }
                                newName = string.Join(" ", words);
                            }
                            break;
                        }
                    }

                    // Only create an override if we actually changed the name
                    if (newName != originalName)
                    {
                        var weapon = state.PatchMod.Weapons.GetOrAddAsOverride(weaponGetter);
                        weapon.Name = newName;
                        weaponsChanged++;
                        Console.WriteLine($"Renamed: {originalName} -> {newName}");
                    }
                }
                Console.WriteLine($"Finished renaming weapons. Total weapons renamed: {weaponsChanged}");

                // Update perk descriptions if enabled
                if (_settings.Value.UpdatePerkDescriptions)
                {
                    // Process perk descriptions
                    var vanillaModNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "Skyrim.esm",
                        "Update.esm",
                        "Dawnguard.esm",
                        "HearthFires.esm",
                        "Dragonborn.esm"
                    };

                    // First, collect all vanilla perk FormKeys
                    var vanillaPerkFormKeys = state.LoadOrder.PriorityOrder
                        .Where(mod => vanillaModNames.Contains(mod.ModKey.FileName.String))
                        .SelectMany(mod => mod.Mod?.Perks ?? Enumerable.Empty<IPerkGetter>())
                        .Select(perk => perk.FormKey)
                        .ToHashSet();

                    // Then process each vanilla perk's winning override
                    foreach (var formKey in vanillaPerkFormKeys)
                    {
                        try
                        {
                            // Try to resolve the winning override for this perk
                            if (!state.LinkCache.TryResolve<IPerkGetter>(formKey, out var perkGetter) || 
                                perkGetter?.Description?.String == null)
                            {
                                continue;
                            }

                            string newDesc = ReplaceOnce(perkGetter.Description.String, PerkDescriptionReplacements);

                            // Only create an override if we actually changed the description
                            if (newDesc != perkGetter.Description.String)
                            {
                                var perk = state.PatchMod.Perks.GetOrAddAsOverride(perkGetter);
                                perk.Description = newDesc;
                                perksChanged++;
                                Console.WriteLine($"Updated perk description: {perkGetter.EditorID ?? perkGetter.FormKey.ToString()}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing perk {formKey}: {ex.Message}");
                            continue;
                        }
                    }
                    Console.WriteLine($"Finished updating perk descriptions. Total perks updated: {perksChanged}");
                }

                Console.WriteLine($"Finished processing. Total weapons renamed: {weaponsChanged}, Perk descriptions updated: {perksChanged}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error while processing records: {ex.Message}", ex);
            }
        }
    }
} 
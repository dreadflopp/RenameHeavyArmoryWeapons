using System;

namespace RenameHeavyArmoryWeapons
{
    public class Settings
    {
        // Checkbox for weapon renaming feature
        public bool RenameSpears { get; set; } = true;
        public bool RenameHalberds { get; set; } = true;
        public bool RenameQuarterstaffs { get; set; } = true;
        public bool RenameBladesSwords { get; set; } = true;

        // Checkbox for perk description updates
        public bool UpdatePerkDescriptions { get; set; } = true;
        
        // Plugin settings - using full plugin name
        public string TargetPlugins { get; set; } = "PrvtI_HeavyArmory.esp";
    }
} 
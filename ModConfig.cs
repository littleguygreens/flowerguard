namespace HoneyHelper
{
    /// <summary>
    /// These are the settings a player can change. SMAPI writes them to a
    /// "config.json" file next to the mod the first time it runs, using the
    /// default values you see below. After that, the player can edit the file
    /// (or use the in-game menu) and their choices are read back into this object.
    /// </summary>
    public sealed class ModConfig
    {
        /// <summary>
        /// A master on/off switch. If a player wants normal flower-picking
        /// back without removing the mod, they set this to false.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// If true, the scythe can still deliberately cut a protected flower.
        /// Only hand-picking is blocked. If false, even the scythe is stopped
        /// from cutting flowers that are near a bee house.
        /// </summary>
        public bool AllowScytheHarvest { get; set; } = true;

        /// <summary>
        /// Extra tiles of protection added ON TOP of the bee house's own flower
        /// range. 0 means "exactly the bee house's range" (the vanilla 5 tiles);
        /// 1, 2, or 3 widens the protected zone by that many tiles.
        /// This maps directly to the in-game slider: "Beehive range", "+1", "+2", "+3".
        /// </summary>
        public int ExtraRange { get; set; } = 0;
    }
}

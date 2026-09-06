namespace HoneyHelper
{
    /// <summary>
    /// These are the settings a player can change. SMAPI writes them to a
    /// "config.json" file next to the mod the first time it runs, using the
    /// default values you see below. After that, the player can edit the file
    /// and their choices are read back into this object.
    /// </summary>
    public sealed class ModConfig
    {
        /// <summary>
        /// A master on/off switch. If a player wants normal flower-picking
        /// back without deleting the mod, they set this to false.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// How far (in tiles) from a bee house a flower is protected.
        /// The game's own bee houses gather from flowers within 5 tiles,
        /// measured as a diamond shape, so 5 matches vanilla behaviour.
        /// A larger number protects a wider area; a smaller one protects less.
        /// </summary>
        public int ProtectionRange { get; set; } = 5;
    }
}

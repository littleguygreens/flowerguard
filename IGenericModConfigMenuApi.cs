using System;
using StardewModdingAPI;

namespace HoneyHelper
{
    /// <summary>
    /// A small "contract" describing the parts of the Generic Mod Config Menu
    /// (GMCM) mod that we use. GMCM is a separate mod that other mods can plug
    /// into to get a settings screen drawn in the game's own style.
    ///
    /// We don't reference GMCM's code directly (that would make it a hard
    /// requirement). Instead we describe the methods we need here, and at run
    /// time SMAPI hands us a matching object IF the player has GMCM installed.
    /// If they don't, we simply skip the menu and the mod still works via
    /// config.json. This interface must match GMCM's real method names exactly.
    /// </summary>
    public interface IGenericModConfigMenuApi
    {
        /// <summary>Start registering this mod's options page.</summary>
        /// <param name="mod">This mod's manifest (identifies us to GMCM).</param>
        /// <param name="reset">Called when the player clicks "reset to default".</param>
        /// <param name="save">Called when the player saves; we write config.json here.</param>
        /// <param name="titleScreenOnly">If true, options only show on the title screen.</param>
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        /// <summary>Add a checkbox (on/off) option.</summary>
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);

        /// <summary>
        /// Add a number option. Giving min/max/interval turns it into a slider.
        /// <paramref name="formatValue"/> lets us show friendly labels (e.g.
        /// "Beehive range", "+1") instead of the raw number.
        /// </summary>
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);
    }
}

using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Tools;
// The game has its own type called "Object", which clashes with C#'s built-in
// "object". This alias lets us write "SObject" to mean the game's version.
using SObject = StardewValley.Object;

namespace HoneyHelper
{
    /// <summary>
    /// The mod's entry point. SMAPI looks for exactly one class that inherits
    /// from "Mod" and calls its Entry(...) method once, when the game loads.
    /// </summary>
    public sealed class ModEntry : Mod
    {
        /// <summary>
        /// The range a real bee house searches for flowers, in tiles (a diamond).
        /// The player's slider adds 0-3 extra tiles on top of this.
        /// </summary>
        private const int VanillaBeeHouseFlowerRange = 5;

        // Our Harmony patch (further down) is a "static" method, which means it
        // belongs to the class itself rather than to a particular instance.
        // Because of that, it can't easily reach the normal instance fields, so
        // we stash the config and logger in static fields it can read.
        internal static ModConfig Config = null!;
        internal static IMonitor? Log;

        /// <summary>Runs once when the mod is first loaded.</summary>
        public override void Entry(IModHelper helper)
        {
            // Load config.json (SMAPI creates it from ModConfig's defaults the
            // very first time, then reads the player's edits on later runs).
            Config = helper.ReadConfig<ModConfig>();
            Log = this.Monitor;

            // Harmony is the library that lets a mod change how existing game
            // methods behave. We give it a unique id so its changes are tracked
            // separately from other mods'.
            var harmony = new Harmony(this.ModManifest.UniqueID);

            // Here we attach OUR code to the game's Crop.harvest method.
            // A "prefix" runs BEFORE the game's own code, and it's allowed to
            // cancel that code entirely. This is the method the game runs when
            // a crop (including a flower) is picked.
            //
            // Important: collecting honey from a bee house is a DIFFERENT method
            // (the bee house's own "check for action"), so nothing we do here
            // touches honey-grabbing. Mash the button at the hive all you like.
            harmony.Patch(
                original: AccessTools.Method(typeof(Crop), nameof(Crop.harvest)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(HarvestPrefix))
            );

            // GameLaunched fires once, after every mod has loaded. That's the
            // right moment to look for GMCM and register our options page,
            // because GMCM must already be loaded for us to find it.
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;

            this.Monitor.Log("Honey Helper loaded. Flowers near bee houses are now protected.", LogLevel.Info);
        }

        /// <summary>
        /// Builds the in-game settings page, but only if the player has GMCM
        /// installed. Without GMCM the mod still works; there's just no menu.
        /// </summary>
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Ask SMAPI for GMCM's public interface by its unique id. If GMCM
            // isn't installed, this returns null and we quietly do nothing.
            var menu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (menu == null)
                return;

            // Register our page. "reset" restores defaults; "save" writes the
            // player's choices back to config.json.
            menu.Register(
                mod: this.ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(Config)
            );

            // A simple on/off checkbox for the whole mod.
            menu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => Config.Enabled,
                setValue: value => Config.Enabled = value,
                name: () => "Protect flowers",
                tooltip: () => "Turn the whole mod on or off."
            );

            // The scythe toggle you asked for.
            menu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => Config.AllowScytheHarvest,
                setValue: value => Config.AllowScytheHarvest = value,
                name: () => "Allow scythe cutting",
                tooltip: () => "If on, the scythe can still cut protected flowers on purpose. Only accidental hand-picking stays blocked."
            );

            // The range slider. Under the hood it's a number from 0 to 3 (the
            // extra tiles), but formatValue shows friendly labels instead.
            menu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => Config.ExtraRange,
                setValue: value => Config.ExtraRange = value,
                name: () => "Protected range",
                tooltip: () => "How far protection reaches: the bee house's own flower range, plus up to 3 extra tiles.",
                min: 0,
                max: 3,
                interval: 1,
                formatValue: value => value == 0
                    ? $"Beehive range ({VanillaBeeHouseFlowerRange} tiles)"
                    : $"+{value} ({VanillaBeeHouseFlowerRange + value} tiles)"
            );
        }

        /// <summary>
        /// Runs just before the game harvests a crop.
        ///
        /// The return value is a Harmony convention:
        ///   - return true  => "carry on and let the game harvest as normal"
        ///   - return false => "skip the game's harvest entirely"
        ///
        /// The special parameter names are also Harmony conventions:
        ///   - xTile / yTile match the real method's parameters (the crop's tile)
        ///   - __instance is the Crop object being harvested
        ///   - __result lets us set what the real method would have returned
        /// </summary>
        private static bool HarvestPrefix(int xTile, int yTile, Crop __instance, ref bool __result)
        {
            try
            {
                // Player turned the mod off -> behave exactly like vanilla.
                if (!Config.Enabled)
                    return true;

                Crop crop = __instance;

                // 1) We only care about flowers. Anything else harvests normally.
                if (!IsFlower(crop))
                    return true;

                // 2) If the player is deliberately using the scythe and has
                //    allowed scythe cutting, let it through. This is the
                //    "on purpose" escape hatch; plain hand-picking is not.
                if (Config.AllowScytheHarvest && IsPlayerUsingScythe())
                    return true;

                // 3) The tile this flower is planted on.
                var flowerTile = new Vector2(xTile, yTile);

                // 4) Which map are we on? Bee houses only matter on the same map
                //    as the flower.
                GameLocation? location = Game1.currentLocation;
                if (location == null)
                    return true;

                // 5) If a bee house is close enough that this flower could be
                //    feeding it, protect the flower: cancel the harvest.
                int range = VanillaBeeHouseFlowerRange + Config.ExtraRange;
                if (IsNearBeeHouse(location, flowerTile, range))
                {
                    __result = false; // tell the game "nothing was harvested"
                    return false;     // and skip the real harvest code
                }

                // Flower isn't near any hive -> pick it as normal.
                return true;
            }
            catch (Exception ex)
            {
                // A mod should never crash the game. If anything unexpected
                // happens, log it and fall back to normal harvesting.
                Log?.Log($"Honey Helper hit a problem and allowed the harvest: {ex}", LogLevel.Error);
                return true;
            }
        }

        /// <summary>True if this crop produces a flower.</summary>
        private static bool IsFlower(Crop? crop)
        {
            // A dead or missing crop isn't a flower we need to protect.
            if (crop == null || crop.dead.Value)
                return false;

            // "indexOfHarvest" is the id of the item the crop gives when picked.
            // We look that item up and check its category. The game files every
            // flower under one category number (flowersCategory, which is -80),
            // so this reliably tells flowers apart from vegetables, fruit, etc.
            string harvestId = crop.indexOfHarvest.Value;
            if (string.IsNullOrEmpty(harvestId))
                return false;

            var data = ItemRegistry.GetDataOrErrorItem(harvestId);
            return data.Category == SObject.flowersCategory;
        }

        /// <summary>
        /// True if the current player is actively holding a scythe. We use this
        /// to tell a deliberate scythe cut apart from an accidental hand-pick.
        /// NOTE: this checks the equipped tool, so it needs a quick in-game test
        /// to confirm it behaves for every harvest path (see README caveats).
        /// </summary>
        private static bool IsPlayerUsingScythe()
        {
            return Game1.player?.CurrentTool is MeleeWeapon weapon && weapon.isScythe();
        }

        /// <summary>True if any placed bee house is within <paramref name="range"/> tiles of the flower.</summary>
        private static bool IsNearBeeHouse(GameLocation location, Vector2 flowerTile, int range)
        {
            // location.Objects holds every placed object on the map, keyed by
            // its tile. We look through them for bee houses.
            foreach (var pair in location.Objects.Pairs)
            {
                SObject obj = pair.Value;
                if (obj == null)
                    continue;

                // A Bee House is a "big craftable" item with id 10, which the
                // game writes as the qualified id "(BC)10". Checking the id
                // (rather than the display name) keeps this working no matter
                // what language the game is running in.
                if (obj.QualifiedItemId != "(BC)10")
                    continue;

                Vector2 hiveTile = pair.Key;

                // Bee houses search for flowers in a DIAMOND, which is measured
                // by "Manhattan distance": the horizontal gap plus the vertical
                // gap. A range of 5 therefore reaches 5 tiles straight out and
                // tapers at the corners -- the same footprint the game uses.
                float distance = Math.Abs(flowerTile.X - hiveTile.X)
                               + Math.Abs(flowerTile.Y - hiveTile.Y);

                if (distance <= range)
                    return true;
            }

            return false;
        }
    }
}

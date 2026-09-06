using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
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

            this.Monitor.Log("Honey Helper loaded. Flowers near bee houses are now protected.", LogLevel.Info);
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

                // 2) The tile this flower is planted on.
                var flowerTile = new Vector2(xTile, yTile);

                // 3) Which map are we on? Bee houses only matter on the same map
                //    as the flower.
                GameLocation? location = Game1.currentLocation;
                if (location == null)
                    return true;

                // 4) If a bee house is close enough that this flower could be
                //    feeding it, protect the flower: cancel the harvest.
                if (IsNearBeeHouse(location, flowerTile, Config.ProtectionRange))
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

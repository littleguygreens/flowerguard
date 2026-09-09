using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Tools;
// The game has its own type called "Object", which clashes with C#'s built-in
// "object". This alias lets us write "SObject" to mean the game's version.
using SObject = StardewValley.Object;

namespace FlowerGuard
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

        /// <summary>
        /// The game's own translucent green "valid placement" tile, from the
        /// cursors spritesheet -- the same graphic it draws under a bee house
        /// while you're deciding where to place one.
        /// </summary>
        private static readonly Rectangle PlacementTileSource = new Rectangle(194, 388, 16, 16);

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

            // Draws the protected-range overlay every frame, when turned on.
            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;

            this.Monitor.Log("Flower Guard loaded. Flowers near bee houses are now protected.", LogLevel.Info);
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

            // The range control. Under the hood it's a number from 0 to 3 (the
            // extra tiles), but it's presented as a dropdown of the 4 allowed
            // choices, with FormatRange showing a friendly label for each.
            menu.AddTextOption(
                mod: this.ModManifest,
                getValue: () => Config.ExtraRange.ToString(),
                setValue: value => Config.ExtraRange = int.Parse(value),
                name: () => "Protected range",
                tooltip: () => "How far protection reaches: the bee house's own flower range, plus up to 3 extra tiles.",
                allowedValues: new[] { "0", "1", "2", "3" },
                formatAllowedValue: value => FormatRange(int.Parse(value))
            );

            // The overlay toggle -- mostly for screenshots and planning where
            // to plant, since it's otherwise invisible which tiles are protected.
            menu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => Config.ShowRangeOverlay,
                setValue: value => Config.ShowRangeOverlay = value,
                name: () => "Show protected range",
                tooltip: () => "Draws a green tile overlay over every tile currently protected by a bee house."
            );
        }

        /// <summary>Draws the protected-range overlay, when the player has it turned on.</summary>
        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Config.Enabled || !Config.ShowRangeOverlay)
                return;

            GameLocation? location = Game1.currentLocation;
            if (location == null)
                return;

            int range = VanillaBeeHouseFlowerRange + Config.ExtraRange;
            foreach (Vector2 tile in GetProtectedTiles(location, range))
            {
                e.SpriteBatch.Draw(
                    Game1.mouseCursors,
                    Game1.GlobalToLocal(Game1.viewport, tile * 64f),
                    PlacementTileSource,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    4f,
                    SpriteEffects.None,
                    1f
                );
            }
        }

        /// <summary>Every tile protected by a bee house on <paramref name="location"/>, deduplicated.</summary>
        private static HashSet<Vector2> GetProtectedTiles(GameLocation location, int range)
        {
            var tiles = new HashSet<Vector2>();

            foreach (var pair in location.Objects.Pairs)
            {
                SObject obj = pair.Value;
                if (obj == null || obj.QualifiedItemId != "(BC)10")
                    continue;

                Vector2 hiveTile = pair.Key;

                // Same diamond shape IsNearBeeHouse checks against, walked tile
                // by tile instead of tested one flower at a time.
                for (int dx = -range; dx <= range; dx++)
                {
                    int remaining = range - Math.Abs(dx);
                    for (int dy = -remaining; dy <= remaining; dy++)
                        tiles.Add(new Vector2(hiveTile.X + dx, hiveTile.Y + dy));
                }
            }

            return tiles;
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
                Log?.Log($"Flower Guard hit a problem and allowed the harvest: {ex}", LogLevel.Error);
                return true;
            }
        }

        /// <summary>The friendly label GMCM shows for a given extra-range choice.</summary>
        private static string FormatRange(int extraRange) => extraRange == 0
            ? $"Bee House range ({VanillaBeeHouseFlowerRange} tiles)"
            : $"+{extraRange} ({VanillaBeeHouseFlowerRange + extraRange} tiles)";

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
        /// True if the current player is mid-swing with a scythe right now.
        ///
        /// Just checking the equipped tool isn't enough: the action/interact
        /// button (hand-picking) fires Crop.harvest the same way regardless of
        /// what's in your toolbar, so if the scythe merely happened to be
        /// selected, that check let hand-picks through too. "UsingTool" is only
        /// true while a tool's swing/use animation is actually playing, which
        /// is what tells a deliberate scythe cut apart from an accidental
        /// hand-pick made while the scythe just happens to be equipped.
        /// </summary>
        private static bool IsPlayerUsingScythe()
        {
            Farmer? player = Game1.player;
            return player != null
                && player.UsingTool
                && player.CurrentTool is MeleeWeapon weapon
                && weapon.isScythe();
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

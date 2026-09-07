# Flower Guard

A small [Stardew Valley](https://www.stardewvalley.net/) mod for
[SMAPI](https://smapi.io/) that stops you accidentally picking the flowers that
flavour your honey while you're collecting from bee houses.

## The problem it solves

Bee houses take on the flavour of the nearest flower planted within range. When
you walk up to a cluster of hives to grab honey and mash the action button, it's
easy to pick one of those flowers by mistake — which changes (or ruins) the
flavour of every hive that relied on it.

## What it does

- Flowers planted within a bee house's range become **un-pickable by hand**.
- **Collecting honey is completely unaffected** — that's a different game action,
  so you can still button-mash at the hives freely.
- Optionally, the **scythe can still cut protected flowers on purpose** (on by default).
- The protection range is **tuneable** — the bee house's own range plus up to 3 tiles.

## Configuring it in-game

If you also install [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)
(GMCM), Flower Guard adds a settings page drawn in the game's own style, with a
toggle for scythe cutting and a dropdown for the range. GMCM is **optional** — the
mod works fine without it, and you can always edit `config.json` by hand instead.

## Status

**Draft — not yet compiled or tested in-game.** This is a first pass written to
be reviewed. A few game-API details (see the code comments) still need to be
verified against a real build before it's known-good. In particular, the scythe
exception decides "is this a deliberate scythe cut?" by checking the equipped
tool; that heuristic needs an in-game test to confirm it covers every harvest
path (e.g. hand-picking while a scythe happens to be equipped).

## Settings (`config.json`)

The file is created automatically the first time the mod runs.

| Setting              | Default | Meaning                                                                        |
| -------------------- | ------- | ------------------------------------------------------------------------------ |
| `Enabled`            | `true`  | Master on/off switch. Set to `false` for normal picking.                       |
| `AllowScytheHarvest` | `true`  | If `true`, the scythe can still cut protected flowers; only hand-picking stops. |
| `ExtraRange`         | `0`     | Extra tiles of protection added on top of the bee house's range (0–3).          |

The bee house's own flower range is a diamond of 5 tiles, so `ExtraRange: 0`
protects exactly that, and `3` reaches 8 tiles out.

## Building

Requires the [.NET 6 SDK](https://dotnet.microsoft.com/download) and an installed
copy of Stardew Valley with SMAPI.

```
dotnet build
```

The [ModBuildConfig](https://github.com/Pathoschild/SMAPI/blob/develop/docs/technical/mod-package.md)
package locates your game automatically and copies the built mod into your
`Stardew Valley/Mods/FlowerGuard` folder. Launch the game through SMAPI to run it.

## How it works (for the curious)

The mod uses [Harmony](https://harmony.pardeike.net/) to run a small check just
before the game harvests any crop. If the crop is a flower **and** a bee house is
within range, the harvest is cancelled. Everything else — including honey
collection, which is a separate game action — is left untouched.

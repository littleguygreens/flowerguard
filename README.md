# Honey Helper

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
- The protection range is **tuneable** in `config.json` (see below).

## Status

**Draft — not yet compiled or tested in-game.** This is a first pass written to
be reviewed. A few game-API details (see the code comments) still need to be
verified against a real build before it's known-good.

## Settings (`config.json`)

The file is created automatically the first time the mod runs.

| Setting           | Default | Meaning                                                        |
| ----------------- | ------- | -------------------------------------------------------------- |
| `Enabled`         | `true`  | Master on/off switch. Set to `false` for normal picking.       |
| `ProtectionRange` | `5`     | How many tiles out from a bee house flowers are protected.     |

`5` matches the game's own bee-house flower range (a diamond, 5 tiles each way).

## Building

Requires the [.NET 6 SDK](https://dotnet.microsoft.com/download) and an installed
copy of Stardew Valley with SMAPI.

```
dotnet build
```

The [ModBuildConfig](https://github.com/Pathoschild/SMAPI/blob/develop/docs/technical/mod-package.md)
package locates your game automatically and copies the built mod into your
`Stardew Valley/Mods/HoneyHelper` folder. Launch the game through SMAPI to run it.

## How it works (for the curious)

The mod uses [Harmony](https://harmony.pardeike.net/) to run a small check just
before the game harvests any crop. If the crop is a flower **and** a bee house is
within range, the harvest is cancelled. Everything else — including honey
collection, which is a separate game action — is left untouched.

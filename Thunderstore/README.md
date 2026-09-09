![WorldGenAccelerator](https://i.imgur.com/KdBw6Ny.png)

# World Gen Accelerator

Optimizes location placement during world generation by caching zones by biome.

## Valheim 1.0

Version 1.0.1 supports the updated zone and location APIs and preserves the new
alternate-biome placement restrictions. Fresh-world generation was tested on
Valheim 1.0.7. Performance relative to vanilla has not been measured on this version.

## Features

- Selects candidate zones from a biome cache during location placement.
- Can be removed after world generation finishes.
- Includes optional Expand World Size integration. This integration has not been
  tested with Valheim 1.0.
- Sends an anonymous analytics ping at startup. Set `Enabled = false` in the
  `[Analytics]` configuration section to disable it.

## Configuration

Configuration file: `BepInEx/config/warpalicious.worldGenAccelerator.cfg`.

- `[General] EnableOptimization`: set to `false` to use vanilla location generation.
- `[General] EnableTimingLogs`: controls location-generation timing logs.

Optimized worlds have different location layouts from vanilla worlds with the
same seed. Some location types may not reach their requested placement count.

## Support and source

- [Discord](https://discord.gg/3aaru2VyHJ)
- [Source code](https://github.com/jneb802/worldGenAccelerator)
- [Support development on Ko-fi](https://ko-fi.com/warpalicious)

# Valheim 1.0 compatibility validation

Tested on 2026-09-09 on `valnet-client-02`, using Valheim `l-1.0.7`
and the `valheim-1.0-testing` mmcli profile.

## Before the fix

Installed the existing World Gen Accelerator 1.0.0 DLL and started a fresh
local world, `WGA10Before0909`, with the development character `ScreenshotTest`.
Location generation failed with:

```text
MissingMethodException: Method not found: UnityEngine.Vector3 .ZoneSystem.GetZonePos(Vector2i)
worldGenAccelerator.ZoneSystemPatch.GenerateLocations_Prefix
```

Building the original source against the local 1.0 assemblies also failed:
zone APIs require `Vector2s`, and `HaveLocationInRange` requires an asset ID.

## After the fix

- `dotnet build -c Release`: zero warnings and zero errors.
- Installed the Release DLL and restarted the same client and profile.
- Started a second fresh world, `WGA10After0909`, with the same character.
- Biome cache: 76,713 zones, built in 858 ms.
- Location generation completed in 62,037 ms.
- The character spawned alive at the starting temple.
- Location queries found the starting temple, three Eikthyr locations,
  four Elder locations, five Bonemass locations, and three Deep North boss locations.
- No exceptions or error-level messages appeared in the corrected run.

The placement code was also compared with the local decompiled 1.0 source:
both nearby-location checks use the prefab asset ID, biome-area selection uses
the zone-center lookup, and placement respects alternate-biome requirements
and blocked location names.

## Limits

The corrected run still reports incomplete placement counts for some location
types, including Hildir camps, North villages, and several alternate-biome
locations. Audio and initial player-ID warnings also remain. The test establishes
that world generation completes; it does not establish full placement counts,
equivalence to vanilla layouts, or a performance improvement over vanilla.

Expand World Size, dedicated servers, and multiplayer were not tested.
The before and after tests use different fresh worlds, not a controlled seed
comparison. No release version was changed or published.

Logs are retained on the client as `/home/paperspace/wga-before.log` and
`/home/paperspace/wga-after.log`, with local copies under
`/Users/benjmarston/Downloads/wga-valheim-1.0-validation/`.

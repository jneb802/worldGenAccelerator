using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace worldGenAccelerator
{
    [HarmonyPatch]
    public static class ZoneSystemPatch
    {
        private static Stopwatch s_totalGenerationTimer = new Stopwatch();

        /// <summary>
        /// Patch A: Build the biome zone cache before location generation starts.
        /// Also starts the total generation timer.
        /// </summary>
        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.GenerateLocations))]
        [HarmonyPrefix]
        private static void GenerateLocations_Prefix()
        {
            if (worldGenAcceleratorPlugin.TimingLogsEnabled)
            {
                s_totalGenerationTimer.Restart();
            }

            if (!worldGenAcceleratorPlugin.OptimizationEnabled)
                return;

            BiomeZoneCache.Instance.Reset();
            BiomeZoneCache.Instance.Build();
        }

        /// <summary>
        /// Patch C: Log total generation time when LocationsGenerated is set to true.
        /// </summary>
        [HarmonyPatch(typeof(ZoneSystem), "set_LocationsGenerated")]
        [HarmonyPostfix]
        private static void LocationsGenerated_Postfix(bool value)
        {
            if (value && worldGenAcceleratorPlugin.TimingLogsEnabled && s_totalGenerationTimer.IsRunning)
            {
                s_totalGenerationTimer.Stop();
                worldGenAcceleratorPlugin.TemplateLogger.LogInfo(
                    $"Total location generation completed in {s_totalGenerationTimer.ElapsedMilliseconds}ms");
            }
        }

        /// <summary>
        /// Patch B: Replace the per-location coroutine with an optimized version
        /// that iterates only biome-matching zones from the cache.
        /// </summary>
        [HarmonyPatch(typeof(ZoneSystem), "GenerateLocationsTimeSliced",
            new Type[] { typeof(ZoneSystem.ZoneLocation), typeof(Stopwatch), typeof(ZPackage) })]
        [HarmonyPrefix]
        private static bool GenerateLocationsTimeSliced_Prefix(
            ZoneSystem __instance,
            ZoneSystem.ZoneLocation location,
            Stopwatch timeSliceStopwatch,
            ZPackage iterationsPkg,
            ref IEnumerator __result)
        {
            if (!worldGenAcceleratorPlugin.OptimizationEnabled)
                return true; // run original

            __result = OptimizedGenerateLocationsTimeSliced(__instance, location, timeSliceStopwatch, iterationsPkg);
            return false; // skip original
        }

        private static IEnumerator OptimizedGenerateLocationsTimeSliced(
            ZoneSystem zoneSystem,
            ZoneSystem.ZoneLocation location,
            Stopwatch timeSliceStopwatch,
            ZPackage iterationsPkg)
        {
            Stopwatch? locationTimer = null;
            if (worldGenAcceleratorPlugin.TimingLogsEnabled)
            {
                locationTimer = Stopwatch.StartNew();
            }

            int seed = WorldGenerator.instance.GetSeed() + location.m_prefab.Name.GetStableHashCode();
            UnityEngine.Random.State state = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);

            float maxRadius = Mathf.Max(location.m_exteriorRadius, location.m_interiorRadius);
            int iterations = 0;
            int placed = zoneSystem.CountNrOfLocation(location);

            if (!location.m_unique || placed <= 0)
            {
                zoneSystem.s_tempVeg.Clear();

                // Get candidate zones from the biome cache
                List<Vector2i> candidates = BiomeZoneCache.Instance.GetCandidateZones(
                    location.m_biome, location.m_biomeArea);

                // Remove zones that already have a location instance or are already generated
                candidates.RemoveAll(z =>
                    zoneSystem.m_locationInstances.ContainsKey(z) ||
                    zoneSystem.m_generatedZones.Contains(z));

                int candidateCount = candidates.Count;

                // Zone ordering
                if (location.m_centerFirst)
                {
                    // Sort by distance from origin (ascending) for center-first placement
                    candidates.Sort((a, b) =>
                    {
                        float distA = ZoneSystem.GetZonePos(a).magnitude;
                        float distB = ZoneSystem.GetZonePos(b).magnitude;
                        return distA.CompareTo(distB);
                    });
                }
                else
                {
                    // Fisher-Yates shuffle using the seeded RNG
                    for (int i = candidates.Count - 1; i > 0; i--)
                    {
                        int j = UnityEngine.Random.Range(0, i + 1);
                        Vector2i tmp = candidates[i];
                        candidates[i] = candidates[j];
                        candidates[j] = tmp;
                    }
                }

                // Iterate candidate zones
                for (int i = 0; i < candidates.Count && placed < location.m_quantity; i++)
                {
                    Vector2i zoneID = candidates[i];

                    // Time-slice yield (outer loop)
                    UnityEngine.Random.State insideState;
                    if (timeSliceStopwatch.Elapsed.TotalSeconds >= (double)zoneSystem.m_timeSlicedGenerationTimeBudget)
                    {
                        insideState = UnityEngine.Random.state;
                        UnityEngine.Random.state = state;
                        yield return null;
                        timeSliceStopwatch.Restart();
                        state = UnityEngine.Random.state;
                        UnityEngine.Random.state = insideState;
                    }

                    // Re-check in case zone was claimed during a yield
                    if (zoneSystem.m_locationInstances.ContainsKey(zoneID))
                        continue;

                    // Inner loop: try 20 random points within this zone
                    for (int j = 0; j < 20; j++)
                    {
                        // Time-slice yield (inner loop)
                        if (timeSliceStopwatch.Elapsed.TotalSeconds >= (double)zoneSystem.m_timeSlicedGenerationTimeBudget)
                        {
                            insideState = UnityEngine.Random.state;
                            UnityEngine.Random.state = state;
                            yield return null;
                            timeSliceStopwatch.Restart();
                            state = UnityEngine.Random.state;
                            UnityEngine.Random.state = insideState;
                        }

                        iterations++;
                        Vector3 randomPointInZone = ZoneSystem.GetRandomPointInZone(zoneID, maxRadius);
                        float magnitude = randomPointInZone.magnitude;

                        // FILTER: minDistance from world center
                        if ((double)location.m_minDistance != 0.0 && (double)magnitude < (double)location.m_minDistance)
                        {
                            continue;
                        }

                        // FILTER: maxDistance from world center
                        if ((double)location.m_maxDistance != 0.0 && (double)magnitude > (double)location.m_maxDistance)
                        {
                            continue;
                        }

                        // FILTER: Biome check at the specific point
                        if ((location.m_biome & WorldGenerator.instance.GetBiome(randomPointInZone)) == Heightmap.Biome.None)
                        {
                            continue;
                        }

                        // Get height and vegetation mask
                        Color mask1;
                        randomPointInZone.y = WorldGenerator.instance.GetHeight(randomPointInZone.x, randomPointInZone.z, out mask1);
                        float altitude = randomPointInZone.y - 30f;

                        // FILTER: Altitude
                        if ((double)altitude < (double)location.m_minAltitude || (double)altitude > (double)location.m_maxAltitude)
                        {
                            continue;
                        }

                        // FILTER: Forest factor
                        if (location.m_inForest)
                        {
                            float forestFactor = WorldGenerator.GetForestFactor(randomPointInZone);
                            if ((double)forestFactor < (double)location.m_forestTresholdMin || (double)forestFactor > (double)location.m_forestTresholdMax)
                            {
                                continue;
                            }
                        }

                        // FILTER: minDistanceFromCenter / maxDistanceFromCenter (XZ only)
                        if ((double)location.m_minDistanceFromCenter > 0.0 || (double)location.m_maxDistanceFromCenter > 0.0)
                        {
                            float lengthXZ = Utils.LengthXZ(randomPointInZone);
                            if ((double)location.m_minDistanceFromCenter > 0.0 && (double)lengthXZ < (double)location.m_minDistanceFromCenter
                                || (double)location.m_maxDistanceFromCenter > 0.0 && (double)lengthXZ > (double)location.m_maxDistanceFromCenter)
                                continue;
                        }

                        // FILTER: TerrainDelta
                        float delta;
                        Vector3 slopeDir;
                        WorldGenerator.instance.GetTerrainDelta(randomPointInZone, location.m_exteriorRadius, out delta, out slopeDir);
                        if ((double)delta > (double)location.m_maxTerrainDelta || (double)delta < (double)location.m_minTerrainDelta)
                        {
                            continue;
                        }

                        // FILTER: minDistanceFromSimilar (must NOT have similar location nearby)
                        if ((double)location.m_minDistanceFromSimilar > 0.0 &&
                            zoneSystem.HaveLocationInRange(location.m_prefab.Name, location.m_group, randomPointInZone, location.m_minDistanceFromSimilar))
                        {
                            continue;
                        }

                        // FILTER: maxDistanceFromSimilar (MUST have similar location nearby)
                        // Note: uses m_prefabName and m_groupMax (asymmetry from vanilla)
                        if ((double)location.m_maxDistanceFromSimilar > 0.0 &&
                            !zoneSystem.HaveLocationInRange(location.m_prefabName, location.m_groupMax, randomPointInZone, location.m_maxDistanceFromSimilar, true))
                        {
                            continue;
                        }

                        float vegMask = mask1.a;

                        // FILTER: Vegetation density bounds
                        if ((double)location.m_minimumVegetation > 0.0 && (double)vegMask <= (double)location.m_minimumVegetation)
                        {
                            continue;
                        }
                        if ((double)location.m_maximumVegetation < 1.0 && (double)vegMask >= (double)location.m_maximumVegetation)
                        {
                            continue;
                        }

                        // FILTER: Surround vegetation check (most expensive, optional)
                        if (location.m_surroundCheckVegetation)
                        {
                            float num3 = 0.0f;
                            for (int index1 = 0; index1 < location.m_surroundCheckLayers; index1++)
                            {
                                float num4 = (float)(index1 + 1) / (float)location.m_surroundCheckLayers * location.m_surroundCheckDistance;
                                for (int index2 = 0; index2 < 6; index2++)
                                {
                                    float f = (float)((double)index2 / 6.0 * 3.1415927410125732 * 2.0);
                                    Vector3 samplePos = randomPointInZone + new Vector3(Mathf.Sin(f) * num4, 0.0f, Mathf.Cos(f) * num4);
                                    Color mask2;
                                    WorldGenerator.instance.GetHeight(samplePos.x, samplePos.z, out mask2);
                                    float num5 = (float)(((double)location.m_surroundCheckDistance - (double)num4) / ((double)location.m_surroundCheckDistance * 2.0));
                                    num3 += mask2.a * num5;
                                }
                            }
                            zoneSystem.s_tempVeg.Add(num3);
                            if (zoneSystem.s_tempVeg.Count >= 10)
                            {
                                float maxVeg = zoneSystem.s_tempVeg.Max();
                                float avgVeg = zoneSystem.s_tempVeg.Average();
                                float threshold = avgVeg + (maxVeg - avgVeg) * location.m_surroundBetterThanAverage;
                                if ((double)num3 < (double)threshold)
                                    continue; // below threshold
                            }
                            else
                            {
                                continue; // need at least 10 samples
                            }
                        }

                        // SUCCESS — register location and move to next zone
                        zoneSystem.RegisterLocation(location, randomPointInZone, false);
                        placed++;
                        break;
                    }
                }

                if (placed < location.m_quantity)
                {
                    worldGenAcceleratorPlugin.TemplateLogger.LogWarning(
                        $"  {location.m_prefab.Name}: placed {placed}/{location.m_quantity} (incomplete)");
                }

                if (worldGenAcceleratorPlugin.TimingLogsEnabled && locationTimer != null)
                {
                    worldGenAcceleratorPlugin.TemplateLogger.LogInfo(
                        $"  {location.m_prefab.Name}: placed {placed}/{location.m_quantity} " +
                        $"({candidateCount} candidate zones, {iterations} point iterations) " +
                        $"in {locationTimer.ElapsedMilliseconds}ms");
                }
            }

            UnityEngine.Random.state = state;
            iterationsPkg.Write(iterations);
            iterationsPkg.SetPos(0);
        }
    }
}

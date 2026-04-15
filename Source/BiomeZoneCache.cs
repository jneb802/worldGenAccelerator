using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace worldGenAccelerator
{
    public class BiomeZoneCache
    {
        public static BiomeZoneCache Instance { get; private set; } = new BiomeZoneCache();

        private Dictionary<Heightmap.Biome, List<Vector2i>> m_zonesByBiome = new Dictionary<Heightmap.Biome, List<Vector2i>>();
        private Dictionary<Vector2i, Heightmap.BiomeArea> m_zoneBiomeArea = new Dictionary<Vector2i, Heightmap.BiomeArea>();
        private int m_cachedZoneCount;
        private bool m_built;

        // Zone grid radius: world radius 10000 / zone size 64 = ~156
        private const int ZoneGridRadius = 156;

        public void Build()
        {
            if (m_built)
                return;

            Stopwatch sw = Stopwatch.StartNew();

            m_zonesByBiome.Clear();
            m_zoneBiomeArea.Clear();
            m_cachedZoneCount = 0;

            WorldGenerator wg = WorldGenerator.instance;

            for (int x = -ZoneGridRadius; x <= ZoneGridRadius; x++)
            {
                for (int y = -ZoneGridRadius; y <= ZoneGridRadius; y++)
                {
                    Vector2i zoneId = new Vector2i(x, y);
                    Vector3 zonePos = ZoneSystem.GetZonePos(zoneId);

                    if ((double)zonePos.magnitude >= 10000.0)
                        continue;

                    Heightmap.Biome biome = wg.GetBiome(zonePos);
                    Heightmap.BiomeArea biomeArea = wg.GetBiomeArea(zonePos);

                    if (!m_zonesByBiome.TryGetValue(biome, out List<Vector2i> list))
                    {
                        list = new List<Vector2i>();
                        m_zonesByBiome[biome] = list;
                    }
                    list.Add(zoneId);
                    m_zoneBiomeArea[zoneId] = biomeArea;
                    m_cachedZoneCount++;
                }
            }

            sw.Stop();
            m_built = true;
            worldGenAcceleratorPlugin.TemplateLogger.LogInfo(
                $"BiomeZoneCache built in {sw.ElapsedMilliseconds}ms ({m_cachedZoneCount} zones cached)");
        }

        public List<Vector2i> GetCandidateZones(Heightmap.Biome biomeMask, Heightmap.BiomeArea biomeAreaMask)
        {
            List<Vector2i> result = new List<Vector2i>();

            foreach (KeyValuePair<Heightmap.Biome, List<Vector2i>> kvp in m_zonesByBiome)
            {
                if ((kvp.Key & biomeMask) == Heightmap.Biome.None)
                    continue;

                foreach (Vector2i zone in kvp.Value)
                {
                    if ((m_zoneBiomeArea[zone] & biomeAreaMask) != (Heightmap.BiomeArea)0)
                    {
                        result.Add(zone);
                    }
                }
            }

            return result;
        }

        public void Reset()
        {
            m_zonesByBiome.Clear();
            m_zoneBiomeArea.Clear();
            m_cachedZoneCount = 0;
            m_built = false;
        }
    }
}

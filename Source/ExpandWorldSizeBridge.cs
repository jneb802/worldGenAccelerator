using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace worldGenAccelerator
{
    /// <summary>
    /// Soft integration with JereKuusela's ExpandWorldSize mod.
    /// Reads the configured world radius via reflection so we don't require
    /// a compile-time reference to the mod. Falls back to vanilla 10000 when
    /// ExpandWorldSize is not installed.
    /// </summary>
    public static class ExpandWorldSizeBridge
    {
        private const string PluginGuid = "expand_world_size";
        private const string ConfigTypeName = "ExpandWorldSize.Configuration";
        private const string RadiusPropertyName = "WorldRadius";
        public const float VanillaWorldRadius = 10000f;

        private static bool s_resolved;
        private static PropertyInfo? s_radiusProperty;

        public static float GetWorldRadius()
        {
            PropertyInfo? prop = GetRadiusProperty();
            if (prop == null)
                return VanillaWorldRadius;

            try
            {
                object? value = prop.GetValue(null);
                if (value is float radius && radius > 0f)
                    return radius;
            }
            catch (Exception ex)
            {
                worldGenAcceleratorPlugin.TemplateLogger.LogWarning(
                    $"Failed to read {ConfigTypeName}.{RadiusPropertyName} from ExpandWorldSize: {ex.Message}. Falling back to vanilla radius.");
            }
            return VanillaWorldRadius;
        }

        private static PropertyInfo? GetRadiusProperty()
        {
            if (s_resolved)
                return s_radiusProperty;

            s_resolved = true;

            if (!Chainloader.PluginInfos.TryGetValue(PluginGuid, out BepInEx.PluginInfo pluginInfo))
                return null;

            Assembly ewsAssembly = pluginInfo.Instance.GetType().Assembly;
            Type? cfg = ewsAssembly.GetType(ConfigTypeName);
            if (cfg == null)
            {
                worldGenAcceleratorPlugin.TemplateLogger.LogWarning(
                    $"ExpandWorldSize is loaded but type {ConfigTypeName} was not found. Falling back to vanilla radius.");
                return null;
            }

            PropertyInfo? prop = cfg.GetProperty(
                RadiusPropertyName,
                BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
            {
                worldGenAcceleratorPlugin.TemplateLogger.LogWarning(
                    $"ExpandWorldSize is loaded but {ConfigTypeName}.{RadiusPropertyName} was not found. Falling back to vanilla radius.");
                return null;
            }

            s_radiusProperty = prop;
            return prop;
        }
    }
}

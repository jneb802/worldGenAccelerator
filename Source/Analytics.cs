using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine.Networking;

namespace worldGenAccelerator
{
    public static class Analytics
    {
        private const string PingUrl = "https://mod-analytics.vercel.app/api/ping";
        private static bool _hasSentPing;

        public static void Init(ConfigFile config, string modId, string modVersion)
        {
            if (_hasSentPing) return;

            ConfigEntry<bool> enabled = config.Bind(
                "Analytics", "Enabled",
                true,
                "Send a single anonymous ping when the game starts. No gameplay data is collected.");

            ConfigEntry<string> instanceId = config.Bind(
                "Analytics", "InstanceID",
                Guid.NewGuid().ToString(),
                "Random anonymous ID. Change or delete to reset.");

            if (!enabled.Value)
            {
                worldGenAcceleratorPlugin.TemplateLogger.LogDebug("Analytics disabled by config");
                return;
            }

            _hasSentPing = true;
            ThreadingHelper.Instance.StartCoroutine(SendPing(modId, modVersion, instanceId.Value));
        }

        private static IEnumerator SendPing(string modId, string modVersion, string instanceId)
        {
            string json = $"{{\"mod_id\":\"{Escape(modId)}\",\"mod_version\":\"{Escape(modVersion)}\",\"instance_id\":\"{Escape(instanceId)}\"}}";
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

            using UnityWebRequest req = new UnityWebRequest(PingUrl, "POST");
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                worldGenAcceleratorPlugin.TemplateLogger.LogDebug("Analytics ping sent");
            else
                worldGenAcceleratorPlugin.TemplateLogger.LogDebug($"Analytics ping failed: {req.error}");
        }

        private static string Escape(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

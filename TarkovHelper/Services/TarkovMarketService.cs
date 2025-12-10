using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TarkovHelper.Debug;
using TarkovHelper.Models;

namespace TarkovHelper.Services
{
    /// <summary>
    /// Service for fetching quest and marker data from Tarkov Market API
    /// Handles the obfuscated response decoding
    /// </summary>
    public class TarkovMarketService : IDisposable
    {
        private static TarkovMarketService? _instance;
        public static TarkovMarketService Instance => _instance ??= new TarkovMarketService();

        private readonly HttpClient _httpClient;
        private const string MarkersEndpoint = "https://tarkov-market.com/api/be/markers/list";
        private const string QuestsEndpoint = "https://tarkov-market.com/api/be/quests/list";

        // Supported map names for markers API
        public static readonly string[] SupportedMaps = new[]
        {
            "customs", "factory", "interchange", "labs", "lighthouse",
            "reserve", "shoreline", "streets", "woods", "ground-zero"
        };

        public TarkovMarketService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TarkovHelper/1.0");
        }

        /// <summary>
        /// Decode obfuscated string from Tarkov Market API
        /// Algorithm: Remove index 5-9 (5 chars), Base64 decode, URL decode
        /// </summary>
        public static string? DecodeObfuscatedString(string encoded)
        {
            try
            {
                if (string.IsNullOrEmpty(encoded) || encoded.Length < 11)
                    return null;

                // 1. Remove index 5~9 (5 chars) - the obfuscation
                var processed = encoded.Substring(0, 5) + encoded.Substring(10);

                // 2. Base64 decode
                var bytes = Convert.FromBase64String(processed);
                var urlEncoded = Encoding.UTF8.GetString(bytes);

                // 3. URL decode
                var json = Uri.UnescapeDataString(urlEncoded);

                return json;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Decode markers from obfuscated response
        /// </summary>
        public static List<TarkovMarketMarker>? DecodeMarkers(string encoded)
        {
            var json = DecodeObfuscatedString(encoded);
            if (json == null) return null;

            try
            {
                return JsonSerializer.Deserialize<List<TarkovMarketMarker>>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Decode quests from obfuscated response
        /// </summary>
        public static List<TarkovMarketQuest>? DecodeQuests(string encoded, Action<string>? debugCallback = null)
        {
            var json = DecodeObfuscatedString(encoded);
            if (json == null)
            {
                debugCallback?.Invoke("[DEBUG] DecodeObfuscatedString returned null");
                System.Diagnostics.Debug.WriteLine("[TarkovMarket] DecodeObfuscatedString returned null");
                return null;
            }

            debugCallback?.Invoke($"[DEBUG] Decoded JSON length: {json.Length}");
            System.Diagnostics.Debug.WriteLine($"[TarkovMarket] Decoded JSON length: {json.Length}");
            System.Diagnostics.Debug.WriteLine($"[TarkovMarket] First 500 chars: {json.Substring(0, Math.Min(500, json.Length))}");

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    // Ignore unknown fields in JSON
                    UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
                    // Allow reading numbers from strings (e.g., "reqLevel": "" or "reqLevel": "10")
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                };
                var result = JsonSerializer.Deserialize<List<TarkovMarketQuest>>(json, options);
                debugCallback?.Invoke($"[DEBUG] Deserialized count: {result?.Count ?? -1}");
                System.Diagnostics.Debug.WriteLine($"[TarkovMarket] Deserialized count: {result?.Count ?? -1}");
                return result;
            }
            catch (Exception ex)
            {
                debugCallback?.Invoke($"[DEBUG] Deserialize exception: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[TarkovMarket] Deserialize EXCEPTION: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[TarkovMarket] Stack: {ex.StackTrace}");

                // JSON 파일로 저장해서 확인
                try
                {
                    var errorPath = Path.Combine(AppEnv.DataPath, "tarkov_market_raw_response.json");
                    File.WriteAllText(errorPath, json, Encoding.UTF8);
                    System.Diagnostics.Debug.WriteLine($"[TarkovMarket] Raw JSON saved to: {errorPath}");
                }
                catch { }

                return null;
            }
        }

        /// <summary>
        /// Fetch markers for a specific map
        /// </summary>
        public async Task<List<TarkovMarketMarker>?> FetchMarkersAsync(string mapName)
        {
            try
            {
                var url = $"{MarkersEndpoint}?map={mapName}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseBytes = await response.Content.ReadAsByteArrayAsync();
                var responseJson = Encoding.UTF8.GetString(responseBytes);

                var wrapper = JsonSerializer.Deserialize<TarkovMarketMarkersResponse>(responseJson);
                if (wrapper == null || string.IsNullOrEmpty(wrapper.Markers))
                    return null;

                return DecodeMarkers(wrapper.Markers);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch markers for {mapName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Fetch all markers from all maps
        /// </summary>
        public async Task<Dictionary<string, List<TarkovMarketMarker>>> FetchAllMarkersAsync(
            Action<string>? progressCallback = null)
        {
            var result = new Dictionary<string, List<TarkovMarketMarker>>();

            foreach (var map in SupportedMaps)
            {
                progressCallback?.Invoke($"Fetching markers for {map}...");

                var markers = await FetchMarkersAsync(map);
                if (markers != null)
                {
                    result[map] = markers;
                }

                // Small delay to avoid rate limiting
                await Task.Delay(100);
            }

            return result;
        }

        /// <summary>
        /// Fetch quest list from Tarkov Market API
        /// </summary>
        public async Task<List<TarkovMarketQuest>?> FetchQuestsAsync(Action<string>? debugCallback = null)
        {
            try
            {
                debugCallback?.Invoke($"[DEBUG] Fetching from {QuestsEndpoint}");
                var response = await _httpClient.GetAsync(QuestsEndpoint);
                debugCallback?.Invoke($"[DEBUG] Response status: {response.StatusCode}");
                response.EnsureSuccessStatusCode();

                var responseBytes = await response.Content.ReadAsByteArrayAsync();
                var responseJson = Encoding.UTF8.GetString(responseBytes);
                debugCallback?.Invoke($"[DEBUG] Response length: {responseJson.Length}");

                var wrapper = JsonSerializer.Deserialize<TarkovMarketQuestsResponse>(responseJson);
                if (wrapper == null)
                {
                    debugCallback?.Invoke("[DEBUG] wrapper is null");
                    return null;
                }
                if (string.IsNullOrEmpty(wrapper.Quests))
                {
                    debugCallback?.Invoke($"[DEBUG] wrapper.Quests is empty, result={wrapper.Result}");
                    return null;
                }

                debugCallback?.Invoke($"[DEBUG] Encoded quests length: {wrapper.Quests.Length}");
                var decoded = DecodeQuests(wrapper.Quests, debugCallback);
                debugCallback?.Invoke($"[DEBUG] Decoded quests count: {decoded?.Count ?? -1}");
                return decoded;
            }
            catch (Exception ex)
            {
                debugCallback?.Invoke($"[DEBUG] Exception: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Failed to fetch quests: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Save quests to JSON file for caching
        /// </summary>
        public async Task SaveQuestsToJsonAsync(List<TarkovMarketQuest> quests, string? fileName = null)
        {
            fileName ??= "tarkov_market_quests.json";
            var filePath = Path.Combine(AppEnv.DataPath, fileName);
            Directory.CreateDirectory(AppEnv.DataPath);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(quests, options);
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
        }

        /// <summary>
        /// Load quests from JSON file
        /// </summary>
        public async Task<List<TarkovMarketQuest>?> LoadQuestsFromJsonAsync(string? fileName = null)
        {
            fileName ??= "tarkov_market_quests.json";
            var filePath = Path.Combine(AppEnv.DataPath, fileName);

            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<TarkovMarketQuest>>(json);
        }

        /// <summary>
        /// Save mismatch report to JSON file
        /// </summary>
        public async Task SaveMismatchReportAsync(QuestMismatchReport report, string? fileName = null)
        {
            fileName ??= "quest_mismatch_report.json";
            var filePath = Path.Combine(AppEnv.DataPath, fileName);
            Directory.CreateDirectory(AppEnv.DataPath);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(report, options);
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
        }

        /// <summary>
        /// Load mismatch report from JSON file
        /// </summary>
        public async Task<QuestMismatchReport?> LoadMismatchReportAsync(string? fileName = null)
        {
            fileName ??= "quest_mismatch_report.json";
            var filePath = Path.Combine(AppEnv.DataPath, fileName);

            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<QuestMismatchReport>(json);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}

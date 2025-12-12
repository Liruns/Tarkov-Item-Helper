using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using TarkovDBEditor.Models;

namespace TarkovDBEditor.Services;

/// <summary>
/// Tarkov Market API 서비스
/// 마커 데이터 가져오기 및 디코딩
/// </summary>
public class TarkovMarketService : IDisposable
{
    private readonly HttpClient _httpClient;
    private const string MarkersApiBase = "https://tarkov-market.com/api/be/markers/list";
    private const string QuestsApiBase = "https://tarkov-market.com/api/be/quests/list";

    // 캐시된 퀘스트 목록 (앱 실행 중 유지)
    private List<TarkovMarketQuest>? _cachedQuests;

    private readonly string _cacheDir;

    /// <summary>
    /// 지원하는 맵 이름 목록 (API용)
    /// </summary>
    public static readonly Dictionary<string, string> MapNameMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Customs", "customs" },
        { "Factory", "factory" },
        { "Interchange", "interchange" },
        { "Labs", "labs" },
        { "Lighthouse", "lighthouse" },
        { "Reserve", "reserve" },
        { "Shoreline", "shoreline" },
        { "StreetsOfTarkov", "streets" },
        { "Woods", "woods" },
        { "GroundZero", "ground-zero" },
        { "Labyrinth", "labyrinth" }
    };

    public TarkovMarketService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TarkovDBEditor/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        _cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "tarkov_market");
        Directory.CreateDirectory(_cacheDir);
    }

    /// <summary>
    /// 특정 맵의 마커 데이터 가져오기
    /// </summary>
    public async Task<List<TarkovMarketMarker>> FetchMarkersAsync(
        string mapKey,
        bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        // API용 맵 이름 변환
        if (!MapNameMapping.TryGetValue(mapKey, out var apiMapName))
        {
            apiMapName = mapKey.ToLowerInvariant();
        }

        // 캐시 확인
        var cacheFile = Path.Combine(_cacheDir, $"markers_{apiMapName}.json");
        if (useCache && File.Exists(cacheFile))
        {
            var cacheAge = DateTime.Now - File.GetLastWriteTime(cacheFile);
            if (cacheAge.TotalHours < 24) // 24시간 캐시
            {
                try
                {
                    var cachedJson = await File.ReadAllTextAsync(cacheFile, cancellationToken);
                    var cachedMarkers = JsonSerializer.Deserialize<List<TarkovMarketMarker>>(cachedJson);
                    if (cachedMarkers != null)
                    {
                        return cachedMarkers;
                    }
                }
                catch
                {
                    // 캐시 읽기 실패 시 API 호출
                }
            }
        }

        // API 호출
        var url = $"{MarkersApiBase}?map={apiMapName}";
        System.Diagnostics.Debug.WriteLine($"[FetchMarkers] Calling API: {url}");

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        System.Diagnostics.Debug.WriteLine($"[FetchMarkers] Response length: {json.Length}");

        var apiResponse = JsonSerializer.Deserialize<TarkovMarketMarkersResponse>(json);

        if (apiResponse == null)
        {
            System.Diagnostics.Debug.WriteLine("[FetchMarkers] Failed to parse API response");
            return new List<TarkovMarketMarker>();
        }

        if (string.IsNullOrEmpty(apiResponse.Markers))
        {
            System.Diagnostics.Debug.WriteLine("[FetchMarkers] Markers field is empty");
            return new List<TarkovMarketMarker>();
        }

        System.Diagnostics.Debug.WriteLine($"[FetchMarkers] Markers field length: {apiResponse.Markers.Length}");

        // 난독화 디코딩
        var markers = DecodeMarkers(apiResponse.Markers);
        if (markers == null)
        {
            System.Diagnostics.Debug.WriteLine("[FetchMarkers] DecodeMarkers returned null");
            return new List<TarkovMarketMarker>();
        }

        // 캐시 저장
        try
        {
            var cacheJson = JsonSerializer.Serialize(markers, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(cacheFile, cacheJson, cancellationToken);
        }
        catch
        {
            // 캐시 저장 실패는 무시
        }

        return markers;
    }

    /// <summary>
    /// 퀘스트 데이터 가져오기 (마커의 questUid와 매칭용)
    /// </summary>
    public async Task<List<TarkovMarketQuest>> FetchQuestsAsync(
        bool useCache = true,
        CancellationToken cancellationToken = default)
    {
        // 메모리 캐시 확인
        if (useCache && _cachedQuests != null)
        {
            return _cachedQuests;
        }

        // 파일 캐시 확인
        var cacheFile = Path.Combine(_cacheDir, "quests.json");
        if (useCache && File.Exists(cacheFile))
        {
            var cacheAge = DateTime.Now - File.GetLastWriteTime(cacheFile);
            if (cacheAge.TotalHours < 24) // 24시간 캐시
            {
                try
                {
                    var cachedJson = await File.ReadAllTextAsync(cacheFile, cancellationToken);
                    var cachedQuests = JsonSerializer.Deserialize<List<TarkovMarketQuest>>(cachedJson);
                    if (cachedQuests != null)
                    {
                        _cachedQuests = cachedQuests;
                        return cachedQuests;
                    }
                }
                catch
                {
                    // 캐시 읽기 실패 시 API 호출
                }
            }
        }

        // API 호출
        System.Diagnostics.Debug.WriteLine($"[FetchQuests] Calling API: {QuestsApiBase}");

        var response = await _httpClient.GetAsync(QuestsApiBase, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        System.Diagnostics.Debug.WriteLine($"[FetchQuests] Response length: {json.Length}");

        var apiResponse = JsonSerializer.Deserialize<TarkovMarketQuestsResponse>(json);

        if (apiResponse == null || string.IsNullOrEmpty(apiResponse.Quests))
        {
            System.Diagnostics.Debug.WriteLine("[FetchQuests] Quests field is empty");
            return new List<TarkovMarketQuest>();
        }

        // 난독화 디코딩
        var quests = DecodeQuests(apiResponse.Quests);
        if (quests == null)
        {
            System.Diagnostics.Debug.WriteLine("[FetchQuests] DecodeQuests returned null");
            return new List<TarkovMarketQuest>();
        }

        // 메모리 캐시
        _cachedQuests = quests;

        // 파일 캐시 저장
        try
        {
            var cacheJson = JsonSerializer.Serialize(quests, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(cacheFile, cacheJson, cancellationToken);
        }
        catch
        {
            // 캐시 저장 실패는 무시
        }

        return quests;
    }

    /// <summary>
    /// questUid로 퀘스트 찾기
    /// </summary>
    public TarkovMarketQuest? FindQuestByUid(string? questUid)
    {
        if (string.IsNullOrEmpty(questUid) || _cachedQuests == null)
        {
            return null;
        }

        return _cachedQuests.FirstOrDefault(q => q.Uid == questUid);
    }

    /// <summary>
    /// 난독화된 마커 데이터 디코딩
    /// 알고리즘: index 5~9 (5글자) 제거 → Base64 디코드 → URL 디코드 → JSON 파싱
    /// </summary>
    public static List<TarkovMarketMarker>? DecodeMarkers(string encoded)
    {
        try
        {
            if (string.IsNullOrEmpty(encoded) || encoded.Length < 11)
            {
                System.Diagnostics.Debug.WriteLine($"[DecodeMarkers] Invalid input: length={encoded?.Length ?? 0}");
                return null;
            }

            // 1. index 5~9 (5글자) 제거
            var processed = encoded.Substring(0, 5) + encoded.Substring(10);
            System.Diagnostics.Debug.WriteLine($"[DecodeMarkers] Processed: {processed.Substring(0, Math.Min(50, processed.Length))}...");

            // 2. Base64 디코드
            var bytes = Convert.FromBase64String(processed);
            var urlEncoded = Encoding.UTF8.GetString(bytes);
            System.Diagnostics.Debug.WriteLine($"[DecodeMarkers] URL encoded: {urlEncoded.Substring(0, Math.Min(100, urlEncoded.Length))}...");

            // 3. URL 디코드
            var json = Uri.UnescapeDataString(urlEncoded);
            System.Diagnostics.Debug.WriteLine($"[DecodeMarkers] JSON length: {json.Length}");

            // 4. JSON 파싱
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };
            var result = JsonSerializer.Deserialize<List<TarkovMarketMarker>>(json, options);
            System.Diagnostics.Debug.WriteLine($"[DecodeMarkers] Parsed {result?.Count ?? 0} markers");
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DecodeMarkers] Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[DecodeMarkers] Stack: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// 난독화된 퀘스트 데이터 디코딩
    /// 알고리즘: index 5~9 (5글자) 제거 → Base64 디코드 → URL 디코드 → JSON 파싱
    /// </summary>
    public static List<TarkovMarketQuest>? DecodeQuests(string encoded)
    {
        try
        {
            if (string.IsNullOrEmpty(encoded) || encoded.Length < 11)
            {
                System.Diagnostics.Debug.WriteLine($"[DecodeQuests] Invalid input: length={encoded?.Length ?? 0}");
                return null;
            }

            // 1. index 5~9 (5글자) 제거
            var processed = encoded.Substring(0, 5) + encoded.Substring(10);

            // 2. Base64 디코드
            var bytes = Convert.FromBase64String(processed);
            var urlEncoded = Encoding.UTF8.GetString(bytes);

            // 3. URL 디코드
            var json = Uri.UnescapeDataString(urlEncoded);
            System.Diagnostics.Debug.WriteLine($"[DecodeQuests] JSON length: {json.Length}");

            // 4. JSON 파싱
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };
            var result = JsonSerializer.Deserialize<List<TarkovMarketQuest>>(json, options);
            System.Diagnostics.Debug.WriteLine($"[DecodeQuests] Parsed {result?.Count ?? 0} quests");
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DecodeQuests] Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 캐시 삭제
    /// </summary>
    public void ClearCache()
    {
        try
        {
            _cachedQuests = null; // 메모리 캐시 삭제

            if (Directory.Exists(_cacheDir))
            {
                foreach (var file in Directory.GetFiles(_cacheDir, "markers_*.json"))
                {
                    File.Delete(file);
                }

                // 퀘스트 캐시 삭제
                var questCacheFile = Path.Combine(_cacheDir, "quests.json");
                if (File.Exists(questCacheFile))
                {
                    File.Delete(questCacheFile);
                }
            }
        }
        catch
        {
            // 삭제 실패 무시
        }
    }

    /// <summary>
    /// 캐시 정보 가져오기
    /// </summary>
    public Dictionary<string, DateTime> GetCacheInfo()
    {
        var result = new Dictionary<string, DateTime>();

        if (Directory.Exists(_cacheDir))
        {
            foreach (var file in Directory.GetFiles(_cacheDir, "markers_*.json"))
            {
                var mapName = Path.GetFileNameWithoutExtension(file).Replace("markers_", "");
                result[mapName] = File.GetLastWriteTime(file);
            }
        }

        return result;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// 좌표 변환 서비스
/// Affine 변환 행렬 계산 및 적용
/// </summary>
public static class CoordinateTransformService
{
    /// <summary>
    /// 매칭된 참조점들로 Affine 변환 행렬 계산
    /// 최소 3개의 참조점 필요
    /// </summary>
    /// <param name="referencePoints">참조점 목록 (DB좌표, API SVG좌표)</param>
    /// <returns>변환 행렬 [a, b, c, d, tx, ty] 또는 실패 시 null</returns>
    public static double[]? CalculateAffineTransform(
        List<(double dbX, double dbZ, double svgX, double svgY)> referencePoints)
    {
        if (referencePoints.Count < 3)
        {
            return null;
        }

        int n = referencePoints.Count;

        // 최소제곱법으로 Affine 변환 계산
        // [gameX] = [a  b] [svgX] + [tx]
        // [gameZ]   [c  d] [svgY]   [ty]

        // X 변환 (gameX = a*svgX + b*svgY + tx)
        // Y 변환 (gameZ = c*svgX + d*svgY + ty)

        // 행렬 A: [[svgX, svgY, 1], ...] (n x 3)
        // 벡터 bX: [gameX, ...] (n x 1)
        // 벡터 bZ: [gameZ, ...] (n x 1)

        // A^T * A * x = A^T * b 형태로 풀기

        // A^T * A (3x3)
        double sumX2 = 0, sumY2 = 0, sumXY = 0, sumX = 0, sumY = 0;
        double sumGameX = 0, sumGameZ = 0;
        double sumXGameX = 0, sumYGameX = 0;
        double sumXGameZ = 0, sumYGameZ = 0;

        foreach (var (dbX, dbZ, svgX, svgY) in referencePoints)
        {
            sumX2 += svgX * svgX;
            sumY2 += svgY * svgY;
            sumXY += svgX * svgY;
            sumX += svgX;
            sumY += svgY;
            sumGameX += dbX;
            sumGameZ += dbZ;
            sumXGameX += svgX * dbX;
            sumYGameX += svgY * dbX;
            sumXGameZ += svgX * dbZ;
            sumYGameZ += svgY * dbZ;
        }

        // 3x3 행렬 (A^T * A)
        // [sumX2,  sumXY, sumX ]
        // [sumXY,  sumY2, sumY ]
        // [sumX,   sumY,  n    ]

        // 3x3 역행렬 계산
        var det = sumX2 * (sumY2 * n - sumY * sumY)
                - sumXY * (sumXY * n - sumY * sumX)
                + sumX * (sumXY * sumY - sumY2 * sumX);

        if (Math.Abs(det) < 1e-10)
        {
            return null; // 특이 행렬
        }

        // 역행렬의 각 요소
        double invA11 = (sumY2 * n - sumY * sumY) / det;
        double invA12 = -(sumXY * n - sumY * sumX) / det;
        double invA13 = (sumXY * sumY - sumY2 * sumX) / det;
        double invA21 = -(sumXY * n - sumX * sumY) / det;
        double invA22 = (sumX2 * n - sumX * sumX) / det;
        double invA23 = -(sumX2 * sumY - sumXY * sumX) / det;
        double invA31 = (sumXY * sumY - sumX * sumY2) / det;
        double invA32 = -(sumX2 * sumY - sumX * sumXY) / det;
        double invA33 = (sumX2 * sumY2 - sumXY * sumXY) / det;

        // X 변환 계수 (a, b, tx)
        double a = invA11 * sumXGameX + invA12 * sumYGameX + invA13 * sumGameX;
        double b = invA21 * sumXGameX + invA22 * sumYGameX + invA23 * sumGameX;
        double tx = invA31 * sumXGameX + invA32 * sumYGameX + invA33 * sumGameX;

        // Z 변환 계수 (c, d, ty)
        double c = invA11 * sumXGameZ + invA12 * sumYGameZ + invA13 * sumGameZ;
        double d = invA21 * sumXGameZ + invA22 * sumYGameZ + invA23 * sumGameZ;
        double ty = invA31 * sumXGameZ + invA32 * sumYGameZ + invA33 * sumGameZ;

        return new[] { a, b, c, d, tx, ty };
    }

    /// <summary>
    /// 변환 행렬로 SVG 좌표를 게임 좌표로 변환
    /// </summary>
    public static (double gameX, double gameZ) TransformSvgToGame(
        double svgX, double svgY, double[] transform)
    {
        if (transform == null || transform.Length < 6)
        {
            return (svgX, svgY);
        }

        var a = transform[0];
        var b = transform[1];
        var c = transform[2];
        var d = transform[3];
        var tx = transform[4];
        var ty = transform[5];

        var gameX = a * svgX + b * svgY + tx;
        var gameZ = c * svgX + d * svgY + ty;

        return (gameX, gameZ);
    }

    /// <summary>
    /// 변환 오차 계산
    /// </summary>
    public static double CalculateError(
        List<(double dbX, double dbZ, double svgX, double svgY)> referencePoints,
        double[] transform)
    {
        if (referencePoints.Count == 0 || transform == null)
        {
            return double.MaxValue;
        }

        double totalError = 0;
        foreach (var (dbX, dbZ, svgX, svgY) in referencePoints)
        {
            var (calcX, calcZ) = TransformSvgToGame(svgX, svgY, transform);
            var dx = calcX - dbX;
            var dz = calcZ - dbZ;
            totalError += Math.Sqrt(dx * dx + dz * dz);
        }

        return totalError / referencePoints.Count;
    }
}

/// <summary>
/// 마커 매칭 서비스
/// </summary>
public static class MarkerMatchingService
{
    /// <summary>
    /// DB 마커와 API 마커 자동 매칭
    /// </summary>
    public static List<MarkerMatchResult> AutoMatch(
        List<MapMarker> dbMarkers,
        List<TarkovMarketMarker> apiMarkers)
    {
        var results = new List<MarkerMatchResult>();
        var usedApiMarkers = new HashSet<string>();

        foreach (var dbMarker in dbMarkers)
        {
            // 같은 타입의 API 마커 찾기 (Geometry가 있는 것만)
            var candidates = apiMarkers
                .Where(api => !usedApiMarkers.Contains(api.Uid) &&
                             api.Geometry != null &&
                             api.MappedMarkerType == dbMarker.MarkerType)
                .ToList();

            if (candidates.Count == 0)
            {
                continue;
            }

            // 이름 유사도로 최적 매칭 찾기
            var bestMatch = candidates
                .Select(api => new
                {
                    ApiMarker = api,
                    Similarity = CalculateNameSimilarity(dbMarker.Name, api.Name)
                })
                .OrderByDescending(x => x.Similarity)
                .FirstOrDefault();

            if (bestMatch != null && bestMatch.Similarity > 0.3) // 30% 이상 유사도
            {
                results.Add(new MarkerMatchResult
                {
                    DbMarker = dbMarker,
                    ApiMarker = bestMatch.ApiMarker,
                    NameSimilarity = bestMatch.Similarity,
                    IsReferencePoint = false,
                    IsManualMatch = false
                });

                usedApiMarkers.Add(bestMatch.ApiMarker.Uid);
            }
        }

        return results;
    }

    /// <summary>
    /// 이름 유사도 계산 (0~1)
    /// </summary>
    public static double CalculateNameSimilarity(string name1, string name2)
    {
        if (string.IsNullOrEmpty(name1) || string.IsNullOrEmpty(name2))
        {
            return 0;
        }

        // 정규화
        var n1 = NormalizeName(name1);
        var n2 = NormalizeName(name2);

        // 정확히 일치
        if (n1 == n2)
        {
            return 1.0;
        }

        // 포함 관계
        if (n1.Contains(n2) || n2.Contains(n1))
        {
            return 0.8;
        }

        // Levenshtein 거리 기반 유사도
        var distance = LevenshteinDistance(n1, n2);
        var maxLen = Math.Max(n1.Length, n2.Length);
        return 1.0 - (double)distance / maxLen;
    }

    private static string NormalizeName(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("_", "")
            .Replace("'", "")
            .Replace("\"", "");
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        var m = s1.Length;
        var n = s2.Length;
        var d = new int[m + 1, n + 1];

        for (var i = 0; i <= m; i++) d[i, 0] = i;
        for (var j = 0; j <= n; j++) d[0, j] = j;

        for (var j = 1; j <= n; j++)
        {
            for (var i = 1; i <= m; i++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[m, n];
    }

    /// <summary>
    /// API 마커의 Level을 FloorId로 변환
    /// </summary>
    public static string? MapLevelToFloorId(int? level, string mapKey, List<MapFloorConfig>? floors)
    {
        if (floors == null || floors.Count == 0)
        {
            return null;
        }

        // level이 없으면 기본 층
        if (!level.HasValue)
        {
            return floors.FirstOrDefault(f => f.IsDefault)?.LayerId ?? "main";
        }

        // level 값에 따른 매핑 (맵마다 다를 수 있음)
        var floorIndex = level.Value;

        // Order 기준으로 정렬된 층 목록
        var sortedFloors = floors.OrderBy(f => f.Order).ToList();

        // level 1이 보통 main (Order 0)에 해당
        // level 0 또는 음수는 basement 등
        // level 2, 3은 level2, level3에 해당

        if (floorIndex <= 0)
        {
            // 지하층 찾기
            var basementFloor = sortedFloors.FirstOrDefault(f => f.Order < 0);
            return basementFloor?.LayerId ?? sortedFloors.FirstOrDefault()?.LayerId;
        }
        else if (floorIndex == 1)
        {
            // 메인 층
            return sortedFloors.FirstOrDefault(f => f.Order == 0)?.LayerId ?? "main";
        }
        else
        {
            // 상위 층
            var upperFloor = sortedFloors.FirstOrDefault(f => f.Order == floorIndex - 1);
            return upperFloor?.LayerId ?? sortedFloors.LastOrDefault()?.LayerId;
        }
    }
}

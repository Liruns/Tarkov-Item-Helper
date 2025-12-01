using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace TarkovHelper.Services;

/// <summary>
/// 이미지 다운로드 및 로컬 캐싱 서비스
/// </summary>
public static class ImageCacheService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static readonly Dictionary<string, BitmapImage> MemoryCache = new();
    private static readonly object CacheLock = new();

    private static string CacheDirectory => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "Cache",
        "Images"
    );

    static ImageCacheService()
    {
        // 캐시 디렉토리 생성
        if (!Directory.Exists(CacheDirectory))
        {
            Directory.CreateDirectory(CacheDirectory);
        }
    }

    /// <summary>
    /// URL에서 이미지를 가져옴 (캐시 우선)
    /// </summary>
    public static BitmapImage? GetImage(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;

        // 메모리 캐시 확인
        lock (CacheLock)
        {
            if (MemoryCache.TryGetValue(url, out var cachedImage))
            {
                return cachedImage;
            }
        }

        // 파일 캐시 확인
        var cacheFilePath = GetCacheFilePath(url);
        if (File.Exists(cacheFilePath))
        {
            try
            {
                var image = LoadImageFromFile(cacheFilePath);
                if (image != null)
                {
                    lock (CacheLock)
                    {
                        MemoryCache[url] = image;
                    }
                    return image;
                }
            }
            catch
            {
                // 캐시 파일이 손상되었으면 삭제
                try { File.Delete(cacheFilePath); } catch { }
            }
        }

        // 비동기로 다운로드 시작 (일단 null 반환)
        _ = DownloadAndCacheAsync(url);
        return null;
    }

    /// <summary>
    /// URL에서 이미지를 비동기로 가져옴 (캐시 우선)
    /// </summary>
    public static async Task<BitmapImage?> GetImageAsync(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;

        // 메모리 캐시 확인
        lock (CacheLock)
        {
            if (MemoryCache.TryGetValue(url, out var cachedImage))
            {
                return cachedImage;
            }
        }

        // 파일 캐시 확인
        var cacheFilePath = GetCacheFilePath(url);
        if (File.Exists(cacheFilePath))
        {
            try
            {
                var image = LoadImageFromFile(cacheFilePath);
                if (image != null)
                {
                    lock (CacheLock)
                    {
                        MemoryCache[url] = image;
                    }
                    return image;
                }
            }
            catch
            {
                try { File.Delete(cacheFilePath); } catch { }
            }
        }

        // 다운로드
        return await DownloadAndCacheAsync(url);
    }

    /// <summary>
    /// 이미지 다운로드 및 캐싱
    /// </summary>
    private static async Task<BitmapImage?> DownloadAndCacheAsync(string url)
    {
        try
        {
            var response = await HttpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var imageData = await response.Content.ReadAsByteArrayAsync();
            if (imageData.Length == 0) return null;

            // 파일에 저장
            var cacheFilePath = GetCacheFilePath(url);
            await File.WriteAllBytesAsync(cacheFilePath, imageData);

            // 이미지 로드
            var image = LoadImageFromBytes(imageData);
            if (image != null)
            {
                lock (CacheLock)
                {
                    MemoryCache[url] = image;
                }
            }

            return image;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 파일에서 이미지 로드
    /// </summary>
    private static BitmapImage? LoadImageFromFile(string filePath)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(filePath, UriKind.Absolute);
            image.EndInit();
            image.Freeze(); // 스레드 안전을 위해 Freeze
            return image;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 바이트 배열에서 이미지 로드
    /// </summary>
    private static BitmapImage? LoadImageFromBytes(byte[] data)
    {
        try
        {
            var image = new BitmapImage();
            using var ms = new MemoryStream(data);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// URL을 파일 경로로 변환
    /// </summary>
    private static string GetCacheFilePath(string url)
    {
        // URL을 해시하여 파일명 생성
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16];
        var extension = GetExtensionFromUrl(url);
        return Path.Combine(CacheDirectory, $"{hash}{extension}");
    }

    /// <summary>
    /// URL에서 확장자 추출
    /// </summary>
    private static string GetExtensionFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" => ext,
                _ => ".png"
            };
        }
        catch
        {
            return ".png";
        }
    }

    /// <summary>
    /// 캐시 정리 (오래된 파일 삭제)
    /// </summary>
    public static void CleanupCache(int maxAgeDays = 30)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-maxAgeDays);
            foreach (var file in Directory.GetFiles(CacheDirectory))
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastAccessTime < cutoffDate)
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// 모든 캐시 삭제
    /// </summary>
    public static void ClearCache()
    {
        lock (CacheLock)
        {
            MemoryCache.Clear();
        }

        try
        {
            foreach (var file in Directory.GetFiles(CacheDirectory))
            {
                try { File.Delete(file); } catch { }
            }
        }
        catch { }
    }
}

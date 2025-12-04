using TarkovHelper.Models.MapTracker;

namespace TarkovHelper.Services.MapTracker;

/// <summary>
/// 월드 좌표를 화면 좌표로 변환하는 서비스.
///
/// [좌표 변환 원리]
/// 월드 좌표 (WorldMinX ~ WorldMaxX) → 이미지 좌표 (0 ~ ImageWidth)
/// 선형 변환 공식:
/// screenX = (worldX - WorldMinX) / (WorldMaxX - WorldMinX) * ImageWidth + OffsetX
/// screenY = (worldY - WorldMinY) / (WorldMaxY - WorldMinY) * ImageHeight + OffsetY
///
/// InvertY가 true인 경우:
/// screenY = ImageHeight - screenY (Y축 반전)
/// </summary>
public sealed class MapCoordinateTransformer : IMapCoordinateTransformer
{
    private Dictionary<string, MapConfig> _mapConfigs = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _aliasToKey = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 빈 맵 설정으로 변환기를 생성합니다.
    /// </summary>
    public MapCoordinateTransformer()
    {
    }

    /// <summary>
    /// 지정된 맵 설정으로 변환기를 생성합니다.
    /// </summary>
    /// <param name="maps">맵 설정 목록</param>
    public MapCoordinateTransformer(IEnumerable<MapConfig> maps)
    {
        UpdateMaps(maps);
    }

    /// <inheritdoc />
    public bool TryTransform(EftPosition worldPosition, out ScreenPosition? screenPosition)
    {
        return TryTransform(worldPosition.MapName, worldPosition.X, worldPosition.Y, worldPosition.Angle, out screenPosition);
    }

    /// <inheritdoc />
    public bool TryTransform(string mapKey, double worldX, double worldY, double? angle, out ScreenPosition? screenPosition)
    {
        screenPosition = null;

        var config = GetMapConfig(mapKey);
        if (config == null)
            return false;

        try
        {
            // X 좌표 변환
            var rangeX = config.WorldMaxX - config.WorldMinX;
            if (Math.Abs(rangeX) < double.Epsilon)
                return false;

            var normalizedX = (worldX - config.WorldMinX) / rangeX;
            var screenX = normalizedX * config.ImageWidth + config.OffsetX;

            if (config.InvertX)
                screenX = config.ImageWidth - screenX;

            // Y 좌표 변환
            var rangeY = config.WorldMaxY - config.WorldMinY;
            if (Math.Abs(rangeY) < double.Epsilon)
                return false;

            var normalizedY = (worldY - config.WorldMinY) / rangeY;
            var screenY = normalizedY * config.ImageHeight + config.OffsetY;

            if (config.InvertY)
                screenY = config.ImageHeight - screenY;

            screenPosition = new ScreenPosition
            {
                MapKey = config.Key,
                X = screenX,
                Y = screenY,
                Angle = angle,
                OriginalPosition = new EftPosition
                {
                    MapName = mapKey,
                    X = worldX,
                    Y = worldY,
                    Angle = angle,
                    Timestamp = DateTime.Now
                }
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void UpdateMaps(IEnumerable<MapConfig> maps)
    {
        _mapConfigs.Clear();
        _aliasToKey.Clear();

        foreach (var map in maps)
        {
            if (string.IsNullOrWhiteSpace(map.Key))
                continue;

            _mapConfigs[map.Key] = map;
            _aliasToKey[map.Key] = map.Key;

            // 별칭 등록
            if (map.Aliases != null)
            {
                foreach (var alias in map.Aliases)
                {
                    if (!string.IsNullOrWhiteSpace(alias))
                        _aliasToKey[alias] = map.Key;
                }
            }
        }
    }

    /// <inheritdoc />
    public MapConfig? GetMapConfig(string mapKey)
    {
        if (string.IsNullOrWhiteSpace(mapKey))
            return null;

        // 별칭에서 실제 키 조회
        if (_aliasToKey.TryGetValue(mapKey, out var actualKey))
        {
            return _mapConfigs.GetValueOrDefault(actualKey);
        }

        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAllMapKeys()
    {
        return _mapConfigs.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// 화면 좌표를 월드 좌표로 역변환합니다. (디버깅/테스트용)
    /// </summary>
    public bool TryReverseTransform(string mapKey, double screenX, double screenY, out double worldX, out double worldY)
    {
        worldX = 0;
        worldY = 0;

        var config = GetMapConfig(mapKey);
        if (config == null)
            return false;

        try
        {
            var adjustedScreenX = screenX - config.OffsetX;
            var adjustedScreenY = screenY - config.OffsetY;

            if (config.InvertX)
                adjustedScreenX = config.ImageWidth - adjustedScreenX;

            if (config.InvertY)
                adjustedScreenY = config.ImageHeight - adjustedScreenY;

            var normalizedX = adjustedScreenX / config.ImageWidth;
            var normalizedY = adjustedScreenY / config.ImageHeight;

            worldX = normalizedX * (config.WorldMaxX - config.WorldMinX) + config.WorldMinX;
            worldY = normalizedY * (config.WorldMaxY - config.WorldMinY) + config.WorldMinY;

            return true;
        }
        catch
        {
            return false;
        }
    }
}

using System.IO;

namespace TarkovHelper.Models.MapTracker;

/// <summary>
/// 맵 트래커 전체 설정.
/// Data/map_tracker_settings.json 파일에 저장됩니다.
/// </summary>
public sealed class MapTrackerSettings
{
    /// <summary>
    /// EFT 스크린샷 폴더 경로.
    /// 기본값: C:\Users\{현재사용자}\Documents\Escape from Tarkov\Screenshots
    /// </summary>
    public string ScreenshotFolderPath { get; set; } = GetDefaultScreenshotPath();

    /// <summary>
    /// 스크린샷 파일명 파싱용 정규식 패턴.
    /// 필수 그룹: x, y
    /// 선택 그룹: z, map, angle, qx, qy, qz, qw
    ///
    /// [패턴 수정 가이드]
    /// 실제 파일명 형식에 맞게 수정하세요.
    ///
    /// 예시 파일명: "2023-09-22[13-00]_-49.9, 12.1, -51.8_0.0, -0.8, 0.1, -0.5_14.08.png"
    /// </summary>
    public string FileNamePattern { get; set; } = DefaultFileNamePattern;

    /// <summary>
    /// 파일 변경 감지 후 처리 대기 시간 (밀리초).
    /// 파일 쓰기 완료를 기다리는 디바운싱 시간입니다.
    /// </summary>
    public int DebounceDelayMs { get; set; } = 500;

    /// <summary>
    /// 맵 설정 목록
    /// </summary>
    public List<MapConfig> Maps { get; set; } = GetDefaultMaps();

    /// <summary>
    /// 마커 크기 (픽셀)
    /// </summary>
    public int MarkerSize { get; set; } = 16;

    /// <summary>
    /// 마커 색상 (ARGB hex, 예: "#FFFF0000" = 빨간색)
    /// </summary>
    public string MarkerColor { get; set; } = "#FFFF5722";

    /// <summary>
    /// 방향 표시 여부
    /// </summary>
    public bool ShowDirection { get; set; } = true;

    /// <summary>
    /// 이동 경로 표시 여부
    /// </summary>
    public bool ShowTrail { get; set; } = true;

    /// <summary>
    /// 이동 경로 최대 포인트 수
    /// </summary>
    public int MaxTrailPoints { get; set; } = 50;

    /// <summary>
    /// 기본 스크린샷 폴더 경로 반환.
    /// 여러 경로를 시도하여 실제 존재하는 폴더를 찾습니다.
    /// </summary>
    private static string GetDefaultScreenshotPath()
    {
        var detectedPath = TryDetectScreenshotFolder();
        if (!string.IsNullOrEmpty(detectedPath))
            return detectedPath;

        // 폴백: 기본 경로 반환
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "Escape from Tarkov", "Screenshots");
    }

    /// <summary>
    /// EFT 스크린샷 폴더를 자동으로 탐지합니다.
    /// 여러 경로 전략과 대소문자 변형을 시도합니다.
    /// </summary>
    /// <returns>탐지된 폴더 경로, 없으면 null</returns>
    public static string? TryDetectScreenshotFolder()
    {
        // EFT 폴더 이름 변형 (대소문자)
        var eftFolderVariants = new[]
        {
            "Escape from Tarkov",
            "Escape From Tarkov",
            "escape from tarkov"
        };

        // 전략 1: MyDocuments 경로
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        foreach (var variant in eftFolderVariants)
        {
            var path = Path.Combine(documentsPath, variant, "Screenshots");
            if (Directory.Exists(path))
                return path;
        }

        // 전략 2: UserProfile 경로 (OneDrive 등 리디렉션된 경우)
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documentsFolders = new[] { "Documents", "문서", "My Documents" };

        foreach (var docFolder in documentsFolders)
        {
            foreach (var variant in eftFolderVariants)
            {
                var path = Path.Combine(userProfile, docFolder, variant, "Screenshots");
                if (Directory.Exists(path))
                    return path;
            }
        }

        // 전략 3: OneDrive 문서 폴더
        var oneDrivePath = Environment.GetEnvironmentVariable("OneDrive");
        if (!string.IsNullOrEmpty(oneDrivePath))
        {
            foreach (var docFolder in documentsFolders)
            {
                foreach (var variant in eftFolderVariants)
                {
                    var path = Path.Combine(oneDrivePath, docFolder, variant, "Screenshots");
                    if (Directory.Exists(path))
                        return path;

                    // OneDrive 루트에 바로 있는 경우
                    path = Path.Combine(oneDrivePath, variant, "Screenshots");
                    if (Directory.Exists(path))
                        return path;
                }
            }
        }

        // 전략 4: 일반 드라이브에서 EFT 폴더 탐색 (C:, D:, E:)
        var drives = new[] { "C:", "D:", "E:" };
        var commonPaths = new[]
        {
            @"Users\{user}\Documents",
            @"Games",
            @"Program Files\Battlestate Games",
            @"Battlestate Games"
        };

        var userName = Environment.UserName;
        foreach (var drive in drives)
        {
            foreach (var commonPath in commonPaths)
            {
                var basePath = Path.Combine(drive + "\\", commonPath.Replace("{user}", userName));
                foreach (var variant in eftFolderVariants)
                {
                    var path = Path.Combine(basePath, variant, "Screenshots");
                    try
                    {
                        if (Directory.Exists(path))
                            return path;
                    }
                    catch
                    {
                        // 권한 오류 무시
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 가능한 모든 스크린샷 폴더 경로를 반환합니다.
    /// UI에서 선택지로 제공할 수 있습니다.
    /// </summary>
    public static List<string> GetPossibleScreenshotPaths()
    {
        var paths = new List<string>();

        var eftFolderVariants = new[]
        {
            "Escape from Tarkov",
            "Escape From Tarkov"
        };

        // MyDocuments
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        foreach (var variant in eftFolderVariants)
        {
            var path = Path.Combine(documentsPath, variant, "Screenshots");
            if (Directory.Exists(path) && !paths.Contains(path))
                paths.Add(path);
        }

        // UserProfile Documents
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var variant in eftFolderVariants)
        {
            var path = Path.Combine(userProfile, "Documents", variant, "Screenshots");
            if (Directory.Exists(path) && !paths.Contains(path))
                paths.Add(path);
        }

        // OneDrive
        var oneDrivePath = Environment.GetEnvironmentVariable("OneDrive");
        if (!string.IsNullOrEmpty(oneDrivePath))
        {
            foreach (var variant in eftFolderVariants)
            {
                var path = Path.Combine(oneDrivePath, "Documents", variant, "Screenshots");
                if (Directory.Exists(path) && !paths.Contains(path))
                    paths.Add(path);
            }
        }

        return paths;
    }

    /// <summary>
    /// 기본 파일명 패턴.
    /// EFT 스크린샷 파일명 형식 (쿼터니언): "2023-09-22[13-00]_-49.9, 12.1, -51.8_0.0, -0.8, 0.1, -0.5_14.08.png"
    ///
    /// [지원되는 그룹]
    /// - x, y: 좌표 (필수)
    /// - z: 높이 (선택)
    /// - map: 맵 이름 (선택 - 없으면 "Unknown")
    /// - angle: 방향 각도 (선택)
    /// - qx, qy, qz, qw: 쿼터니언 회전값 (선택 - angle 대신 사용 가능)
    /// </summary>
    private const string DefaultFileNamePattern =
        @"\d{4}-\d{2}-\d{2}\[\d{2}-\d{2}\]_(?<x>-?\d+\.?\d*),\s*(?<y>-?\d+\.?\d*),\s*(?<z>-?\d+\.?\d*)_(?<qx>-?\d+\.?\d*),\s*(?<qy>-?\d+\.?\d*),\s*(?<qz>-?\d+\.?\d*),\s*(?<qw>-?\d+\.?\d*)_";

    /// <summary>
    /// 기본 맵 설정 목록 생성.
    /// tarkov.dev의 maps.json 데이터 기반.
    /// SVG 파일 사용, Transform 좌표 변환 방식.
    /// </summary>
    private static List<MapConfig> GetDefaultMaps()
    {
        return new List<MapConfig>
        {
            new()
            {
                Key = "Woods",
                DisplayName = "Woods",
                ImagePath = "Assets/Maps/Woods.svg",
                // bounds: [[650,-945],[-762,470]] -> X: -762~650, Y: -945~470
                WorldMinX = -762.0,
                WorldMaxX = 650.0,
                WorldMinY = -945.0,
                WorldMaxY = 470.0,
                // SVG viewBox: 0 0 1401.8693 1420.5972
                ImageWidth = 1402,
                ImageHeight = 1421,
                Transform = [0.1855, 113.1, 0.1855, 167.8],
                CoordinateRotation = 180,
                Aliases = new List<string> { "woods", "WOODS" }
            },
            new()
            {
                Key = "Customs",
                DisplayName = "Customs",
                ImagePath = "Assets/Maps/Customs.svg",
                // bounds: [[698,-307],[-372,237]] -> X: -372~698, Y: -307~237
                WorldMinX = -372.0,
                WorldMaxX = 698.0,
                WorldMinY = -307.0,
                WorldMaxY = 237.0,
                // SVG viewBox: 0 0 1062.4827 535.17401
                ImageWidth = 1062,
                ImageHeight = 535,
                Transform = [0.239, 168.65, 0.239, 136.35],
                CoordinateRotation = 180,
                Aliases = new List<string> { "customs", "CUSTOMS", "bigmap" }
            },
            new()
            {
                Key = "Shoreline",
                DisplayName = "Shoreline",
                ImagePath = "Assets/Maps/Shoreline.svg",
                // bounds: [[508,-415],[-1060,618]] -> X: -1060~508, Y: -415~618
                WorldMinX = -1060.0,
                WorldMaxX = 508.0,
                WorldMinY = -415.0,
                WorldMaxY = 618.0,
                // SVG viewBox: 0 0 1559.5717 1032.4935
                ImageWidth = 1560,
                ImageHeight = 1032,
                Transform = [0.16, 83.2, 0.16, 111.1],
                CoordinateRotation = 180,
                Aliases = new List<string> { "shoreline", "SHORELINE" }
            },
            new()
            {
                Key = "Interchange",
                DisplayName = "Interchange",
                ImagePath = "Assets/Maps/Interchange.svg",
                // bounds: [[532.75,-442.75],[-364,453.5]] -> X: -364~532.75, Y: -442.75~453.5
                WorldMinX = -364.0,
                WorldMaxX = 532.75,
                WorldMinY = -442.75,
                WorldMaxY = 453.5,
                // SVG viewBox: 0 0 977.09998 977.09998
                ImageWidth = 977,
                ImageHeight = 977,
                Transform = [0.265, 150.6, 0.265, 134.6],
                CoordinateRotation = 180,
                Aliases = new List<string> { "interchange", "INTERCHANGE" }
            },
            new()
            {
                Key = "Reserve",
                DisplayName = "Reserve",
                ImagePath = "Assets/Maps/Reserve.svg",
                // bounds: [[289,-293],[-303,244]] -> X: -303~289, Y: -293~244
                WorldMinX = -303.0,
                WorldMaxX = 289.0,
                WorldMinY = -293.0,
                WorldMaxY = 244.0,
                // SVG viewBox: 0 0 827.28742 761.16437
                ImageWidth = 827,
                ImageHeight = 761,
                Transform = [0.395, 122.0, 0.395, 137.65],
                CoordinateRotation = 180,
                Aliases = new List<string> { "reserve", "RESERVE", "RezervBase" }
            },
            new()
            {
                Key = "Lighthouse",
                DisplayName = "Lighthouse",
                ImagePath = "Assets/Maps/Lighthouse.svg",
                // bounds: [[515,-998],[-545,725]] -> X: -545~515, Y: -998~725
                WorldMinX = -545.0,
                WorldMaxX = 515.0,
                WorldMinY = -998.0,
                WorldMaxY = 725.0,
                // SVG viewBox: 0 0 1059.3752 1722.9499
                ImageWidth = 1059,
                ImageHeight = 1723,
                Transform = [0.2, 0, 0.2, 0],
                CoordinateRotation = 180,
                Aliases = new List<string> { "lighthouse", "LIGHTHOUSE" }
            },
            new()
            {
                Key = "StreetsOfTarkov",
                DisplayName = "Streets of Tarkov",
                ImagePath = "Assets/Maps/StreetsOfTarkov.svg",
                // bounds: [[323,-317],[-280,554]] -> X: -280~323, Y: -317~554
                WorldMinX = -280.0,
                WorldMaxX = 323.0,
                WorldMinY = -317.0,
                WorldMaxY = 554.0,
                // SVG viewBox: 0 0 605.32395 831.57753
                ImageWidth = 605,
                ImageHeight = 832,
                Transform = [0.38, 0, 0.38, 0],
                CoordinateRotation = 180,
                Aliases = new List<string> { "streets", "STREETS", "TarkovStreets" }
            },
            new()
            {
                Key = "Factory",
                DisplayName = "Factory",
                ImagePath = "Assets/Maps/Factory.svg",
                // bounds: [[79,-64.5],[-66.5,67.4]] -> X: -66.5~79, Y: -64.5~67.4
                WorldMinX = -66.5,
                WorldMaxX = 79.0,
                WorldMinY = -64.5,
                WorldMaxY = 67.4,
                // SVG viewBox: 0 0 130.81831 141.23242
                ImageWidth = 131,
                ImageHeight = 141,
                Transform = [1.629, 119.9, 1.629, 139.3],
                CoordinateRotation = 90,
                Aliases = new List<string> { "factory", "FACTORY", "factory4_day", "factory4_night" }
            },
            new()
            {
                Key = "GroundZero",
                DisplayName = "Ground Zero",
                ImagePath = "Assets/Maps/GroundZero.svg",
                // bounds: [[249,-124],[-99,364]] -> X: -99~249, Y: -124~364
                WorldMinX = -99.0,
                WorldMaxX = 249.0,
                WorldMinY = -124.0,
                WorldMaxY = 364.0,
                // SVG viewBox: 0 0 348.92543 488.44792
                ImageWidth = 349,
                ImageHeight = 488,
                Transform = [0.524, 167.3, 0.524, 65.1],
                CoordinateRotation = 180,
                Aliases = new List<string> { "groundzero", "GROUNDZERO", "Sandbox", "sandbox" }
            },
            new()
            {
                Key = "Labs",
                DisplayName = "The Lab",
                ImagePath = "Assets/Maps/Labs.svg",
                // bounds: [[-80,-477],[-287,-193]] -> X: -287~-80, Y: -477~-193
                WorldMinX = -287.0,
                WorldMaxX = -80.0,
                WorldMinY = -477.0,
                WorldMaxY = -193.0,
                // Labs는 SVG가 없어 placeholder
                ImageWidth = 400,
                ImageHeight = 400,
                Transform = [0.575, 281.2, 0.575, 193.7],
                CoordinateRotation = 270,
                Aliases = new List<string> { "labs", "LABS", "laboratory" }
            }
        };
    }
}

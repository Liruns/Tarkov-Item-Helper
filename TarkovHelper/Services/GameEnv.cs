using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace TarkovHelper.Services;

/// <summary>
/// Game path and log folder management
/// </summary>
public static class GameEnv
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string DataDirectory => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "Data"
    );

    private static string SettingsPath => Path.Combine(DataDirectory, "game_settings.json");

    private static string? _gameFolder;
    private static bool _settingsLoaded;
    private static string? _detectionMethod;

    /// <summary>
    /// Game installation folder
    /// </summary>
    public static string? GameFolder
    {
        get
        {
            if (!_settingsLoaded)
            {
                LoadSettings();
            }

            if (_gameFolder == null)
            {
                _gameFolder = DetectGameFolder();
            }

            return _gameFolder;
        }
        set
        {
            _gameFolder = value;
            SaveSettings();
        }
    }

    /// <summary>
    /// Logs folder path (handles both BSG Launcher and Steam versions)
    /// BSG Launcher: GameFolder/Logs
    /// Steam: GameFolder/build/Logs
    /// </summary>
    public static string? LogsFolder
    {
        get
        {
            var gameFolder = GameFolder;
            if (string.IsNullOrEmpty(gameFolder))
            {
                return null;
            }

            // Steam version: build/Logs (check first as it's more specific)
            var steamLogsPath = Path.Combine(gameFolder, "build", "Logs");
            if (Directory.Exists(steamLogsPath))
            {
                return steamLogsPath;
            }

            // BSG Launcher version: Logs
            var bsgLogsPath = Path.Combine(gameFolder, "Logs");
            if (Directory.Exists(bsgLogsPath))
            {
                return bsgLogsPath;
            }

            // Neither exists - check if it looks like Steam installation
            // Steam version has "build" folder even if Logs doesn't exist yet
            var buildFolder = Path.Combine(gameFolder, "build");
            if (Directory.Exists(buildFolder))
            {
                return steamLogsPath;
            }

            // Check if path contains Steam indicators
            if (gameFolder.Contains("steamapps", StringComparison.OrdinalIgnoreCase) ||
                gameFolder.Contains("Steam", StringComparison.OrdinalIgnoreCase))
            {
                return steamLogsPath;
            }

            return bsgLogsPath;
        }
    }

    /// <summary>
    /// Check if game folder is valid
    /// </summary>
    public static bool IsGameFolderValid
    {
        get
        {
            var folder = GameFolder;
            if (string.IsNullOrEmpty(folder))
            {
                return false;
            }

            return Directory.Exists(folder);
        }
    }

    /// <summary>
    /// Check if logs folder exists
    /// </summary>
    public static bool IsLogsFolderValid
    {
        get
        {
            var folder = LogsFolder;
            if (string.IsNullOrEmpty(folder))
            {
                return false;
            }

            return Directory.Exists(folder);
        }
    }

    /// <summary>
    /// How the game folder was detected (e.g., "Steam", "BSG Launcher", "Registry", "Default Path")
    /// </summary>
    public static string? DetectionMethod => _detectionMethod;

    /// <summary>
    /// Try to detect game folder from multiple sources
    /// </summary>
    private static string? DetectGameFolder()
    {
        string? result;

        // 1. Try BSG Launcher registry (official launcher)
        result = TryDetectFromBsgLauncher();
        if (result != null)
        {
            _detectionMethod = "BSG Launcher";
            return result;
        }

        // 2. Try Steam installation
        result = TryDetectFromSteam();
        if (result != null)
        {
            _detectionMethod = "Steam";
            return result;
        }

        // 3. Try legacy registry entry
        result = TryDetectFromRegistry();
        if (result != null)
        {
            _detectionMethod = "Registry";
            return result;
        }

        // 4. Try default installation paths
        result = TryDetectFromDefaultPaths();
        if (result != null)
        {
            _detectionMethod = "Default Path";
            return result;
        }

        _detectionMethod = null;
        return null;
    }

    /// <summary>
    /// Detect from BSG Launcher registry
    /// </summary>
    private static string? TryDetectFromBsgLauncher()
    {
        try
        {
            // BSG Launcher stores installation path in registry
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov");

            var installPath = key?.GetValue("InstallLocation")?.ToString();
            if (!string.IsNullOrEmpty(installPath) && IsValidTarkovFolder(installPath))
            {
                return installPath;
            }

            // Try HKCU as well
            using var userKey = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Battlestate Games\EscapeFromTarkov");
            var userPath = userKey?.GetValue("InstallLocation")?.ToString();
            if (!string.IsNullOrEmpty(userPath) && IsValidTarkovFolder(userPath))
            {
                return userPath;
            }
        }
        catch
        {
            // Registry access failed
        }

        return null;
    }

    /// <summary>
    /// Detect from Steam installation
    /// </summary>
    private static string? TryDetectFromSteam()
    {
        try
        {
            // Get Steam installation path from registry
            string? steamPath = null;

            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam"))
            {
                steamPath = key?.GetValue("SteamPath")?.ToString();
            }

            if (string.IsNullOrEmpty(steamPath))
            {
                // Try default Steam path
                var defaultSteamPath = @"C:\Program Files (x86)\Steam";
                if (Directory.Exists(defaultSteamPath))
                {
                    steamPath = defaultSteamPath;
                }
            }

            if (string.IsNullOrEmpty(steamPath))
            {
                return null;
            }

            // Normalize path separators
            steamPath = steamPath.Replace("/", "\\");

            // Get all Steam library folders
            var libraryFolders = GetSteamLibraryFolders(steamPath);

            // Check each library folder for Tarkov (try multiple folder names)
            string[] possibleFolderNames = ["Escape from Tarkov", "EscapeFromTarkov"];

            foreach (var libraryFolder in libraryFolders)
            {
                foreach (var folderName in possibleFolderNames)
                {
                    var tarkovPath = Path.Combine(libraryFolder, "steamapps", "common", folderName);
                    if (IsValidTarkovFolder(tarkovPath))
                    {
                        return tarkovPath;
                    }
                }
            }
        }
        catch
        {
            // Steam detection failed
        }

        return null;
    }

    /// <summary>
    /// Get all Steam library folders from libraryfolders.vdf
    /// </summary>
    private static List<string> GetSteamLibraryFolders(string steamPath)
    {
        var folders = new List<string> { steamPath };

        try
        {
            var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdfPath))
            {
                return folders;
            }

            var content = File.ReadAllText(vdfPath);

            // Parse VDF format to find path entries
            // Format: "path"		"D:\\SteamLibrary"
            var pathRegex = new Regex(@"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
            var matches = pathRegex.Matches(content);

            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var path = match.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(path) && !folders.Contains(path, StringComparer.OrdinalIgnoreCase))
                    {
                        folders.Add(path);
                    }
                }
            }
        }
        catch
        {
            // VDF parsing failed
        }

        return folders;
    }

    /// <summary>
    /// Detect from legacy registry entry
    /// </summary>
    private static string? TryDetectFromRegistry()
    {
        try
        {
            // Try additional registry locations
            string[] registryPaths =
            [
                @"SOFTWARE\WOW6432Node\EscapeFromTarkov",
                @"SOFTWARE\EscapeFromTarkov",
                @"SOFTWARE\Battlestate Games\EscapeFromTarkov"
            ];

            foreach (var regPath in registryPaths)
            {
                using var key = Registry.LocalMachine.OpenSubKey(regPath);
                var installPath = key?.GetValue("InstallLocation")?.ToString()
                    ?? key?.GetValue("InstallPath")?.ToString()
                    ?? key?.GetValue("Path")?.ToString();

                if (!string.IsNullOrEmpty(installPath) && IsValidTarkovFolder(installPath))
                {
                    return installPath;
                }
            }
        }
        catch
        {
            // Registry access failed
        }

        return null;
    }

    /// <summary>
    /// Try common default installation paths
    /// </summary>
    private static string? TryDetectFromDefaultPaths()
    {
        // Common installation paths
        string[] defaultPaths =
        [
            @"C:\Battlestate Games\EFT",
            @"C:\Battlestate Games\Escape from Tarkov",
            @"D:\Battlestate Games\EFT",
            @"D:\Battlestate Games\Escape from Tarkov",
            @"E:\Battlestate Games\EFT",
            @"E:\Battlestate Games\Escape from Tarkov",
            @"C:\Games\EFT",
            @"D:\Games\EFT",
            @"C:\Program Files\Battlestate Games\EFT",
            @"C:\Program Files (x86)\Battlestate Games\EFT"
        ];

        foreach (var path in defaultPaths)
        {
            if (IsValidTarkovFolder(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Check if a folder is a valid Tarkov installation
    /// </summary>
    public static bool IsValidTarkovFolder(string? folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            return false;
        }

        // Check for common Tarkov files/folders
        // BSG Launcher version
        var exePath = Path.Combine(folderPath, "EscapeFromTarkov.exe");
        var bsgLogsPath = Path.Combine(folderPath, "Logs");

        // Steam version - has build folder structure
        var steamBuildPath = Path.Combine(folderPath, "build");
        var steamLogsPath = Path.Combine(folderPath, "build", "Logs");
        var steamExePath = Path.Combine(folderPath, "build", "EscapeFromTarkov.exe");

        // Check if any valid indicator exists
        return File.Exists(exePath) ||
               File.Exists(steamExePath) ||
               Directory.Exists(bsgLogsPath) ||
               Directory.Exists(steamLogsPath) ||
               Directory.Exists(steamBuildPath);
    }

    /// <summary>
    /// Force re-detection of game folder
    /// </summary>
    public static string? ForceDetect()
    {
        _gameFolder = null;
        _detectionMethod = null;
        return DetectGameFolder();
    }

    /// <summary>
    /// Save settings to JSON file
    /// </summary>
    public static void SaveSettings()
    {
        try
        {
            if (!Directory.Exists(DataDirectory))
            {
                Directory.CreateDirectory(DataDirectory);
            }

            var settings = new GameSettings { GameFolder = _gameFolder };
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Save failed
        }
    }

    /// <summary>
    /// Load settings from JSON file
    /// </summary>
    private static void LoadSettings()
    {
        _settingsLoaded = true;

        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<GameSettings>(json, JsonOptions);
                if (settings != null && !string.IsNullOrEmpty(settings.GameFolder))
                {
                    _gameFolder = settings.GameFolder;
                }
            }
        }
        catch
        {
            // Load failed, use auto-detection
        }
    }

    /// <summary>
    /// Reset settings to auto-detect
    /// </summary>
    public static void ResetSettings()
    {
        _gameFolder = null;
        _settingsLoaded = false;

        try
        {
            if (File.Exists(SettingsPath))
            {
                File.Delete(SettingsPath);
            }
        }
        catch
        {
            // Delete failed
        }
    }

    private class GameSettings
    {
        public string? GameFolder { get; set; }
    }
}

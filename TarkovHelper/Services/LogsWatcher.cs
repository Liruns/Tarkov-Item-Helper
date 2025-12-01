using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TarkovHelper.Services;

/// <summary>
/// Log watcher for detecting quest completion from game logs
/// </summary>
public class LogsWatcher : IDisposable
{
    // Quest completion notification substring
    private const string NotificationSubstring = "push-notifications|Got notification | ChatMessageReceived";

    // Regex to detect start of a new log line (timestamp)
    private static readonly Regex LineStartRegex = new(@"^\d{4}-\d{2}-\d{2} \d{1,2}:\d{1,2}:\d{1,2}\.\d{3}",
        RegexOptions.Compiled);

    private FileSystemWatcher? _logsFoldersWatcher;
    private LogFileWatcher? _notifLogFileWatcher;
    private string? _currentLogFolder;
    private readonly Dictionary<string, long> _filePositions = new();

    private int _initialLogsReadCount;
    private bool IsAllInitialLogsRead => _initialLogsReadCount >= 1;

    /// <summary>
    /// Current watcher status
    /// </summary>
    public LogWatcherStatus Status { get; private set; } = LogWatcherStatus.NotStarted;

    /// <summary>
    /// Status message for display
    /// </summary>
    public string? StatusMessage { get; private set; }

    /// <summary>
    /// Event fired when a quest is completed
    /// </summary>
    public event EventHandler<QuestCompletedEventArgs>? QuestCompleted;

    /// <summary>
    /// Event fired when status changes
    /// </summary>
    public event EventHandler<LogWatcherStatus>? StatusChanged;

    private void SetStatus(LogWatcherStatus status, string? message = null)
    {
        Status = status;
        StatusMessage = message;
        StatusChanged?.Invoke(this, status);
    }

    /// <summary>
    /// Start watching logs
    /// </summary>
    public void Start()
    {
        ResetInitialLogsReadDone();

        var gameFolder = GameEnv.GameFolder;
        if (string.IsNullOrEmpty(gameFolder))
        {
            SetStatus(LogWatcherStatus.Error, "Game folder not set");
            return;
        }

        if (!Directory.Exists(gameFolder))
        {
            SetStatus(LogWatcherStatus.Error, $"Game folder not found: '{gameFolder}'");
            return;
        }

        var logsFolder = GameEnv.LogsFolder;
        if (string.IsNullOrEmpty(logsFolder) || !Directory.Exists(logsFolder))
        {
            SetStatus(LogWatcherStatus.Error, $"Logs folder not found: '{logsFolder}'");
            return;
        }

        // Get newest log folder
        _currentLogFolder = GetLatestLogFolder(logsFolder);
        if (_currentLogFolder != null)
        {
            MonitorLogFolder(_currentLogFolder);
        }

        // Watch for new folder creation (new game sessions)
        try
        {
            _logsFoldersWatcher = new FileSystemWatcher(logsFolder);
            _logsFoldersWatcher.Created += OnNewFolderCreated;
            _logsFoldersWatcher.EnableRaisingEvents = true;

            SetStatus(LogWatcherStatus.Monitoring, $"Monitoring: {logsFolder}");
        }
        catch (Exception ex)
        {
            SetStatus(LogWatcherStatus.Error, $"Failed to start folder watcher: {ex.Message}");
        }
    }

    /// <summary>
    /// Stop watching logs
    /// </summary>
    public void Stop()
    {
        ClearLogsFoldersWatcher();
        ClearLogsWatcher();
        SetStatus(LogWatcherStatus.NotStarted);
    }

    /// <summary>
    /// Restart watching logs
    /// </summary>
    public void Restart()
    {
        Stop();
        Start();
    }

    private void ClearLogsFoldersWatcher()
    {
        if (_logsFoldersWatcher != null)
        {
            _logsFoldersWatcher.Created -= OnNewFolderCreated;
            _logsFoldersWatcher.Dispose();
            _logsFoldersWatcher = null;
        }

        _filePositions.Clear();
    }

    private void MonitorLogFolder(string logsFolder)
    {
        ClearLogsWatcher();

        try
        {
            // Watch for notification log file changes
            _notifLogFileWatcher = new LogFileWatcher(logsFolder, "*notifications_*.log");
            _notifLogFileWatcher.Created += OnLogFileChanged;
            _notifLogFileWatcher.Changed += OnLogFileChanged;
            _notifLogFileWatcher.Start();

            SetStatus(LogWatcherStatus.Monitoring, $"Monitoring: {logsFolder}");
        }
        catch (Exception ex)
        {
            SetStatus(LogWatcherStatus.Error, $"Failed to monitor log folder: {ex.Message}");
        }
    }

    private void ClearLogsWatcher()
    {
        if (_notifLogFileWatcher != null)
        {
            _notifLogFileWatcher.Created -= OnLogFileChanged;
            _notifLogFileWatcher.Changed -= OnLogFileChanged;
            _notifLogFileWatcher.Stop();
            _notifLogFileWatcher = null;
        }
    }

    private void OnNewFolderCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            var newDirectory = e.FullPath;
            if (_currentLogFolder == null ||
                Directory.GetCreationTime(newDirectory) > Directory.GetCreationTime(_currentLogFolder))
            {
                _currentLogFolder = newDirectory;
                ResetInitialLogsReadDone();
                MonitorLogFolder(_currentLogFolder);
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    private static string? GetLatestLogFolder(string logsFolder)
    {
        try
        {
            var directories = Directory.GetDirectories(logsFolder);
            if (directories.Length == 0)
            {
                return null;
            }

            // Sort by creation date
            return directories.OrderByDescending(d => Directory.GetCreationTime(d)).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private void OnLogFileChanged(object? sender, FileChangedEventArgs e)
    {
        ProcessLogFile(e.FullPath);
    }

    private void ProcessLogFile(string filePath)
    {
        try
        {
            // Get last read position
            long lastPosition = 0;
            if (_filePositions.TryGetValue(filePath, out var pos))
            {
                lastPosition = pos;
            }

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(lastPosition, SeekOrigin.Begin);

            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                // Skip initial read - don't process existing logs
                if (!IsAllInitialLogsRead)
                {
                    continue;
                }

                if (line.Contains(NotificationSubstring))
                {
                    // Read JSON content
                    var jsonBuilder = new StringBuilder();
                    line = reader.ReadLine();

                    while (line != null)
                    {
                        // Check if this is a new log entry (starts with timestamp)
                        if (LineStartRegex.IsMatch(line))
                        {
                            break;
                        }

                        jsonBuilder.AppendLine(line);
                        line = reader.ReadLine();
                    }

                    // Parse JSON and extract quest info
                    ParseQuestNotification(jsonBuilder.ToString());
                }
            }

            // Save read position
            _filePositions[filePath] = stream.Position;
        }
        catch (Exception)
        {
            // Log file read error - ignore
        }

        // Mark initial read as done
        SetInitialLogsReadDone();
    }

    private void ParseQuestNotification(string jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("message", out var message))
            {
                if (message.TryGetProperty("type", out var typeElement) &&
                    message.TryGetProperty("templateId", out var templateIdElement))
                {
                    var status = typeElement.GetString();
                    var templateId = templateIdElement.GetString();

                    if (!string.IsNullOrEmpty(templateId))
                    {
                        // templateId format: "questId statusMessageText"
                        // e.g., "6574e0dedc0d635f633a5805 successMessageText"
                        var parts = templateId.Split(' ');
                        if (parts.Length > 0)
                        {
                            var questId = parts[0];
                            if (!string.IsNullOrEmpty(questId))
                            {
                                QuestCompleted?.Invoke(this, new QuestCompletedEventArgs(questId, status ?? "unknown"));
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // JSON parse error - ignore
        }
    }

    private void SetInitialLogsReadDone()
    {
        if (!IsAllInitialLogsRead)
        {
            _initialLogsReadCount++;
        }
    }

    private void ResetInitialLogsReadDone()
    {
        _initialLogsReadCount = 0;
        _filePositions.Clear();
    }

    public void Dispose()
    {
        Stop();
    }
}

/// <summary>
/// Log watcher status
/// </summary>
public enum LogWatcherStatus
{
    NotStarted,
    Monitoring,
    Error
}

/// <summary>
/// Event args for quest completion
/// </summary>
public class QuestCompletedEventArgs : EventArgs
{
    public string QuestId { get; }
    public string Status { get; }

    public QuestCompletedEventArgs(string questId, string status)
    {
        QuestId = questId;
        Status = status;
    }
}

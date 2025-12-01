using System.IO;

namespace TarkovHelper.Services;

/// <summary>
/// Custom file watcher that polls for file changes
/// </summary>
public class LogFileWatcher : IDisposable
{
    private readonly string _folder;
    private readonly string _searchPattern;
    private readonly int _checkInterval;

    private volatile bool _isStopping;
    private long _lastFileSize;
    private FileSystemWatcher? _fileCreateWatcher;
    private Task? _pollingTask;

    /// <summary>
    /// Event fired when file is created
    /// </summary>
    public event EventHandler<FileChangedEventArgs>? Created;

    /// <summary>
    /// Event fired when file content changes
    /// </summary>
    public event EventHandler<FileChangedEventArgs>? Changed;

    public LogFileWatcher(string folder, string searchPattern, int checkInterval = 3000)
    {
        _folder = folder;
        _searchPattern = searchPattern;
        _checkInterval = checkInterval;
    }

    /// <summary>
    /// Try to get the file path matching the search pattern
    /// </summary>
    private string? TryGetFilePath()
    {
        try
        {
            var files = Directory.GetFiles(_folder, _searchPattern);
            if (files.Length > 0)
            {
                return files[0];
            }
        }
        catch
        {
            // Directory access failed
        }

        return null;
    }

    /// <summary>
    /// Start watching for file changes
    /// </summary>
    public void Start()
    {
        Reset();

        var filePath = TryGetFilePath();

        if (!string.IsNullOrEmpty(filePath))
        {
            // File exists - start monitoring changes
            StartFileChangeMonitoring(filePath);
        }
        else
        {
            // File doesn't exist - wait for creation
            try
            {
                _fileCreateWatcher = new FileSystemWatcher(_folder, _searchPattern);
                _fileCreateWatcher.Created += OnLogFileCreated;
                _fileCreateWatcher.Renamed += OnLogFileCreated;
                _fileCreateWatcher.EnableRaisingEvents = true;
            }
            catch
            {
                // Folder doesn't exist or no permissions
            }
        }
    }

    private void StartFileChangeMonitoring(string filePath)
    {
        _pollingTask = Task.Run(() => CheckFile(filePath));
    }

    private void OnLogFileCreated(object sender, FileSystemEventArgs e)
    {
        // Start monitoring changes
        StartFileChangeMonitoring(e.FullPath);

        // Stop file creation monitoring
        StopFileCreationMonitoring();

        // Trigger created event
        Created?.Invoke(this, new FileChangedEventArgs(e.FullPath));
    }

    private void CheckFile(string filePath)
    {
        while (!_isStopping)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var currentFileSize = fileInfo.Length;

                if (currentFileSize > _lastFileSize)
                {
                    _lastFileSize = currentFileSize;
                    Changed?.Invoke(this, new FileChangedEventArgs(filePath));
                }
            }
            catch
            {
                // File access error - exit loop
                return;
            }

            Thread.Sleep(_checkInterval);
        }
    }

    /// <summary>
    /// Stop watching
    /// </summary>
    public void Stop()
    {
        _isStopping = true;
        StopFileCreationMonitoring();
    }

    private void StopFileCreationMonitoring()
    {
        if (_fileCreateWatcher != null)
        {
            _fileCreateWatcher.Created -= OnLogFileCreated;
            _fileCreateWatcher.Renamed -= OnLogFileCreated;
            _fileCreateWatcher.Dispose();
            _fileCreateWatcher = null;
        }
    }

    private void Reset()
    {
        _isStopping = false;
        _lastFileSize = 0;
    }

    public void Dispose()
    {
        Stop();
    }
}

/// <summary>
/// Event args for file change events
/// </summary>
public class FileChangedEventArgs : EventArgs
{
    public string FullPath { get; }

    public FileChangedEventArgs(string fullPath)
    {
        FullPath = fullPath;
    }
}

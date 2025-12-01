using System.IO;
using System.Text.Json;
using TarkovHelper.Models;

namespace TarkovHelper.Services;

/// <summary>
/// Task/Item 데이터셋 관리 (로드/저장)
/// </summary>
public class TaskDatasetManager
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

    /// <summary>
    /// Task 데이터셋 기본 저장 경로
    /// </summary>
    public static string DefaultDataPath => Path.Combine(DataDirectory, "tasks.json");

    /// <summary>
    /// Item 데이터셋 기본 저장 경로
    /// </summary>
    public static string DefaultItemDataPath => Path.Combine(DataDirectory, "items.json");

    /// <summary>
    /// Hideout 데이터셋 기본 저장 경로
    /// </summary>
    public static string DefaultHideoutDataPath => Path.Combine(DataDirectory, "hideouts.json");

    /// <summary>
    /// API에서 Task 데이터를 받아와 JSON 파일로 저장합니다
    /// </summary>
    public static async Task<TaskDataset> FetchAndSaveAsync(string? filePath = null)
    {
        filePath ??= DefaultDataPath;
        EnsureDirectoryExists(filePath);

        using var apiService = new TarkovApiService();
        var dataset = await apiService.BuildTaskDatasetAsync();

        var json = JsonSerializer.Serialize(dataset, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);

        return dataset;
    }

    /// <summary>
    /// API에서 Item 데이터를 받아와 JSON 파일로 저장합니다
    /// </summary>
    public static async Task<ItemDataset> FetchAndSaveItemsAsync(string? filePath = null)
    {
        filePath ??= DefaultItemDataPath;
        EnsureDirectoryExists(filePath);

        using var apiService = new TarkovApiService();
        var dataset = await apiService.BuildItemDatasetAsync();

        var json = JsonSerializer.Serialize(dataset, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);

        return dataset;
    }

    /// <summary>
    /// API에서 Hideout 데이터를 받아와 JSON 파일로 저장합니다
    /// </summary>
    public static async Task<HideoutDataset> FetchAndSaveHideoutsAsync(string? filePath = null)
    {
        filePath ??= DefaultHideoutDataPath;
        EnsureDirectoryExists(filePath);

        using var apiService = new TarkovApiService();
        var dataset = await apiService.BuildHideoutDatasetAsync();

        var json = JsonSerializer.Serialize(dataset, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);

        return dataset;
    }

    /// <summary>
    /// API에서 모든 데이터(Task + Item + Hideout)를 받아와 저장합니다
    /// </summary>
    public static async Task<(TaskDataset Tasks, ItemDataset Items, HideoutDataset Hideouts)> FetchAndSaveAllAsync()
    {
        EnsureDirectoryExists(DefaultDataPath);

        using var apiService = new TarkovApiService();

        // 병렬로 데이터 가져오기
        var taskDatasetTask = apiService.BuildTaskDatasetAsync();
        var itemDatasetTask = apiService.BuildItemDatasetAsync();
        var hideoutDatasetTask = apiService.BuildHideoutDatasetAsync();

        await Task.WhenAll(taskDatasetTask, itemDatasetTask, hideoutDatasetTask);

        var taskDataset = taskDatasetTask.Result;
        var itemDataset = itemDatasetTask.Result;
        var hideoutDataset = hideoutDatasetTask.Result;

        // 저장
        var taskJson = JsonSerializer.Serialize(taskDataset, JsonOptions);
        var itemJson = JsonSerializer.Serialize(itemDataset, JsonOptions);
        var hideoutJson = JsonSerializer.Serialize(hideoutDataset, JsonOptions);

        await Task.WhenAll(
            File.WriteAllTextAsync(DefaultDataPath, taskJson),
            File.WriteAllTextAsync(DefaultItemDataPath, itemJson),
            File.WriteAllTextAsync(DefaultHideoutDataPath, hideoutJson)
        );

        return (taskDataset, itemDataset, hideoutDataset);
    }

    /// <summary>
    /// JSON 파일에서 Task 데이터셋을 로드합니다
    /// </summary>
    public static async Task<TaskDataset?> LoadAsync(string? filePath = null)
    {
        filePath ??= DefaultDataPath;

        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<TaskDataset>(json, JsonOptions);
    }

    /// <summary>
    /// JSON 파일에서 Item 데이터셋을 로드합니다
    /// </summary>
    public static async Task<ItemDataset?> LoadItemsAsync(string? filePath = null)
    {
        filePath ??= DefaultItemDataPath;

        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<ItemDataset>(json, JsonOptions);
    }

    /// <summary>
    /// JSON 파일에서 Hideout 데이터셋을 로드합니다
    /// </summary>
    public static async Task<HideoutDataset?> LoadHideoutsAsync(string? filePath = null)
    {
        filePath ??= DefaultHideoutDataPath;

        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<HideoutDataset>(json, JsonOptions);
    }

    /// <summary>
    /// 특정 퀘스트의 모든 선행 퀘스트 ID를 재귀적으로 가져옵니다
    /// </summary>
    public static HashSet<string> GetAllPrerequisites(TaskDataset dataset, string taskId)
    {
        var result = new HashSet<string>();
        var taskMap = dataset.Tasks.ToDictionary(t => t.Id);

        void CollectPrerequisites(string id)
        {
            if (!taskMap.TryGetValue(id, out var task)) return;

            foreach (var prereqId in task.PrerequisiteTaskIds)
            {
                if (result.Add(prereqId))
                {
                    CollectPrerequisites(prereqId);
                }
            }
        }

        CollectPrerequisites(taskId);
        return result;
    }

    /// <summary>
    /// 특정 퀘스트를 진행중으로 체크했을 때 자동 완료해야 할 선행 퀘스트 목록
    /// </summary>
    public static List<TaskData> GetTasksToAutoComplete(TaskDataset dataset, string taskId)
    {
        var prereqIds = GetAllPrerequisites(dataset, taskId);
        var taskMap = dataset.Tasks.ToDictionary(t => t.Id);

        return prereqIds
            .Where(id => taskMap.ContainsKey(id))
            .Select(id => taskMap[id])
            .ToList();
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}

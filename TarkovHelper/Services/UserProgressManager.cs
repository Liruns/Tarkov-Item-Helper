using System.IO;
using System.Text.Json;
using TarkovHelper.Models;

namespace TarkovHelper.Services;

/// <summary>
/// 사용자 진행 상황 관리 (저장/로드/계산)
/// </summary>
public class UserProgressManager
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

    public static string DefaultProgressPath => Path.Combine(DataDirectory, "progress.json");

    private UserProgress _progress = new();
    private TaskDataset? _taskDataset;
    private ItemDataset? _itemDataset;
    private HideoutDataset? _hideoutDataset;

    private Dictionary<string, TaskData> _taskMap = [];
    private Dictionary<string, ItemData> _itemMap = [];
    private Dictionary<string, HideoutData> _hideoutMap = [];
    private Dictionary<string, string> _alternativeIdMap = []; // Alternative ID -> Primary ID

    public UserProgress Progress => _progress;
    public TaskDataset? TaskDataset => _taskDataset;
    public ItemDataset? ItemDataset => _itemDataset;
    public HideoutDataset? HideoutDataset => _hideoutDataset;

    /// <summary>
    /// 모든 데이터 로드
    /// </summary>
    public async Task LoadAllAsync()
    {
        // 데이터셋 로드
        var taskTask = TaskDatasetManager.LoadAsync();
        var itemTask = TaskDatasetManager.LoadItemsAsync();
        var hideoutTask = TaskDatasetManager.LoadHideoutsAsync();
        var progressTask = LoadProgressAsync();

        await Task.WhenAll(taskTask, itemTask, hideoutTask, progressTask);

        _taskDataset = taskTask.Result;
        _itemDataset = itemTask.Result;
        _hideoutDataset = hideoutTask.Result;
        _progress = progressTask.Result ?? new UserProgress();

        // 데이터가 없으면 API에서 가져오기
        if (_taskDataset == null || _itemDataset == null || _hideoutDataset == null)
        {
            var (tasks, items, hideouts) = await TaskDatasetManager.FetchAndSaveAllAsync();
            _taskDataset = tasks;
            _itemDataset = items;
            _hideoutDataset = hideouts;
        }

        // 맵 생성
        _taskMap = _taskDataset.Tasks.ToDictionary(t => t.Id);
        _itemMap = _itemDataset.Items.ToDictionary(i => i.Id);
        _hideoutMap = _hideoutDataset.Hideouts.ToDictionary(h => h.Id);

        // Alternative ID 맵 생성 (중복 퀘스트 ID -> 원본 ID 매핑)
        _alternativeIdMap = [];
        foreach (var task in _taskDataset.Tasks)
        {
            foreach (var altId in task.AlternativeIds)
            {
                _alternativeIdMap[altId] = task.Id;
            }
        }
    }

    /// <summary>
    /// 진행 상황 저장
    /// </summary>
    public async Task SaveProgressAsync(string? filePath = null)
    {
        filePath ??= DefaultProgressPath;
        EnsureDirectoryExists(filePath);

        _progress.LastUpdated = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(_progress, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// 진행 상황 로드
    /// </summary>
    public static async Task<UserProgress?> LoadProgressAsync(string? filePath = null)
    {
        filePath ??= DefaultProgressPath;

        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<UserProgress>(json, JsonOptions);
    }

    /// <summary>
    /// 퀘스트 완료 처리 (선행 퀘스트 자동 완료 포함)
    /// </summary>
    public void CompleteQuest(string questId, bool autoCompletePrerequites = true)
    {
        if (_taskDataset == null) return;

        if (autoCompletePrerequites)
        {
            // 선행 퀘스트 모두 완료 처리
            var prereqs = TaskDatasetManager.GetAllPrerequisites(_taskDataset, questId);
            foreach (var prereqId in prereqs)
            {
                _progress.CompletedQuestIds.Add(prereqId);
                _progress.InProgressQuestIds.Remove(prereqId);
            }
        }

        _progress.CompletedQuestIds.Add(questId);
        _progress.InProgressQuestIds.Remove(questId);
    }

    /// <summary>
    /// 퀘스트 완료 취소
    /// </summary>
    public void UncompleteQuest(string questId)
    {
        _progress.CompletedQuestIds.Remove(questId);
    }

    /// <summary>
    /// 퀘스트 진행 중으로 설정
    /// </summary>
    public void SetQuestInProgress(string questId)
    {
        if (!_progress.CompletedQuestIds.Contains(questId))
        {
            _progress.InProgressQuestIds.Add(questId);
        }
    }

    /// <summary>
    /// 하이드아웃 레벨 설정
    /// </summary>
    public void SetHideoutLevel(string stationId, int level)
    {
        _progress.HideoutLevels[stationId] = level;
    }

    /// <summary>
    /// 하이드아웃 현재 레벨 조회
    /// </summary>
    public int GetHideoutLevel(string stationId)
    {
        return _progress.HideoutLevels.GetValueOrDefault(stationId, 0);
    }

    /// <summary>
    /// 퀘스트가 완료되었는지 확인
    /// </summary>
    public bool IsQuestCompleted(string questId)
    {
        return _progress.CompletedQuestIds.Contains(questId);
    }

    /// <summary>
    /// 퀘스트 진행 가능 여부 확인 (선행 퀘스트 완료 여부)
    /// </summary>
    public bool IsQuestAvailable(string questId)
    {
        if (!_taskMap.TryGetValue(questId, out var task)) return false;

        return task.PrerequisiteTaskIds.All(prereqId =>
            _progress.CompletedQuestIds.Contains(prereqId));
    }

    // 화폐 아이템 ID (필터링 대상)
    private static readonly HashSet<string> CurrencyItemIds =
    [
        "5449016a4bdc2d6f028b456f", // Roubles
        "5696686a4bdc2da3298b456a", // Dollars
        "569668774bdc2da2298b4568"  // Euros
    ];

    /// <summary>
    /// 미완료 퀘스트에서 필요한 아이템 집계
    /// giveItem만 카운트 (findItem은 제출이 아니므로 제외)
    /// </summary>
    public List<RequiredItemSummary> GetRequiredItemsForQuests()
    {
        if (_taskDataset == null || _itemDataset == null) return [];

        var itemRequirements = new Dictionary<string, RequiredItemSummary>();

        foreach (var task in _taskDataset.Tasks)
        {
            // 완료된 퀘스트는 스킵
            if (_progress.CompletedQuestIds.Contains(task.Id)) continue;

            foreach (var objective in task.Objectives)
            {
                // giveItem만 카운트 (findItem은 인벤토리 확인용, 실제 제출은 giveItem)
                // plantItem과 mark는 설치용이므로 포함
                if (objective.Type is not ("giveItem" or "plantItem" or "mark")) continue;

                foreach (var item in objective.Items)
                {
                    // 화폐 아이템 필터링
                    if (CurrencyItemIds.Contains(item.ItemId)) continue;

                    if (!itemRequirements.TryGetValue(item.ItemId, out var summary))
                    {
                        var itemData = _itemMap.GetValueOrDefault(item.ItemId);
                        summary = new RequiredItemSummary
                        {
                            ItemId = item.ItemId,
                            ItemNameEn = itemData?.NameEn ?? item.ItemId,
                            ItemNameKo = itemData?.NameKo ?? item.ItemId,
                            IconLink = itemData?.IconLink,
                            WikiLink = itemData?.WikiLink
                        };
                        itemRequirements[item.ItemId] = summary;
                    }

                    if (item.FoundInRaid)
                    {
                        summary.QuestFirCount += item.Count;
                    }
                    else
                    {
                        summary.QuestNormalCount += item.Count;
                    }

                    summary.QuestSources.Add($"{task.NameEn} ({task.TraderName})");
                    summary.QuestSourceDetails.Add(new ItemQuestSource
                    {
                        QuestId = task.Id,
                        QuestNameEn = task.NameEn,
                        QuestNameKo = task.NameKo,
                        TraderName = task.TraderName,
                        WikiLink = task.WikiLink,
                        Count = item.Count,
                        FoundInRaid = item.FoundInRaid
                    });
                }
            }
        }

        return itemRequirements.Values.ToList();
    }

    /// <summary>
    /// 미완료 하이드아웃 레벨에서 필요한 아이템 집계
    /// </summary>
    public List<RequiredItemSummary> GetRequiredItemsForHideout()
    {
        if (_hideoutDataset == null || _itemDataset == null) return [];

        var itemRequirements = new Dictionary<string, RequiredItemSummary>();

        foreach (var station in _hideoutDataset.Hideouts)
        {
            var currentLevel = GetHideoutLevel(station.Id);

            // 현재 레벨 이후의 모든 레벨에서 필요한 아이템 집계
            foreach (var level in station.Levels.Where(l => l.Level > currentLevel))
            {
                foreach (var req in level.ItemRequirements)
                {
                    // 화폐 아이템 필터링
                    if (CurrencyItemIds.Contains(req.ItemId)) continue;

                    if (!itemRequirements.TryGetValue(req.ItemId, out var summary))
                    {
                        var itemData = _itemMap.GetValueOrDefault(req.ItemId);
                        summary = new RequiredItemSummary
                        {
                            ItemId = req.ItemId,
                            ItemNameEn = itemData?.NameEn ?? req.ItemNameEn,
                            ItemNameKo = itemData?.NameKo ?? req.ItemNameKo,
                            IconLink = itemData?.IconLink,
                            WikiLink = itemData?.WikiLink
                        };
                        itemRequirements[req.ItemId] = summary;
                    }

                    if (req.FoundInRaid)
                    {
                        summary.HideoutFirCount += req.Count;
                    }
                    else
                    {
                        summary.HideoutNormalCount += req.Count;
                    }

                    summary.HideoutSources.Add($"{station.NameEn} Lv.{level.Level}");
                    summary.HideoutSourceDetails.Add(new ItemHideoutSource
                    {
                        StationId = station.Id,
                        StationNameEn = station.NameEn,
                        StationNameKo = station.NameKo,
                        Level = level.Level,
                        Count = req.Count,
                        FoundInRaid = req.FoundInRaid
                    });
                }
            }
        }

        return itemRequirements.Values.ToList();
    }

    /// <summary>
    /// 전체 필요 아이템 집계 (퀘스트 + 하이드아웃)
    /// </summary>
    public List<RequiredItemSummary> GetAllRequiredItems()
    {
        var questItems = GetRequiredItemsForQuests();
        var hideoutItems = GetRequiredItemsForHideout();

        // 병합
        var merged = new Dictionary<string, RequiredItemSummary>();

        foreach (var item in questItems)
        {
            merged[item.ItemId] = item;
        }

        foreach (var item in hideoutItems)
        {
            if (merged.TryGetValue(item.ItemId, out var existing))
            {
                existing.HideoutNormalCount = item.HideoutNormalCount;
                existing.HideoutFirCount = item.HideoutFirCount;
                existing.HideoutSources = item.HideoutSources;
                existing.HideoutSourceDetails = item.HideoutSourceDetails;
            }
            else
            {
                merged[item.ItemId] = item;
            }
        }

        // 보유 아이템 정보 적용
        foreach (var item in merged.Values)
        {
            item.OwnedNormalCount = _progress.OwnedItems.GetValueOrDefault(item.ItemId, 0);
            item.OwnedFirCount = _progress.OwnedFirItems.GetValueOrDefault(item.ItemId, 0);
        }

        return merged.Values
            .OrderByDescending(i => i.TotalCount)
            .ThenBy(i => i.ItemNameEn)
            .ToList();
    }

    /// <summary>
    /// 퀘스트 뷰모델 목록 생성
    /// </summary>
    public List<QuestViewModel> GetQuestViewModels(string? searchText = null)
    {
        if (_taskDataset == null) return [];

        var quests = _taskDataset.Tasks.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            quests = quests.Where(t =>
                t.NameEn.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                t.NameKo.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                t.TraderName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        return quests.Select(q => new QuestViewModel
        {
            Quest = q,
            IsCompleted = _progress.CompletedQuestIds.Contains(q.Id),
            IsInProgress = _progress.InProgressQuestIds.Contains(q.Id),
            IsAvailable = IsQuestAvailable(q.Id),
            RequiredItemCount = q.Objectives
                .Where(o => o.IsItemObjective)
                .SelectMany(o => o.Items)
                .Sum(i => i.Count)
        })
        .OrderBy(q => q.IsCompleted ? 1 : 0)
        .ThenBy(q => q.Quest.MinPlayerLevel ?? 0)
        .ThenBy(q => q.Quest.NameEn)
        .ToList();
    }

    /// <summary>
    /// 하이드아웃 스테이션 뷰모델 목록 생성
    /// </summary>
    public List<HideoutStationViewModel> GetHideoutViewModels()
    {
        if (_hideoutDataset == null) return [];

        return _hideoutDataset.Hideouts.Select(h =>
        {
            var currentLevel = GetHideoutLevel(h.Id);
            var maxLevel = h.Levels.Max(l => l.Level);
            var nextLevel = h.Levels.FirstOrDefault(l => l.Level == currentLevel + 1);

            return new HideoutStationViewModel
            {
                Station = h,
                CurrentLevel = currentLevel,
                MaxLevel = maxLevel,
                NextLevel = nextLevel
            };
        })
        .OrderBy(h => h.Station.NameEn)
        .ToList();
    }

    /// <summary>
    /// 아이템 조회
    /// </summary>
    public ItemData? GetItem(string itemId)
    {
        return _itemMap.GetValueOrDefault(itemId);
    }

    /// <summary>
    /// 퀘스트 조회 (Alternative ID도 체크)
    /// </summary>
    public TaskData? GetQuest(string questId)
    {
        // 먼저 직접 ID로 찾기
        if (_taskMap.TryGetValue(questId, out var task))
        {
            return task;
        }

        // Alternative ID로 찾기
        if (_alternativeIdMap.TryGetValue(questId, out var primaryId))
        {
            return _taskMap.GetValueOrDefault(primaryId);
        }

        return null;
    }

    /// <summary>
    /// Alternative ID를 Primary ID로 변환
    /// </summary>
    public string ResolvePrimaryQuestId(string questId)
    {
        if (_taskMap.ContainsKey(questId))
        {
            return questId;
        }

        if (_alternativeIdMap.TryGetValue(questId, out var primaryId))
        {
            return primaryId;
        }

        return questId;
    }

    /// <summary>
    /// 게임 로그에서 퀘스트 완료 감지 시 호출
    /// Alternative ID도 처리
    /// </summary>
    public bool TryCompleteQuestFromLog(string questId)
    {
        var primaryId = ResolvePrimaryQuestId(questId);
        var quest = GetQuest(primaryId);

        if (quest == null)
        {
            return false; // 알 수 없는 퀘스트
        }

        if (_progress.CompletedQuestIds.Contains(primaryId))
        {
            return false; // 이미 완료됨
        }

        // 선행 퀘스트 포함 완료 처리
        CompleteQuest(primaryId, autoCompletePrerequites: true);
        return true;
    }

    /// <summary>
    /// 진행 상황 초기화
    /// </summary>
    public void ResetProgress()
    {
        _progress = new UserProgress();
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

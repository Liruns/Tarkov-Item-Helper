using System.Text.Json.Serialization;

namespace TarkovHelper.Models;

/// <summary>
/// 사용자 진행 상황 데이터
/// </summary>
public class UserProgress
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 완료된 퀘스트 ID 목록
    /// </summary>
    [JsonPropertyName("completedQuestIds")]
    public HashSet<string> CompletedQuestIds { get; set; } = [];

    /// <summary>
    /// 진행 중인 퀘스트 ID 목록
    /// </summary>
    [JsonPropertyName("inProgressQuestIds")]
    public HashSet<string> InProgressQuestIds { get; set; } = [];

    /// <summary>
    /// 하이드아웃 스테이션별 현재 레벨 (StationId -> Level)
    /// Level 0 = 미건설, Level 1 = 1레벨 완료, etc.
    /// </summary>
    [JsonPropertyName("hideoutLevels")]
    public Dictionary<string, int> HideoutLevels { get; set; } = [];

    /// <summary>
    /// 보유 중인 아이템 수량 (ItemId -> Count)
    /// </summary>
    [JsonPropertyName("ownedItems")]
    public Dictionary<string, int> OwnedItems { get; set; } = [];

    /// <summary>
    /// 보유 중인 FIR 아이템 수량 (ItemId -> Count)
    /// </summary>
    [JsonPropertyName("ownedFirItems")]
    public Dictionary<string, int> OwnedFirItems { get; set; } = [];
}

/// <summary>
/// 필요 아이템 집계 결과
/// </summary>
public class RequiredItemSummary
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemNameEn { get; set; } = string.Empty;
    public string ItemNameKo { get; set; } = string.Empty;
    public string ItemNameJa { get; set; } = string.Empty;
    public string? IconLink { get; set; }
    public string? WikiLink { get; set; }

    /// <summary>
    /// 퀘스트에서 필요한 일반 아이템 수량
    /// </summary>
    public int QuestNormalCount { get; set; }

    /// <summary>
    /// 퀘스트에서 필요한 FIR 아이템 수량
    /// </summary>
    public int QuestFirCount { get; set; }

    /// <summary>
    /// 하이드아웃에서 필요한 일반 아이템 수량
    /// </summary>
    public int HideoutNormalCount { get; set; }

    /// <summary>
    /// 하이드아웃에서 필요한 FIR 아이템 수량
    /// </summary>
    public int HideoutFirCount { get; set; }

    /// <summary>
    /// 총 필요 일반 아이템 수량
    /// </summary>
    public int TotalNormalCount => QuestNormalCount + HideoutNormalCount;

    /// <summary>
    /// 총 필요 FIR 아이템 수량
    /// </summary>
    public int TotalFirCount => QuestFirCount + HideoutFirCount;

    /// <summary>
    /// 전체 필요 수량 (일반 + FIR)
    /// </summary>
    public int TotalCount => TotalNormalCount + TotalFirCount;

    /// <summary>
    /// 보유 중인 일반 아이템 수량
    /// </summary>
    public int OwnedNormalCount { get; set; }

    /// <summary>
    /// 보유 중인 FIR 아이템 수량
    /// </summary>
    public int OwnedFirCount { get; set; }

    /// <summary>
    /// 추가로 필요한 일반 아이템 수량
    /// </summary>
    public int NeededNormalCount => Math.Max(0, TotalNormalCount - OwnedNormalCount);

    /// <summary>
    /// 추가로 필요한 FIR 아이템 수량
    /// </summary>
    public int NeededFirCount => Math.Max(0, TotalFirCount - OwnedFirCount);

    /// <summary>
    /// FIR 아이템이 필요한지 여부
    /// </summary>
    public bool RequiresFir => TotalFirCount > 0;

    /// <summary>
    /// 퀘스트 출처 목록 (문자열, 하위 호환)
    /// </summary>
    public List<string> QuestSources { get; set; } = [];

    /// <summary>
    /// 하이드아웃 출처 목록
    /// </summary>
    public List<string> HideoutSources { get; set; } = [];

    /// <summary>
    /// 퀘스트 출처 상세 정보 (위키 링크 포함)
    /// </summary>
    public List<ItemQuestSource> QuestSourceDetails { get; set; } = [];

    /// <summary>
    /// 하이드아웃 출처 상세 정보
    /// </summary>
    public List<ItemHideoutSource> HideoutSourceDetails { get; set; } = [];
}

/// <summary>
/// 퀘스트 표시용 뷰모델
/// </summary>
public class QuestViewModel
{
    public TaskData Quest { get; set; } = null!;
    public bool IsCompleted { get; set; }
    public bool IsInProgress { get; set; }
    public bool IsAvailable { get; set; } // 선행 퀘스트가 모두 완료된 경우
    public int RequiredItemCount { get; set; }
}

/// <summary>
/// 하이드아웃 스테이션 표시용 뷰모델
/// </summary>
public class HideoutStationViewModel
{
    public HideoutData Station { get; set; } = null!;
    public int CurrentLevel { get; set; }
    public int MaxLevel { get; set; }
    public HideoutLevel? NextLevel { get; set; }
    public bool IsMaxLevel => CurrentLevel >= MaxLevel;
}

/// <summary>
/// 아이템을 필요로 하는 퀘스트 정보
/// </summary>
public class ItemQuestSource
{
    public string QuestId { get; set; } = string.Empty;
    public string QuestNameEn { get; set; } = string.Empty;
    public string QuestNameKo { get; set; } = string.Empty;
    public string QuestNameJa { get; set; } = string.Empty;
    public string TraderName { get; set; } = string.Empty;
    public string? WikiLink { get; set; }
    public int Count { get; set; }
    public bool FoundInRaid { get; set; }
}

/// <summary>
/// 아이템을 필요로 하는 하이드아웃 정보
/// </summary>
public class ItemHideoutSource
{
    public string StationId { get; set; } = string.Empty;
    public string StationNameEn { get; set; } = string.Empty;
    public string StationNameKo { get; set; } = string.Empty;
    public string StationNameJa { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Count { get; set; }
    public bool FoundInRaid { get; set; }
}

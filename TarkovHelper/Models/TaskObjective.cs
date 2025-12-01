using System.Text.Json.Serialization;

namespace TarkovHelper.Models;

/// <summary>
/// 퀘스트 목표 타입
/// </summary>
public enum ObjectiveType
{
    Unknown,
    GiveItem,       // 아이템 제출
    FindItem,       // 아이템 찾기
    Mark,           // 마커 설치
    Kill,           // 처치
    Visit,          // 방문
    Extract,        // 탈출
    Skill,          // 스킬 레벨
    TraderLevel,    // 트레이더 레벨
    BuildWeapon,    // 무기 조립
    Shoot,          // 사격
    PlantItem,      // 아이템 설치
    Experience,     // 경험치
    TaskStatus,     // 다른 퀘스트 상태
    PlayerLevel,    // 플레이어 레벨
    TraderStanding, // 트레이더 호감도
    UseItem,        // 아이템 사용
    QuestItem       // 퀘스트 아이템
}

/// <summary>
/// 퀘스트 목표에서 필요한 아이템 정보
/// </summary>
public class ObjectiveItem
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("foundInRaid")]
    public bool FoundInRaid { get; set; }
}

/// <summary>
/// 퀘스트 목표
/// </summary>
public class TaskObjective
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("optional")]
    public bool Optional { get; set; }

    [JsonPropertyName("maps")]
    public List<string> Maps { get; set; } = [];

    /// <summary>
    /// 아이템 제출/찾기 목표인 경우 필요한 아이템 목록
    /// </summary>
    [JsonPropertyName("items")]
    public List<ObjectiveItem> Items { get; set; } = [];

    /// <summary>
    /// 아이템 목표 타입인지 확인
    /// </summary>
    [JsonIgnore]
    public bool IsItemObjective => Type is "giveItem" or "findItem" or "plantItem" or "mark";

    /// <summary>
    /// 문자열 타입을 ObjectiveType enum으로 변환
    /// </summary>
    [JsonIgnore]
    public ObjectiveType ObjectiveType => Type.ToLowerInvariant() switch
    {
        "giveitem" => ObjectiveType.GiveItem,
        "finditem" => ObjectiveType.FindItem,
        "mark" => ObjectiveType.Mark,
        "kill" => ObjectiveType.Kill,
        "visit" => ObjectiveType.Visit,
        "extract" => ObjectiveType.Extract,
        "skill" => ObjectiveType.Skill,
        "traderlevel" => ObjectiveType.TraderLevel,
        "buildweapon" => ObjectiveType.BuildWeapon,
        "shoot" => ObjectiveType.Shoot,
        "plantitem" => ObjectiveType.PlantItem,
        "experience" => ObjectiveType.Experience,
        "taskstatus" => ObjectiveType.TaskStatus,
        "playerlevel" => ObjectiveType.PlayerLevel,
        "traderstanding" => ObjectiveType.TraderStanding,
        "useitem" => ObjectiveType.UseItem,
        "questitem" => ObjectiveType.QuestItem,
        _ => ObjectiveType.Unknown
    };
}

using System.Text.Json.Serialization;

namespace TarkovHelper.Models.GraphQL;

/// <summary>
/// GraphQL 응답 래퍼
/// </summary>
public class GraphQLResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<GraphQLError>? Errors { get; set; }
}

public class GraphQLError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// tasks 쿼리 응답
/// </summary>
public class TasksQueryResponse
{
    [JsonPropertyName("tasks")]
    public List<ApiTask> Tasks { get; set; } = [];
}

/// <summary>
/// API에서 받는 Task 데이터
/// </summary>
public class ApiTask
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("normalizedName")]
    public string NormalizedName { get; set; } = string.Empty;

    [JsonPropertyName("trader")]
    public ApiTrader? Trader { get; set; }

    [JsonPropertyName("minPlayerLevel")]
    public int? MinPlayerLevel { get; set; }

    [JsonPropertyName("experience")]
    public int Experience { get; set; }

    [JsonPropertyName("kappaRequired")]
    public bool? KappaRequired { get; set; }

    [JsonPropertyName("lightkeeperRequired")]
    public bool? LightkeeperRequired { get; set; }

    [JsonPropertyName("wikiLink")]
    public string? WikiLink { get; set; }

    [JsonPropertyName("taskRequirements")]
    public List<ApiTaskRequirement> TaskRequirements { get; set; } = [];

    [JsonPropertyName("objectives")]
    public List<ApiTaskObjective> Objectives { get; set; } = [];
}

public class ApiTrader
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class ApiTaskRequirement
{
    [JsonPropertyName("task")]
    public ApiTaskReference? Task { get; set; }

    [JsonPropertyName("status")]
    public List<string> Status { get; set; } = [];
}

public class ApiTaskReference
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 퀘스트 목표 (objectives)
/// </summary>
public class ApiTaskObjective
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
    public List<ApiMap>? Maps { get; set; }

    // TaskObjectiveItem 필드들 (inline fragment로 받음)
    [JsonPropertyName("items")]
    public List<ApiItemReference>? Items { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("foundInRaid")]
    public bool? FoundInRaid { get; set; }
}

public class ApiMap
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("normalizedName")]
    public string NormalizedName { get; set; } = string.Empty;
}

public class ApiItemReference
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("shortName")]
    public string ShortName { get; set; } = string.Empty;
}

/// <summary>
/// items 쿼리 응답
/// </summary>
public class ItemsQueryResponse
{
    [JsonPropertyName("items")]
    public List<ApiItem> Items { get; set; } = [];
}

/// <summary>
/// API에서 받는 Item 데이터
/// </summary>
public class ApiItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("shortName")]
    public string? ShortName { get; set; }

    [JsonPropertyName("normalizedName")]
    public string? NormalizedName { get; set; }

    [JsonPropertyName("wikiLink")]
    public string? WikiLink { get; set; }

    [JsonPropertyName("iconLink")]
    public string? IconLink { get; set; }

    [JsonPropertyName("gridImageLink")]
    public string? GridImageLink { get; set; }

    [JsonPropertyName("basePrice")]
    public int BasePrice { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("types")]
    public List<string> Types { get; set; } = [];

    [JsonPropertyName("category")]
    public ApiItemCategory? Category { get; set; }
}

public class ApiItemCategory
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// hideoutStations 쿼리 응답
/// </summary>
public class HideoutStationsQueryResponse
{
    [JsonPropertyName("hideoutStations")]
    public List<ApiHideoutStation> HideoutStations { get; set; } = [];
}

/// <summary>
/// API에서 받는 HideoutStation 데이터
/// </summary>
public class ApiHideoutStation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("normalizedName")]
    public string NormalizedName { get; set; } = string.Empty;

    [JsonPropertyName("imageLink")]
    public string? ImageLink { get; set; }

    [JsonPropertyName("levels")]
    public List<ApiHideoutStationLevel> Levels { get; set; } = [];
}

/// <summary>
/// HideoutStation의 레벨 데이터
/// </summary>
public class ApiHideoutStationLevel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("constructionTime")]
    public int ConstructionTime { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("itemRequirements")]
    public List<ApiRequirementItem> ItemRequirements { get; set; } = [];

    [JsonPropertyName("stationLevelRequirements")]
    public List<ApiRequirementHideoutStationLevel> StationLevelRequirements { get; set; } = [];

    [JsonPropertyName("traderRequirements")]
    public List<ApiRequirementTrader> TraderRequirements { get; set; } = [];

    [JsonPropertyName("skillRequirements")]
    public List<ApiRequirementSkill> SkillRequirements { get; set; } = [];
}

/// <summary>
/// 아이템 요구사항
/// </summary>
public class ApiRequirementItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("item")]
    public ApiItemReference Item { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("attributes")]
    public List<ApiItemAttribute>? Attributes { get; set; }
}

/// <summary>
/// 아이템 속성 (foundInRaid 등)
/// </summary>
public class ApiItemAttribute
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

/// <summary>
/// 은신처 스테이션 레벨 요구사항
/// </summary>
public class ApiRequirementHideoutStationLevel
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("station")]
    public ApiHideoutStationRef Station { get; set; } = new();

    [JsonPropertyName("level")]
    public int Level { get; set; }
}

/// <summary>
/// 은신처 스테이션 참조 (요구사항에서 사용)
/// </summary>
public class ApiHideoutStationRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 트레이더 요구사항
/// </summary>
public class ApiRequirementTrader
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("trader")]
    public ApiTrader Trader { get; set; } = new();

    [JsonPropertyName("level")]
    public int? Level { get; set; }

    [JsonPropertyName("value")]
    public int? Value { get; set; }
}

/// <summary>
/// 스킬 요구사항
/// </summary>
public class ApiRequirementSkill
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }
}

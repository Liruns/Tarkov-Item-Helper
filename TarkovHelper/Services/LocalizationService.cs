using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace TarkovHelper.Services;

/// <summary>
/// Supported languages
/// </summary>
public enum AppLanguage
{
    EN,
    KO
}

/// <summary>
/// Centralized localization service for managing UI language
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string DataDirectory => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "Data"
    );

    private static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    private AppLanguage _currentLanguage = AppLanguage.EN;

    public LocalizationService()
    {
        // 저장된 설정 로드
        LoadSettings();
    }

    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                OnPropertyChanged(nameof(CurrentLanguage));
                OnPropertyChanged(nameof(IsEnglish));
                OnPropertyChanged(nameof(IsKorean));
                LanguageChanged?.Invoke(this, value);
                SaveSettings(); // 언어 변경 시 저장
            }
        }
    }

    public bool IsEnglish => CurrentLanguage == AppLanguage.EN;
    public bool IsKorean => CurrentLanguage == AppLanguage.KO;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<AppLanguage>? LanguageChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #region Settings Persistence

    private void SaveSettings()
    {
        try
        {
            if (!Directory.Exists(DataDirectory))
            {
                Directory.CreateDirectory(DataDirectory);
            }

            var settings = new AppSettings { Language = _currentLanguage.ToString() };
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // 저장 실패 시 무시
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null && Enum.TryParse<AppLanguage>(settings.Language, out var lang))
                {
                    _currentLanguage = lang;
                }
            }
        }
        catch
        {
            // 로드 실패 시 기본값 (EN) 사용
            _currentLanguage = AppLanguage.EN;
        }
    }

    private class AppSettings
    {
        public string Language { get; set; } = "EN";
    }

    #endregion

    /// <summary>
    /// Get display name based on current language (primary name with secondary underneath for KO)
    /// </summary>
    public string GetDisplayName(string nameEn, string nameKo)
    {
        return CurrentLanguage == AppLanguage.EN ? nameEn : nameKo;
    }

    /// <summary>
    /// Get secondary display name (shown smaller, below primary)
    /// Only shows for KO mode - EN mode shows empty string
    /// </summary>
    public string GetSecondaryName(string nameEn, string nameKo)
    {
        // EN 모드에서는 보조 이름 없음, KO 모드에서만 영어 이름 표시
        return CurrentLanguage == AppLanguage.EN ? string.Empty : nameEn;
    }

    /// <summary>
    /// Check if secondary name should be visible (only in KO mode)
    /// </summary>
    public bool ShowSecondaryName => CurrentLanguage == AppLanguage.KO;

    #region UI String Resources

    // Header
    public string AppSubtitle => CurrentLanguage == AppLanguage.EN
        ? "Quest & Hideout Tracker"
        : "퀘스트 & 은신처 트래커";

    public string RefreshData => CurrentLanguage == AppLanguage.EN
        ? "Refresh Data"
        : "데이터 새로고침";

    public string ResetProgress => CurrentLanguage == AppLanguage.EN
        ? "Reset Progress"
        : "진행 초기화";

    // Tab Headers
    public string TabQuests => CurrentLanguage == AppLanguage.EN
        ? "QUESTS"
        : "퀘스트";

    public string TabHideout => CurrentLanguage == AppLanguage.EN
        ? "HIDEOUT"
        : "은신처";

    public string TabRequiredItems => CurrentLanguage == AppLanguage.EN
        ? "REQUIRED ITEMS"
        : "필요 아이템";

    // Quest Tab
    public string SearchQuestsPlaceholder => CurrentLanguage == AppLanguage.EN
        ? "Search quests (EN/KO/Trader)..."
        : "퀘스트 검색 (영어/한국어/상인)...";

    public string HideCompleted => CurrentLanguage == AppLanguage.EN
        ? "Hide Completed"
        : "완료 숨기기";

    public string SearchAndComplete => CurrentLanguage == AppLanguage.EN
        ? "Quick Start Quest"
        : "퀘스트 빠른 시작";

    public string SearchAndCompleteDesc => CurrentLanguage == AppLanguage.EN
        ? "Search for a quest, auto-complete all prerequisites, and set it as in progress"
        : "퀘스트를 검색하여 모든 선행 퀘스트를 자동 완료하고 해당 퀘스트를 진행중으로 설정합니다";

    public string SearchQuest => CurrentLanguage == AppLanguage.EN
        ? "Find Quest"
        : "퀘스트 찾기";

    public string SelectQuest => CurrentLanguage == AppLanguage.EN
        ? "Select a Quest"
        : "퀘스트 선택";

    public string Available => CurrentLanguage == AppLanguage.EN
        ? "Available"
        : "진행 가능";

    public string Items => CurrentLanguage == AppLanguage.EN
        ? "items"
        : "아이템";

    public string Prerequisites => CurrentLanguage == AppLanguage.EN
        ? "Prerequisites"
        : "선행 퀘스트";

    public string Objectives => CurrentLanguage == AppLanguage.EN
        ? "Objectives"
        : "목표";

    public string Level => CurrentLanguage == AppLanguage.EN
        ? "Level"
        : "레벨";

    // Hideout Tab
    public string SelectStation => CurrentLanguage == AppLanguage.EN
        ? "Select a Station"
        : "시설 선택";

    public string CurrentLevel => CurrentLanguage == AppLanguage.EN
        ? "Current Level:"
        : "현재 레벨:";

    public string NextLevelRequirements => CurrentLanguage == AppLanguage.EN
        ? "Next Level Requirements"
        : "다음 레벨 요구사항";

    public string MaxLevelReached => CurrentLanguage == AppLanguage.EN
        ? "Max Level Reached"
        : "최대 레벨 도달";

    public string StationMaxUpgraded => CurrentLanguage == AppLanguage.EN
        ? "This station is fully upgraded!"
        : "이 시설은 최대 업그레이드 되었습니다!";

    public string RequiredStations => CurrentLanguage == AppLanguage.EN
        ? "Required Stations"
        : "필요 시설";

    public string RequiredTraders => CurrentLanguage == AppLanguage.EN
        ? "Required Traders"
        : "필요 상인";

    public string RequiredItems => CurrentLanguage == AppLanguage.EN
        ? "Required Items"
        : "필요 아이템";

    // Required Items Tab
    public string TotalItems => CurrentLanguage == AppLanguage.EN
        ? "Total Items"
        : "전체 아이템";

    public string QuestItems => CurrentLanguage == AppLanguage.EN
        ? "Quest Items"
        : "퀘스트 아이템";

    public string HideoutItems => CurrentLanguage == AppLanguage.EN
        ? "Hideout Items"
        : "은신처 아이템";

    public string FirRequired => CurrentLanguage == AppLanguage.EN
        ? "FIR Required"
        : "FIR 필요";

    public string SearchItemsPlaceholder => CurrentLanguage == AppLanguage.EN
        ? "Search items..."
        : "아이템 검색...";

    public string FirOnly => CurrentLanguage == AppLanguage.EN
        ? "FIR Only"
        : "FIR만";

    public string QuestItemsFilter => CurrentLanguage == AppLanguage.EN
        ? "Quest Items"
        : "퀘스트 아이템";

    public string HideoutItemsFilter => CurrentLanguage == AppLanguage.EN
        ? "Hideout Items"
        : "은신처 아이템";

    public string SelectItem => CurrentLanguage == AppLanguage.EN
        ? "Select an Item"
        : "아이템 선택";

    public string Quest => CurrentLanguage == AppLanguage.EN
        ? "Quest"
        : "퀘스트";

    public string Hideout => CurrentLanguage == AppLanguage.EN
        ? "Hideout"
        : "은신처";

    public string Total => CurrentLanguage == AppLanguage.EN
        ? "Total"
        : "합계";

    public string Quests => CurrentLanguage == AppLanguage.EN
        ? "Quests"
        : "퀘스트";

    // Status messages
    public string LoadingData => CurrentLanguage == AppLanguage.EN
        ? "Loading data..."
        : "데이터 로딩 중...";

    public string DataLoadedSuccessfully => CurrentLanguage == AppLanguage.EN
        ? "Data loaded successfully"
        : "데이터 로드 완료";

    public string FailedToLoadData => CurrentLanguage == AppLanguage.EN
        ? "Failed to load data"
        : "데이터 로드 실패";

    public string FetchingDataFromApi => CurrentLanguage == AppLanguage.EN
        ? "Fetching data from API..."
        : "API에서 데이터 가져오는 중...";

    public string DataRefreshedSuccessfully => CurrentLanguage == AppLanguage.EN
        ? "Data refreshed successfully"
        : "데이터 새로고침 완료";

    public string FailedToRefreshData => CurrentLanguage == AppLanguage.EN
        ? "Failed to refresh data"
        : "데이터 새로고침 실패";

    public string ProgressReset => CurrentLanguage == AppLanguage.EN
        ? "Progress reset"
        : "진행 초기화됨";

    // Dialogs
    public string SearchQuestTitle => CurrentLanguage == AppLanguage.EN
        ? "Search Quest"
        : "퀘스트 검색";

    public string EnterQuestName => CurrentLanguage == AppLanguage.EN
        ? "Enter quest name (EN/KO)..."
        : "퀘스트 이름 입력 (영어/한국어)...";

    public string SetInProgress => CurrentLanguage == AppLanguage.EN
        ? "Start Quest"
        : "퀘스트 시작";

    public string Confirm => CurrentLanguage == AppLanguage.EN
        ? "Confirm"
        : "확인";

    public string Error => CurrentLanguage == AppLanguage.EN
        ? "Error"
        : "오류";

    public string RefreshDataConfirm => CurrentLanguage == AppLanguage.EN
        ? "This will fetch fresh data from the API. Continue?"
        : "API에서 새 데이터를 가져옵니다. 계속하시겠습니까?";

    public string ResetProgressConfirm => CurrentLanguage == AppLanguage.EN
        ? "This will reset all your progress. Are you sure?"
        : "모든 진행 상황이 초기화됩니다. 계속하시겠습니까?";

    // Format strings
    public string FormatQuestProgress(int completed, int total) => CurrentLanguage == AppLanguage.EN
        ? $"Quests: {completed}/{total}"
        : $"퀘스트: {completed}/{total}";

    public string FormatHideoutProgress(int current, int total) => CurrentLanguage == AppLanguage.EN
        ? $"Hideout: {current}/{total}"
        : $"은신처: {current}/{total}";

    public string FormatLevelRequirement(int level) => CurrentLanguage == AppLanguage.EN
        ? $"Level {level} Requirements"
        : $"레벨 {level} 요구사항";

    public string FormatInProgressStatus(string questName, int prereqCount) => CurrentLanguage == AppLanguage.EN
        ? $"Started: {questName} ({prereqCount} prerequisites completed)"
        : $"시작됨: {questName} (선행 퀘스트 {prereqCount}개 완료)";

    public string FormatItemCount(int count) => CurrentLanguage == AppLanguage.EN
        ? $"{count} items"
        : $"{count}개 아이템";

    public string FormatTotalDetails(int total, int quest, int hideout) => CurrentLanguage == AppLanguage.EN
        ? $"Total: {total} (Quest: {quest}, Hideout: {hideout})"
        : $"합계: {total} (퀘스트: {quest}, 은신처: {hideout})";

    public string FormatSetInProgressConfirm(string nameEn, string nameKo) => CurrentLanguage == AppLanguage.EN
        ? $"Start '{nameEn}'?"
        : $"'{nameKo}'를 시작하시겠습니까?";

    public string FormatPrerequisiteCompleteCount(int count) => CurrentLanguage == AppLanguage.EN
        ? $"The following {count} prerequisite quest(s) will be marked as completed:"
        : $"다음 선행 퀘스트 {count}개가 완료 처리됩니다:";

    public string FormatAndMore(int count) => CurrentLanguage == AppLanguage.EN
        ? $"  ... and {count} more"
        : $"  ... 외 {count}개";

    public string FormatLevelDisplay(int level) => CurrentLanguage == AppLanguage.EN
        ? $"Lv.{level}"
        : $"레벨 {level}";

    public string FormatHideoutLevelDisplay(string stationNameKo, int level) =>
        $"{stationNameKo} 레벨 {level}";

    // Log Monitoring
    public string LogMonitoring => CurrentLanguage == AppLanguage.EN
        ? "Log Monitoring"
        : "로그 모니터링";

    public string LogPathSettings => CurrentLanguage == AppLanguage.EN
        ? "Log Path Settings"
        : "로그 경로 설정";

    public string GameFolder => CurrentLanguage == AppLanguage.EN
        ? "Game Folder"
        : "게임 폴더";

    public string LogsFolder => CurrentLanguage == AppLanguage.EN
        ? "Logs Folder"
        : "로그 폴더";

    public string Browse => CurrentLanguage == AppLanguage.EN
        ? "Browse"
        : "찾아보기";

    public string AutoDetect => CurrentLanguage == AppLanguage.EN
        ? "Auto-Detect"
        : "자동 감지";

    public string LogStatusMonitoring => CurrentLanguage == AppLanguage.EN
        ? "Monitoring"
        : "모니터링 중";

    public string LogStatusNotStarted => CurrentLanguage == AppLanguage.EN
        ? "Not Started"
        : "시작 안됨";

    public string LogStatusError => CurrentLanguage == AppLanguage.EN
        ? "Error"
        : "오류";

    public string LogStatusTooltipMonitoring => CurrentLanguage == AppLanguage.EN
        ? "Log monitoring active - Quest completions will be detected automatically"
        : "로그 모니터링 활성 - 퀘스트 완료가 자동으로 감지됩니다";

    public string LogStatusTooltipNotStarted => CurrentLanguage == AppLanguage.EN
        ? "Log monitoring not started - Click to configure"
        : "로그 모니터링 시작 안됨 - 클릭하여 설정";

    public string LogStatusTooltipError => CurrentLanguage == AppLanguage.EN
        ? "Log monitoring error - Click to configure"
        : "로그 모니터링 오류 - 클릭하여 설정";

    public string StartMonitoring => CurrentLanguage == AppLanguage.EN
        ? "Start Monitoring"
        : "모니터링 시작";

    public string StopMonitoring => CurrentLanguage == AppLanguage.EN
        ? "Stop Monitoring"
        : "모니터링 중지";

    public string QuestCompletedFromLog => CurrentLanguage == AppLanguage.EN
        ? "Quest completed (detected from game log)"
        : "퀘스트 완료 (게임 로그에서 감지)";

    public string FormatQuestCompletedFromLog(string questName) => CurrentLanguage == AppLanguage.EN
        ? $"'{questName}' completed (detected from game log)"
        : $"'{questName}' 완료 (게임 로그에서 감지)";

    public string SelectGameFolder => CurrentLanguage == AppLanguage.EN
        ? "Select Tarkov game folder"
        : "타르코프 게임 폴더 선택";

    public string InvalidGameFolder => CurrentLanguage == AppLanguage.EN
        ? "Selected folder doesn't appear to be a valid Tarkov installation"
        : "선택한 폴더가 유효한 타르코프 설치 폴더가 아닙니다";

    public string Save => CurrentLanguage == AppLanguage.EN
        ? "Save"
        : "저장";

    public string Cancel => CurrentLanguage == AppLanguage.EN
        ? "Cancel"
        : "취소";

    public string Close => CurrentLanguage == AppLanguage.EN
        ? "Close"
        : "닫기";

    public string Settings => CurrentLanguage == AppLanguage.EN
        ? "Settings"
        : "설정";

    // Game Path Detection
    public string GamePathNotFoundTitle => CurrentLanguage == AppLanguage.EN
        ? "Game Path Not Found"
        : "게임 경로를 찾을 수 없음";

    public string GamePathNotFoundMessage => CurrentLanguage == AppLanguage.EN
        ? "Escape from Tarkov installation could not be detected automatically.\n\nWould you like to select the game folder manually?\n\n(This is required for automatic quest completion tracking)"
        : "Escape from Tarkov 설치 경로를 자동으로 찾을 수 없습니다.\n\n게임 폴더를 직접 선택하시겠습니까?\n\n(퀘스트 자동 완료 추적을 위해 필요합니다)";

    public string InvalidGameFolderRetry => CurrentLanguage == AppLanguage.EN
        ? "The selected folder does not appear to be a valid Tarkov installation.\n\nWould you like to try again?"
        : "선택한 폴더가 유효한 타르코프 설치 폴더가 아닌 것 같습니다.\n\n다시 시도하시겠습니까?";

    public string LogMonitoringDisabled => CurrentLanguage == AppLanguage.EN
        ? "Log monitoring disabled - Configure in Settings"
        : "로그 모니터링 비활성화됨 - 설정에서 구성하세요";

    public string AutoDetectFailed => CurrentLanguage == AppLanguage.EN
        ? "Could not automatically detect Tarkov installation.\n\nPlease select the game folder manually using the Browse button."
        : "타르코프 설치 경로를 자동으로 찾을 수 없습니다.\n\n찾아보기 버튼을 사용하여 게임 폴더를 직접 선택해주세요.";

    public string FormatGamePathDetected(string method, string path) => CurrentLanguage == AppLanguage.EN
        ? $"Game path detected via {method}: {path}"
        : $"{method}에서 게임 경로 감지됨: {path}";

    public string FormatGamePathSet(string path) => CurrentLanguage == AppLanguage.EN
        ? $"Game path set: {path}"
        : $"게임 경로 설정됨: {path}";

    public string FormatAutoDetectSuccess(string method, string path) => CurrentLanguage == AppLanguage.EN
        ? $"Game folder detected!\n\nMethod: {method}\nPath: {path}"
        : $"게임 폴더를 찾았습니다!\n\n방법: {method}\n경로: {path}";

    #endregion
}

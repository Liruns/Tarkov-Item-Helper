using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TarkovHelper.Models;
using TarkovHelper.Services;

namespace TarkovHelper.Pages
{
    /// <summary>
    /// Quest list view model for display
    /// </summary>
    public class QuestViewModel
    {
        public TarkovTask Task { get; set; } = null!;
        public string DisplayName { get; set; } = string.Empty;
        public string SubtitleName { get; set; } = string.Empty;
        public Visibility SubtitleVisibility { get; set; } = Visibility.Collapsed;
        public string TraderInitial { get; set; } = string.Empty;
        public QuestStatus Status { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public Brush StatusBackground { get; set; } = Brushes.Gray;
        public Visibility CompleteButtonVisibility { get; set; } = Visibility.Visible;
        public bool IsKappaRequired { get; set; }
        public Visibility KappaBadgeVisibility => IsKappaRequired ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Required item view model
    /// </summary>
    public class RequiredItemViewModel
    {
        public string DisplayText { get; set; } = string.Empty;
        public bool FoundInRaid { get; set; }
        public Visibility FirVisibility => FoundInRaid ? Visibility.Visible : Visibility.Collapsed;
        public BitmapImage? IconSource { get; set; }
        public string RequirementType { get; set; } = string.Empty;
        public Visibility RequirementTypeVisibility =>
            string.IsNullOrEmpty(RequirementType) ? Visibility.Collapsed : Visibility.Visible;

        // Navigation identifier
        public string ItemNormalizedName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Guide image view model
    /// </summary>
    public class GuideImageViewModel
    {
        public string FileName { get; set; } = string.Empty;
        public string? Caption { get; set; }
        public BitmapImage? ImageSource { get; set; }
        public Visibility CaptionVisibility =>
            string.IsNullOrEmpty(Caption) ? Visibility.Collapsed : Visibility.Visible;
    }

    public partial class QuestListPage : UserControl
    {
        private readonly LocalizationService _loc = LocalizationService.Instance;
        private readonly QuestProgressService _progressService = QuestProgressService.Instance;
        private readonly ImageCacheService _imageCache = ImageCacheService.Instance;
        private List<QuestViewModel> _allQuestViewModels = new();
        private List<string> _traders = new();
        private List<string> _maps = new();
        private List<TarkovMap>? _mapData;
        private List<TarkovItem>? _itemData;
        private Dictionary<string, TarkovItem>? _itemLookup;
        private bool _isInitializing = true;
        private bool _isDataLoaded = false;
        private string? _pendingQuestSelection = null;

        // Status brushes
        private static readonly Brush LockedBrush = new SolidColorBrush(Color.FromRgb(102, 102, 102));
        private static readonly Brush ActiveBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        private static readonly Brush DoneBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243));
        private static readonly Brush FailedBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54));
        private static readonly Brush LevelLockedBrush = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange for Level Locked

        public QuestListPage()
        {
            InitializeComponent();
            _loc.LanguageChanged += OnLanguageChanged;
            _progressService.ProgressChanged += OnProgressChanged;

            Loaded += QuestListPage_Loaded;
        }

        private async void QuestListPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Skip if already loaded (prevents re-initialization on tab switching)
            if (_isDataLoaded) return;

            await LoadMapsAsync();
            LoadQuests();
            PopulateTraderFilter();
            PopulateMapFilter();
            _isInitializing = false;
            _isDataLoaded = true;
            ApplyFilters();

            // Process pending selection if any
            if (!string.IsNullOrEmpty(_pendingQuestSelection))
            {
                var pendingName = _pendingQuestSelection;
                _pendingQuestSelection = null;
                SelectQuestInternal(pendingName);
            }
        }

        private async Task LoadMapsAsync()
        {
            var apiService = new TarkovDevApiService();
            _mapData = await apiService.LoadMapsFromJsonAsync();

            // Also load items data for localized names and icons
            _itemData = await apiService.LoadItemsFromJsonAsync();
            if (_itemData != null)
            {
                _itemLookup = TarkovDevApiService.BuildItemLookup(_itemData);
            }
        }

        private void OnLanguageChanged(object? sender, AppLanguage e)
        {
            RefreshQuestDisplayNames();
            ApplyFilters();
            UpdateDetailPanel();
        }

        private void OnProgressChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                RefreshQuestStatuses();
                ApplyFilters();
                UpdateDetailPanel();
            });
        }

        /// <summary>
        /// Public method to refresh the display from external callers
        /// </summary>
        public void RefreshDisplay()
        {
            RefreshQuestStatuses();
            ApplyFilters();
            UpdateDetailPanel();
        }

        /// <summary>
        /// Select a quest by its normalized name (for cross-tab navigation)
        /// </summary>
        public void SelectQuest(string questNormalizedName)
        {
            // If data is not loaded yet, save for later
            if (!_isDataLoaded)
            {
                _pendingQuestSelection = questNormalizedName;
                return;
            }

            SelectQuestInternal(questNormalizedName);
        }

        /// <summary>
        /// Internal method to select a quest (called when data is ready)
        /// </summary>
        private void SelectQuestInternal(string questNormalizedName)
        {
            // Reset filters to ensure the quest is visible
            ResetFiltersForNavigation();

            // Find the quest view model
            var questVm = _allQuestViewModels.FirstOrDefault(vm =>
                string.Equals(vm.Task.NormalizedName, questNormalizedName, StringComparison.OrdinalIgnoreCase));

            if (questVm == null) return;

            // Disable selection changed event BEFORE ApplyFilters to prevent
            // the detail panel from being hidden when ItemsSource changes
            LstQuests.SelectionChanged -= LstQuests_SelectionChanged;

            // Apply filters to update the list
            ApplyFilters();

            // Use Dispatcher to ensure UI is updated before selection
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // Select the quest in the list
                    LstQuests.SelectedItem = questVm;

                    // Scroll to make it visible
                    LstQuests.ScrollIntoView(questVm);

                    // Force UI update
                    LstQuests.UpdateLayout();

                    // Force update detail panel with the specific quest
                    UpdateDetailPanel(questVm);
                }
                finally
                {
                    // Re-enable selection changed event
                    LstQuests.SelectionChanged += LstQuests_SelectionChanged;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Reset filters for navigation to ensure target item is visible
        /// </summary>
        private void ResetFiltersForNavigation()
        {
            _isInitializing = true;

            // Reset status filter to "All"
            CmbStatus.SelectedIndex = 1; // "All"

            // Clear search text
            TxtSearch.Text = "";

            // Reset other filters
            ChkKappaOnly.IsChecked = false;
            ChkItemRequired.IsChecked = false;
            CmbTrader.SelectedIndex = 0; // "All Traders"
            CmbMap.SelectedIndex = 0; // "All Maps"

            _isInitializing = false;
        }

        private void LoadQuests()
        {
            var tasks = _progressService.AllTasks;

            _allQuestViewModels = tasks.Select(t => CreateQuestViewModel(t)).ToList();
            _traders = tasks.Select(t => t.Trader).Where(t => !string.IsNullOrEmpty(t)).Distinct().OrderBy(t => t).ToList();
            _maps = tasks.Where(t => t.Maps != null).SelectMany(t => t.Maps!).Distinct().OrderBy(m => m).ToList();
        }

        private QuestViewModel CreateQuestViewModel(TarkovTask task)
        {
            var status = _progressService.GetStatus(task);
            var (displayName, subtitle, showSubtitle) = GetLocalizedNames(task);

            return new QuestViewModel
            {
                Task = task,
                DisplayName = displayName,
                SubtitleName = subtitle,
                SubtitleVisibility = showSubtitle ? Visibility.Visible : Visibility.Collapsed,
                TraderInitial = GetTraderInitial(task.Trader),
                Status = status,
                StatusText = GetStatusText(status, task),
                StatusBackground = GetStatusBrush(status),
                CompleteButtonVisibility = status == QuestStatus.Active || status == QuestStatus.Locked || status == QuestStatus.LevelLocked
                    ? Visibility.Visible : Visibility.Collapsed,
                IsKappaRequired = task.ReqKappa
            };
        }

        private (string DisplayName, string Subtitle, bool ShowSubtitle) GetLocalizedNames(TarkovTask task)
        {
            var lang = _loc.CurrentLanguage;

            if (lang == AppLanguage.EN)
            {
                return (task.Name, string.Empty, false);
            }

            // For KO/JA, show localized name as main, English as subtitle
            var localizedName = lang switch
            {
                AppLanguage.KO => task.NameKo,
                AppLanguage.JA => task.NameJa,
                _ => null
            };

            if (!string.IsNullOrEmpty(localizedName))
            {
                return (localizedName, task.Name, true);
            }

            // Fallback to English only
            return (task.Name, string.Empty, false);
        }

        private static string GetTraderInitial(string trader)
        {
            if (string.IsNullOrEmpty(trader)) return "?";
            return trader.Length >= 2 ? trader[..2].ToUpper() : trader.ToUpper();
        }

        private string GetStatusText(QuestStatus status, TarkovTask? task = null)
        {
            if (status == QuestStatus.LevelLocked && task?.RequiredLevel.HasValue == true)
            {
                return $"Lv.{task.RequiredLevel}";
            }

            return status switch
            {
                QuestStatus.Locked => "Locked",
                QuestStatus.Active => "Active",
                QuestStatus.Done => "Done",
                QuestStatus.Failed => "Failed",
                QuestStatus.LevelLocked => "Level",
                _ => "Unknown"
            };
        }

        private static Brush GetStatusBrush(QuestStatus status)
        {
            return status switch
            {
                QuestStatus.Locked => LockedBrush,
                QuestStatus.Active => ActiveBrush,
                QuestStatus.Done => DoneBrush,
                QuestStatus.Failed => FailedBrush,
                QuestStatus.LevelLocked => LevelLockedBrush,
                _ => Brushes.Gray
            };
        }

        private void RefreshQuestDisplayNames()
        {
            foreach (var vm in _allQuestViewModels)
            {
                var (displayName, subtitle, showSubtitle) = GetLocalizedNames(vm.Task);
                vm.DisplayName = displayName;
                vm.SubtitleName = subtitle;
                vm.SubtitleVisibility = showSubtitle ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void RefreshQuestStatuses()
        {
            foreach (var vm in _allQuestViewModels)
            {
                var status = _progressService.GetStatus(vm.Task);
                vm.Status = status;
                vm.StatusText = GetStatusText(status, vm.Task);
                vm.StatusBackground = GetStatusBrush(status);
                vm.CompleteButtonVisibility = status == QuestStatus.Active || status == QuestStatus.Locked || status == QuestStatus.LevelLocked
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void PopulateTraderFilter()
        {
            // Clear existing items except "All Traders"
            while (CmbTrader.Items.Count > 1)
            {
                CmbTrader.Items.RemoveAt(1);
            }

            foreach (var trader in _traders)
            {
                CmbTrader.Items.Add(new ComboBoxItem { Content = trader, Tag = trader });
            }
        }

        private void PopulateMapFilter()
        {
            // Clear existing items except "All Maps"
            while (CmbMap.Items.Count > 1)
            {
                CmbMap.Items.RemoveAt(1);
            }

            foreach (var mapNormalized in _maps)
            {
                // Get localized map name
                var mapName = GetLocalizedMapName(mapNormalized);
                CmbMap.Items.Add(new ComboBoxItem { Content = mapName, Tag = mapNormalized });
            }
        }

        private string GetLocalizedMapName(string normalizedName)
        {
            if (_mapData == null) return normalizedName;

            var map = _mapData.FirstOrDefault(m =>
                string.Equals(m.NormalizedName, normalizedName, StringComparison.OrdinalIgnoreCase));

            if (map == null) return normalizedName;

            return _loc.CurrentLanguage switch
            {
                AppLanguage.KO => map.NameKo ?? map.Name,
                AppLanguage.JA => map.NameJa ?? map.Name,
                _ => map.Name
            };
        }

        private void ApplyFilters()
        {
            var searchText = TxtSearch.Text?.Trim().ToLowerInvariant() ?? string.Empty;
            var kappaOnly = ChkKappaOnly.IsChecked == true;
            var itemRequired = ChkItemRequired.IsChecked == true;

            var selectedTrader = (CmbTrader.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
            var selectedMap = (CmbMap.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
            var selectedStatus = (CmbStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Active";

            var filtered = _allQuestViewModels.Where(vm =>
            {
                // Search filter (multi-language)
                if (!string.IsNullOrEmpty(searchText))
                {
                    var matchName = vm.Task.Name?.ToLowerInvariant().Contains(searchText) == true;
                    var matchKo = vm.Task.NameKo?.ToLowerInvariant().Contains(searchText) == true;
                    var matchJa = vm.Task.NameJa?.ToLowerInvariant().Contains(searchText) == true;

                    if (!matchName && !matchKo && !matchJa)
                        return false;
                }

                // Kappa filter
                if (kappaOnly && !vm.Task.ReqKappa)
                    return false;

                // Item required filter
                if (itemRequired && (vm.Task.RequiredItems == null || vm.Task.RequiredItems.Count == 0))
                    return false;

                // Trader filter
                if (!string.IsNullOrEmpty(selectedTrader) && vm.Task.Trader != selectedTrader)
                    return false;

                // Map filter
                if (!string.IsNullOrEmpty(selectedMap))
                {
                    if (vm.Task.Maps == null || !vm.Task.Maps.Any(m =>
                        string.Equals(m, selectedMap, StringComparison.OrdinalIgnoreCase)))
                        return false;
                }

                // Status filter
                if (selectedStatus != "All")
                {
                    var statusFilter = Enum.Parse<QuestStatus>(selectedStatus);
                    if (vm.Status != statusFilter)
                        return false;
                }

                return true;
            }).ToList();

            LstQuests.ItemsSource = filtered;

            // Update statistics
            var stats = _progressService.GetStatistics();
            var playerLevel = SettingsService.Instance.PlayerLevel;
            TxtStats.Text = $"Lv.{playerLevel} | Showing {filtered.Count} of {stats.Total} quests | " +
                           $"Active: {stats.Active} | Level: {stats.LevelLocked} | Done: {stats.Done} | Locked: {stats.Locked} | Failed: {stats.Failed}";

            // Update Kappa progress gauge
            UpdateKappaGauge();
        }

        private void UpdateKappaGauge()
        {
            try
            {
                var graphService = QuestGraphService.Instance;
                var (completed, total, percentage) = graphService.GetCollectorProgress(
                    normalizedName => _progressService.IsQuestCompleted(normalizedName));

                TxtKappaGauge.Text = $"{completed}/{total}";
                KappaGaugeBar.Width = (percentage / 100.0) * 120; // 120 is the gauge width
            }
            catch
            {
                // QuestGraphService not initialized yet
                TxtKappaGauge.Text = "0/0";
                KappaGaugeBar.Width = 0;
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitializing) ApplyFilters();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitializing) ApplyFilters();
        }

        private void CmbTrader_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing) ApplyFilters();
        }

        private void CmbMap_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing) ApplyFilters();
        }

        private void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing) ApplyFilters();
        }

        private void LstQuests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDetailPanel();
        }

        private void UpdateDetailPanel(QuestViewModel? overrideVm = null)
        {
            var selectedVm = overrideVm ?? LstQuests.SelectedItem as QuestViewModel;

            if (selectedVm == null)
            {
                DetailPanel.Visibility = Visibility.Collapsed;
                TxtSelectQuest.Visibility = Visibility.Visible;
                return;
            }

            DetailPanel.Visibility = Visibility.Visible;
            TxtSelectQuest.Visibility = Visibility.Collapsed;

            var task = selectedVm.Task;
            var status = _progressService.GetStatus(task);

            // Title
            var (displayName, subtitle, showSubtitle) = GetLocalizedNames(task);
            TxtDetailName.Text = displayName;
            TxtDetailSubtitle.Text = subtitle;
            TxtDetailSubtitle.Visibility = showSubtitle ? Visibility.Visible : Visibility.Collapsed;

            // Trader & Status
            TxtDetailTrader.Text = task.Trader;
            TxtDetailStatus.Text = GetStatusText(status);
            DetailStatusBadge.Background = GetStatusBrush(status);

            // Maps
            if (task.Maps != null && task.Maps.Count > 0)
            {
                var mapNames = task.Maps.Select(GetLocalizedMapName);
                TxtDetailMap.Text = string.Join(", ", mapNames);
                MapInfoPanel.Visibility = Visibility.Visible;
            }
            else
            {
                TxtDetailMap.Text = "-";
                MapInfoPanel.Visibility = Visibility.Visible;
            }

            // Kappa Progress Section (for Collector quest)
            UpdateKappaProgressSection(task);

            // Requirements - Level with current level comparison
            if (task.RequiredLevel.HasValue && task.RequiredLevel.Value > 0)
            {
                var playerLevel = SettingsService.Instance.PlayerLevel;
                var reqLevel = task.RequiredLevel.Value;
                if (playerLevel >= reqLevel)
                {
                    TxtRequiredLevel.Text = $"Level {reqLevel} (Current: {playerLevel})";
                    TxtRequiredLevel.Foreground = (Brush)FindResource("TextPrimaryBrush");
                }
                else
                {
                    TxtRequiredLevel.Text = $"Level {reqLevel} (Current: {playerLevel})";
                    TxtRequiredLevel.Foreground = LevelLockedBrush;
                }
                TxtRequiredLevel.Visibility = Visibility.Visible;
            }
            else
            {
                TxtRequiredLevel.Visibility = Visibility.Collapsed;
            }

            // Prerequisites
            var prereqs = _progressService.GetPrerequisiteChain(task);
            if (prereqs.Count > 0)
            {
                var prereqVms = prereqs.Select(p =>
                {
                    var pStatus = _progressService.GetStatus(p);
                    var (pName, _, _) = GetLocalizedNames(p);
                    return new QuestViewModel
                    {
                        DisplayName = pName,
                        StatusText = GetStatusText(pStatus),
                        StatusBackground = GetStatusBrush(pStatus)
                    };
                }).ToList();

                PrerequisitesList.ItemsSource = prereqVms;
            }
            else
            {
                PrerequisitesList.ItemsSource = new[] { new QuestViewModel { DisplayName = "None" } };
            }

            // Guide Section
            UpdateGuideSection(task);

            // Required Items
            if (task.RequiredItems != null && task.RequiredItems.Count > 0)
            {
                _ = LoadRequiredItemsAsync(task.RequiredItems);
                TxtRequiredItemsHeader.Visibility = Visibility.Visible;
                RequiredItemsSection.Visibility = Visibility.Visible;
            }
            else
            {
                RequiredItemsList.ItemsSource = null;
                TxtRequiredItemsHeader.Visibility = Visibility.Collapsed;
                RequiredItemsSection.Visibility = Visibility.Collapsed;
            }

            // Button states
            BtnComplete.Visibility = status == QuestStatus.Done ? Visibility.Collapsed : Visibility.Visible;
            BtnReset.Visibility = status == QuestStatus.Done || status == QuestStatus.Failed
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CompleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is QuestViewModel vm)
            {
                _progressService.CompleteQuest(vm.Task, true);
            }
        }

        private void BtnWiki_Click(object sender, RoutedEventArgs e)
        {
            var selectedVm = LstQuests.SelectedItem as QuestViewModel;
            if (selectedVm?.Task.Name == null) return;

            var wikiPageName = NormalizedNameGenerator.GetWikiPageName(selectedVm.Task.Name);
            var wikiUrl = $"https://escapefromtarkov.fandom.com/wiki/{Uri.EscapeDataString(wikiPageName.Replace(" ", "_"))}";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = wikiUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore errors opening browser
            }
        }

        private void BtnComplete_Click(object sender, RoutedEventArgs e)
        {
            var selectedVm = LstQuests.SelectedItem as QuestViewModel;
            if (selectedVm != null)
            {
                _progressService.CompleteQuest(selectedVm.Task, true);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var selectedVm = LstQuests.SelectedItem as QuestViewModel;
            if (selectedVm != null)
            {
                _progressService.ResetQuest(selectedVm.Task);
            }
        }

        #region Kappa Progress Section

        private void UpdateKappaProgressSection(TarkovTask task)
        {
            // Check if this is the Collector quest
            var isCollector = task.NormalizedName?.Equals("collector", StringComparison.OrdinalIgnoreCase) == true;

            if (!isCollector)
            {
                KappaProgressSection.Visibility = Visibility.Collapsed;
                return;
            }

            KappaProgressSection.Visibility = Visibility.Visible;

            // Get Kappa progress
            var graphService = QuestGraphService.Instance;
            var (completed, total, percentage) = graphService.GetCollectorProgress(
                normalizedName => _progressService.IsQuestCompleted(normalizedName));

            // Update progress text
            TxtKappaProgress.Text = $"Prerequisites: ({completed}/{total} completed)";
            TxtKappaProgressPercent.Text = $"{percentage}%";

            // Update progress bar width
            KappaProgressBar.Width = (percentage / 100.0) * (KappaProgressBar.Parent as Grid)?.ActualWidth ?? 0;

            // If parent grid not yet rendered, set it after layout
            if (KappaProgressBar.Width == 0 && percentage > 0)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var parentGrid = KappaProgressBar.Parent as Grid;
                    if (parentGrid != null)
                    {
                        KappaProgressBar.Width = (percentage / 100.0) * parentGrid.ActualWidth;
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void BtnShowKappaQuests_Click(object sender, RoutedEventArgs e)
        {
            var graphService = QuestGraphService.Instance;
            var kappaQuests = graphService.GetKappaRequiredQuestsWithStatus(
                normalizedName => _progressService.IsQuestCompleted(normalizedName));

            // Create a popup window to show all Kappa required quests
            var popupWindow = new Window
            {
                Title = "Kappa Required Quests",
                Width = 500,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = (Brush)FindResource("BackgroundDarkBrush")
            };

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stackPanel = new StackPanel { Margin = new Thickness(16) };

            // Header
            var (completed, total, percentage) = graphService.GetCollectorProgress(
                normalizedName => _progressService.IsQuestCompleted(normalizedName));
            var headerText = new TextBlock
            {
                Text = $"Kappa Required Quests ({completed}/{total})",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("AccentBrush"),
                Margin = new Thickness(0, 0, 0, 16)
            };
            stackPanel.Children.Add(headerText);

            // Quest list
            foreach (var (quest, isCompleted) in kappaQuests)
            {
                var (displayName, _, _) = GetLocalizedNames(quest);
                var questPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };

                // Status indicator
                var statusIndicator = new TextBlock
                {
                    Text = isCompleted ? "✓" : "○",
                    FontSize = 14,
                    Foreground = isCompleted ? DoneBrush : (Brush)FindResource("TextSecondaryBrush"),
                    Width = 24,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Quest name
                var questName = new TextBlock
                {
                    Text = displayName,
                    FontSize = 13,
                    Foreground = isCompleted ? (Brush)FindResource("TextSecondaryBrush") : (Brush)FindResource("TextPrimaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextDecorations = isCompleted ? TextDecorations.Strikethrough : null
                };

                // Trader
                var traderText = new TextBlock
                {
                    Text = $"  ({quest.Trader})",
                    FontSize = 11,
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                };

                questPanel.Children.Add(statusIndicator);
                questPanel.Children.Add(questName);
                questPanel.Children.Add(traderText);
                stackPanel.Children.Add(questPanel);
            }

            scrollViewer.Content = stackPanel;
            popupWindow.Content = scrollViewer;
            popupWindow.ShowDialog();
        }

        #endregion

        #region Guide Section

        private void UpdateGuideSection(TarkovTask task)
        {
            var hasGuideText = !string.IsNullOrEmpty(task.GuideText);
            var hasGuideImages = task.GuideImages != null && task.GuideImages.Count > 0;

            if (!hasGuideText && !hasGuideImages)
            {
                GuideSection.Visibility = Visibility.Collapsed;
                return;
            }

            GuideSection.Visibility = Visibility.Visible;

            // Guide text
            if (hasGuideText)
            {
                TxtGuideText.Text = task.GuideText;
                GuideTextExpander.Visibility = Visibility.Visible;
                GuideTextExpander.IsExpanded = false; // Reset to collapsed when switching quests
            }
            else
            {
                GuideTextExpander.Visibility = Visibility.Collapsed;
            }

            // Guide images
            if (hasGuideImages)
            {
                _ = LoadGuideImagesAsync(task.GuideImages!);
            }
            else
            {
                GuideImagesList.ItemsSource = null;
            }
        }

        private async Task LoadGuideImagesAsync(List<GuideImage> guideImages)
        {
            var imageVms = new List<GuideImageViewModel>();

            foreach (var guideImage in guideImages)
            {
                var vm = new GuideImageViewModel
                {
                    FileName = guideImage.FileName,
                    Caption = guideImage.Caption
                };

                // Load image asynchronously
                var image = await _imageCache.GetWikiImageAsync(guideImage.FileName);
                vm.ImageSource = image;
                imageVms.Add(vm);
            }

            // Update on UI thread
            Dispatcher.Invoke(() =>
            {
                GuideImagesList.ItemsSource = imageVms;
            });
        }

        private void GuideImage_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is GuideImageViewModel vm)
            {
                // Open wiki image in browser
                var encodedFileName = Uri.EscapeDataString(vm.FileName.Replace(" ", "_"));
                var url = $"https://escapefromtarkov.fandom.com/wiki/File:{encodedFileName}";

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // Ignore errors opening browser
                }
            }
        }

        #endregion

        #region Required Items with Localization

        private async Task LoadRequiredItemsAsync(List<QuestItem> requiredItems)
        {
            var itemVms = new List<RequiredItemViewModel>();

            foreach (var item in requiredItems)
            {
                var vm = new RequiredItemViewModel
                {
                    FoundInRaid = item.FoundInRaid,
                    RequirementType = item.Requirement,
                    ItemNormalizedName = item.ItemNormalizedName // For navigation
                };

                // Get localized item name
                var localizedName = GetLocalizedItemName(item.ItemNormalizedName);
                vm.DisplayText = $"{localizedName} x{item.Amount}";

                // Get item icon
                var tarkovItem = GetItemByNormalizedName(item.ItemNormalizedName);
                if (tarkovItem?.IconLink != null)
                {
                    var icon = await _imageCache.GetItemIconAsync(tarkovItem.IconLink);
                    vm.IconSource = icon;
                }

                itemVms.Add(vm);
            }

            // Update on UI thread
            Dispatcher.Invoke(() =>
            {
                RequiredItemsList.ItemsSource = itemVms;
            });
        }

        /// <summary>
        /// Handle click on item name to navigate to Items tab
        /// </summary>
        private void ItemName_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is RequiredItemViewModel vm)
            {
                if (string.IsNullOrEmpty(vm.ItemNormalizedName)) return;

                var mainWindow = Window.GetWindow(this) as MainWindow;
                mainWindow?.NavigateToItem(vm.ItemNormalizedName);
            }
        }

        private string GetLocalizedItemName(string normalizedName)
        {
            var item = GetItemByNormalizedName(normalizedName);
            if (item == null)
                return normalizedName;

            return _loc.CurrentLanguage switch
            {
                AppLanguage.KO => item.NameKo ?? item.Name,
                AppLanguage.JA => item.NameJa ?? item.Name,
                _ => item.Name
            };
        }

        private TarkovItem? GetItemByNormalizedName(string normalizedName)
        {
            if (_itemLookup == null)
                return null;

            // Try direct lookup
            if (_itemLookup.TryGetValue(normalizedName, out var item))
                return item;

            // Try with alternative names (fuzzy match)
            var alternatives = NormalizedNameGenerator.GenerateAlternatives(normalizedName);
            foreach (var alt in alternatives)
            {
                if (_itemLookup.TryGetValue(alt, out item))
                    return item;
            }

            return null;
        }

        #endregion
    }
}

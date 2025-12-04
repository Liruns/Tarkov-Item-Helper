using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using TarkovHelper.Models;
using TarkovHelper.Models.MapTracker;
using TarkovHelper.Services;
using TarkovHelper.Services.MapTracker;

namespace TarkovHelper.Pages;

/// <summary>
/// 맵 위치 추적 페이지.
/// 스크린샷 폴더를 감시하고 플레이어 위치를 맵 위에 표시합니다.
/// </summary>
public partial class MapTrackerPage : UserControl
{
    private readonly MapTrackerService? _trackerService;
    private readonly QuestObjectiveService _objectiveService = QuestObjectiveService.Instance;
    private readonly QuestProgressService _progressService = QuestProgressService.Instance;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private string? _currentMapKey;
    private double _zoomLevel = 1.0;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 5.0;

    // 드래그 관련 필드
    private bool _isDragging;
    private Point _dragStartPoint;
    private double _dragStartTranslateX;
    private double _dragStartTranslateY;

    // 줌 레벨 프리셋
    private static readonly double[] ZoomPresets = { 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0 };

    // 퀘스트 마커 관련 필드
    private readonly List<FrameworkElement> _questMarkerElements = new();
    private List<TaskObjectiveWithLocation> _currentMapObjectives = new();
    private bool _showQuestMarkers = true;

    public MapTrackerPage()
    {
        try
        {
            InitializeComponent();

            _trackerService = MapTrackerService.Instance;

            // 이벤트 연결
            _trackerService.PositionUpdated += OnPositionUpdated;
            _trackerService.ErrorOccurred += OnErrorOccurred;
            _trackerService.StatusMessage += OnStatusMessage;
            _trackerService.WatchingStateChanged += OnWatchingStateChanged;
            _loc.LanguageChanged += OnLanguageChanged;

            Loaded += MapTrackerPage_Loaded;
            Unloaded += MapTrackerPage_Unloaded;

            // 줌 콤보박스 초기화
            InitializeZoomComboBox();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"MapTrackerPage initialization error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void InitializeZoomComboBox()
    {
        CmbZoomLevel.Items.Clear();
        foreach (var preset in ZoomPresets)
        {
            CmbZoomLevel.Items.Add($"{preset * 100:F0}%");
        }
        CmbZoomLevel.Text = "100%";
    }

    private async void MapTrackerPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // 페이지 로드 시 Trail 초기화
            _trackerService?.ClearTrail();
            TrailPath.Points.Clear();

            LoadSettings();
            PopulateMapComboBox();
            UpdateUI();

            // 퀘스트 목표 데이터 로드
            await LoadQuestObjectivesAsync();

            // 퀘스트 진행 상태 변경 이벤트 구독
            _progressService.ProgressChanged += OnQuestProgressChanged;
            _progressService.ObjectiveProgressChanged += OnObjectiveProgressChanged;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"MapTrackerPage load error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MapTrackerPage_Unloaded(object sender, RoutedEventArgs e)
    {
        // 이벤트 구독 해제
        _progressService.ProgressChanged -= OnQuestProgressChanged;
        _progressService.ObjectiveProgressChanged -= OnObjectiveProgressChanged;
    }

    private void OnLanguageChanged(object? sender, AppLanguage e)
    {
        UpdateLocalizedText();
    }

    private void UpdateLocalizedText()
    {
        // 다국어 지원이 필요한 경우 여기서 처리
    }

    private void LoadSettings()
    {
        if (_trackerService == null) return;
        var settings = _trackerService.Settings;
        TxtScreenshotFolder.Text = settings.ScreenshotFolderPath;
        TxtFilePattern.Text = settings.FileNamePattern;
        SliderMarkerSize.Value = settings.MarkerSize;
        ChkShowDirection.IsChecked = settings.ShowDirection;
        ChkShowTrail.IsChecked = settings.ShowTrail;
    }

    private void PopulateMapComboBox()
    {
        if (_trackerService == null) return;
        CmbMapSelect.Items.Clear();
        foreach (var mapKey in _trackerService.GetAllMapKeys())
        {
            var config = _trackerService.GetMapConfig(mapKey);
            CmbMapSelect.Items.Add(new ComboBoxItem
            {
                Content = config?.DisplayName ?? mapKey,
                Tag = mapKey
            });
        }

        if (CmbMapSelect.Items.Count > 0)
            CmbMapSelect.SelectedIndex = 0;
    }

    private void UpdateUI()
    {
        // 감시 상태에 따른 UI 업데이트
        var isWatching = _trackerService?.IsWatching ?? false;
        BtnToggleTracking.Content = isWatching ? "Stop Tracking" : "Start Tracking";

        var successBrush = TryFindResource("SuccessBrush") as Brush ?? Brushes.Green;
        var secondaryBrush = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
        StatusIndicator.Fill = isWatching ? successBrush : secondaryBrush;
        TxtStatus.Text = isWatching ? "감시 중" : "대기 중";
    }

    #region 이벤트 핸들러 - 서비스

    private void OnPositionUpdated(object? sender, ScreenPosition position)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateMarkerPosition(position);
            UpdateTrailPath();
            UpdateCoordinatesDisplay(position);
        });
    }

    private void OnErrorOccurred(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            TxtStatus.Text = $"오류: {message}";
            TxtStatus.Foreground = TryFindResource("WarningBrush") as Brush ?? Brushes.Orange;
        });
    }

    private void OnStatusMessage(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            TxtStatus.Text = message;
            TxtStatus.Foreground = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
        });
    }

    private void OnWatchingStateChanged(object? sender, bool isWatching)
    {
        Dispatcher.Invoke(UpdateUI);
    }

    #endregion

    #region 이벤트 핸들러 - UI

    private void BtnToggleTracking_Click(object sender, RoutedEventArgs e)
    {
        if (_trackerService == null) return;
        if (_trackerService.IsWatching)
        {
            _trackerService.StopTracking();
        }
        else
        {
            _trackerService.StartTracking();
        }
    }

    private void BtnClearTrail_Click(object sender, RoutedEventArgs e)
    {
        _trackerService?.ClearTrail();
        TrailPath.Points.Clear();
        PlayerMarker.Visibility = Visibility.Collapsed;
        PlayerDot.Visibility = Visibility.Collapsed;
        TxtCoordinates.Text = "--";
        TxtLastUpdate.Text = "마지막 업데이트: --";
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        ToggleSettingsPanel(true);
    }

    private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
    {
        ToggleSettingsPanel(false);
    }

    private void ToggleSettingsPanel(bool show)
    {
        if (show)
        {
            SettingsColumn.Width = new GridLength(320);
            SettingsPanel.Visibility = Visibility.Visible;
            LoadCurrentMapSettings();
        }
        else
        {
            SettingsColumn.Width = new GridLength(0);
            SettingsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void CmbMapSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbMapSelect.SelectedItem is ComboBoxItem item && item.Tag is string mapKey)
        {
            _currentMapKey = mapKey;
            _trackerService?.SetCurrentMap(mapKey);

            // 맵 변경 시 Trail 초기화
            _trackerService?.ClearTrail();
            TrailPath.Points.Clear();
            PlayerMarker.Visibility = Visibility.Collapsed;
            PlayerDot.Visibility = Visibility.Collapsed;

            LoadMapImage(mapKey);
            LoadCurrentMapSettings();

            // 초기화 완료 후에만 호출
            if (_objectiveService.IsLoaded)
            {
                RefreshQuestMarkers();
            }
            if (QuestDrawerPanel != null)
            {
                CloseQuestDrawer();
            }
        }
    }

    private void BtnAutoDetect_Click(object sender, RoutedEventArgs e)
    {
        var detectedPath = MapTrackerSettings.TryDetectScreenshotFolder();
        if (!string.IsNullOrEmpty(detectedPath))
        {
            TxtScreenshotFolder.Text = detectedPath;
            _trackerService?.ChangeScreenshotFolder(detectedPath);
            MessageBox.Show($"스크린샷 폴더를 찾았습니다:\n{detectedPath}", "자동 탐지 성공",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            // 가능한 경로 목록 표시
            var possiblePaths = MapTrackerSettings.GetPossibleScreenshotPaths();
            if (possiblePaths.Count > 0)
            {
                var pathList = string.Join("\n", possiblePaths);
                MessageBox.Show($"스크린샷 폴더를 자동으로 찾지 못했습니다.\n\n발견된 EFT 폴더:\n{pathList}\n\n수동으로 선택해주세요.",
                    "자동 탐지 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show("EFT 스크린샷 폴더를 찾을 수 없습니다.\n수동으로 폴더를 선택해주세요.",
                    "자동 탐지 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "스크린샷 폴더 선택",
            InitialDirectory = TxtScreenshotFolder.Text
        };

        if (dialog.ShowDialog() == true)
        {
            TxtScreenshotFolder.Text = dialog.FolderName;
            _trackerService?.ChangeScreenshotFolder(dialog.FolderName);
        }
    }

    private void BtnApplyPattern_Click(object sender, RoutedEventArgs e)
    {
        if (_trackerService == null) return;
        var pattern = TxtFilePattern.Text;
        if (_trackerService.ChangeFileNamePattern(pattern))
        {
            MessageBox.Show("패턴이 적용되었습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("유효하지 않은 정규식 패턴입니다.\nmap, x, y 그룹이 필요합니다.", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SliderMarkerSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtMarkerSize != null && _trackerService != null)
        {
            var size = (int)e.NewValue;
            TxtMarkerSize.Text = size.ToString();
            _trackerService.Settings.MarkerSize = size;
            UpdateMarkerSize(size);
        }
    }

    private void ChkShowDirection_Changed(object sender, RoutedEventArgs e)
    {
        if (_trackerService == null) return;
        _trackerService.Settings.ShowDirection = ChkShowDirection.IsChecked ?? true;
        UpdateMarkerVisibility();
    }

    private void ChkShowTrail_Changed(object sender, RoutedEventArgs e)
    {
        if (_trackerService == null) return;
        _trackerService.Settings.ShowTrail = ChkShowTrail.IsChecked ?? true;
        TrailPath.Visibility = _trackerService.Settings.ShowTrail ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnSaveMapSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_trackerService == null) return;
        if (string.IsNullOrEmpty(_currentMapKey))
        {
            MessageBox.Show("맵을 먼저 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var config = _trackerService.GetMapConfig(_currentMapKey);
        if (config == null) return;

        if (double.TryParse(TxtWorldMinX.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var minX))
            config.WorldMinX = minX;
        if (double.TryParse(TxtWorldMaxX.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxX))
            config.WorldMaxX = maxX;
        if (double.TryParse(TxtWorldMinY.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var minY))
            config.WorldMinY = minY;
        if (double.TryParse(TxtWorldMaxY.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxY))
            config.WorldMaxY = maxY;

        config.InvertY = ChkInvertY.IsChecked ?? true;
        config.InvertX = ChkInvertX.IsChecked ?? false;

        _trackerService.UpdateMapConfig(config);
        MessageBox.Show("맵 설정이 저장되었습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnTestCoordinate_Click(object sender, RoutedEventArgs e)
    {
        if (_trackerService == null) return;
        if (string.IsNullOrEmpty(_currentMapKey))
        {
            MessageBox.Show("맵을 먼저 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(TxtTestX.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
        {
            MessageBox.Show("유효한 X 좌표를 입력하세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!double.TryParse(TxtTestY.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            MessageBox.Show("유효한 Y 좌표를 입력하세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        double? angle = null;
        if (double.TryParse(TxtTestAngle.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var a))
            angle = a;

        var screenPos = _trackerService.TestCoordinate(_currentMapKey, x, y, angle);
        if (screenPos != null)
        {
            UpdateMarkerPosition(screenPos);
            UpdateCoordinatesDisplay(screenPos);
        }
        else
        {
            MessageBox.Show("좌표 변환에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnTestFile_Click(object sender, RoutedEventArgs e)
    {
        if (_trackerService == null) return;
        if (string.IsNullOrEmpty(_currentMapKey))
        {
            MessageBox.Show("맵을 먼저 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var filePath = TxtTestFilePath.Text?.Trim();
        if (string.IsNullOrEmpty(filePath))
        {
            MessageBox.Show("테스트할 파일 경로나 파일명을 입력하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var screenPos = _trackerService.ProcessScreenshotFile(filePath, _currentMapKey);
        if (screenPos != null)
        {
            UpdateMarkerPosition(screenPos);
            UpdateCoordinatesDisplay(screenPos);
        }
        else
        {
            MessageBox.Show("파일 파싱 또는 좌표 변환에 실패했습니다.\n파일명 형식을 확인하세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        // 다음 프리셋으로 줌
        var nextPreset = ZoomPresets.FirstOrDefault(p => p > _zoomLevel);
        if (nextPreset > 0)
            SetZoom(nextPreset);
        else
            SetZoom(_zoomLevel * 1.25);
    }

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        // 이전 프리셋으로 줌
        var prevPreset = ZoomPresets.LastOrDefault(p => p < _zoomLevel);
        if (prevPreset > 0)
            SetZoom(prevPreset);
        else
            SetZoom(_zoomLevel * 0.8);
    }

    private void CmbZoomLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbZoomLevel.SelectedItem is string selected)
        {
            ParseAndSetZoom(selected);
        }
    }

    private void CmbZoomLevel_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ParseAndSetZoom(CmbZoomLevel.Text);
            e.Handled = true;
        }
    }

    private void ParseAndSetZoom(string zoomText)
    {
        // "100%" 형식에서 숫자 추출
        var text = zoomText.Trim().TrimEnd('%');
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
        {
            SetZoom(percent / 100.0);
        }
    }

    private void BtnResetView_Click(object sender, RoutedEventArgs e)
    {
        // 줌과 위치 초기화
        SetZoom(1.0);
        MapTranslate.X = 0;
        MapTranslate.Y = 0;
    }

    #region 드래그 이벤트 핸들러

    private void MapViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (NoMapPanel.Visibility == Visibility.Visible) return;

        _isDragging = true;
        _dragStartPoint = e.GetPosition(MapViewerGrid);
        _dragStartTranslateX = MapTranslate.X;
        _dragStartTranslateY = MapTranslate.Y;
        MapViewerGrid.CaptureMouse();
        MapCanvas.Cursor = Cursors.ScrollAll;
    }

    private void MapViewer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            MapViewerGrid.ReleaseMouseCapture();
            MapCanvas.Cursor = Cursors.Arrow;
        }
    }

    private void MapViewer_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = e.GetPosition(MapViewerGrid);
        var deltaX = currentPoint.X - _dragStartPoint.X;
        var deltaY = currentPoint.Y - _dragStartPoint.Y;

        MapTranslate.X = _dragStartTranslateX + deltaX;
        MapTranslate.Y = _dragStartTranslateY + deltaY;
    }

    private void MapViewer_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 마우스 위치를 중심으로 줌
        var mousePos = e.GetPosition(MapCanvas);
        var oldZoom = _zoomLevel;

        // 줌 계산
        var zoomFactor = e.Delta > 0 ? 1.15 : 0.87;
        var newZoom = Math.Clamp(_zoomLevel * zoomFactor, MinZoom, MaxZoom);

        if (Math.Abs(newZoom - oldZoom) < 0.001) return;

        // 마우스 위치 기준 줌
        var scaleChange = newZoom / oldZoom;

        // 현재 마우스 위치의 실제 캔버스 좌표
        var canvasX = (mousePos.X - MapTranslate.X / oldZoom);
        var canvasY = (mousePos.Y - MapTranslate.Y / oldZoom);

        // 새로운 오프셋 계산 (마우스 위치가 고정되도록)
        MapTranslate.X -= canvasX * (scaleChange - 1) * oldZoom;
        MapTranslate.Y -= canvasY * (scaleChange - 1) * oldZoom;

        SetZoom(newZoom);
        e.Handled = true;
    }

    #endregion

    #endregion

    #region 맵/마커 관련 메서드

    private void LoadMapImage(string mapKey)
    {
        var config = _trackerService?.GetMapConfig(mapKey);
        if (config == null)
        {
            ShowNoMapPanel(true);
            return;
        }

        var imagePath = config.ImagePath;

        // 상대 경로인 경우 앱 디렉토리 기준으로 변환
        if (!System.IO.Path.IsPathRooted(imagePath))
        {
            imagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imagePath);
        }

        if (!File.Exists(imagePath))
        {
            ShowNoMapPanel(true);
            return;
        }

        try
        {
            var extension = System.IO.Path.GetExtension(imagePath).ToLowerInvariant();

            if (extension == ".svg")
            {
                // SVG 전처리: CSS 클래스를 인라인 스타일로 변환
                MapSvg.Visibility = Visibility.Collapsed;
                MapImage.Visibility = Visibility.Visible;

                var pngImage = ConvertSvgToPngWithPreprocessing(imagePath, config.ImageWidth, config.ImageHeight);
                if (pngImage != null)
                {
                    MapImage.Source = pngImage;
                    MapImage.Stretch = Stretch.None;
                    MapImage.Width = config.ImageWidth;
                    MapImage.Height = config.ImageHeight;

                    MapCanvas.Width = config.ImageWidth;
                    MapCanvas.Height = config.ImageHeight;
                    Canvas.SetLeft(MapImage, 0);
                    Canvas.SetTop(MapImage, 0);
                }
                else
                {
                    // 폴백: SvgViewbox 사용
                    MapImage.Visibility = Visibility.Collapsed;
                    MapSvg.Visibility = Visibility.Visible;
                    MapSvg.Source = new Uri(imagePath, UriKind.Absolute);
                    MapCanvas.Width = config.ImageWidth;
                    MapCanvas.Height = config.ImageHeight;
                    MapSvg.Width = config.ImageWidth;
                    MapSvg.Height = config.ImageHeight;
                    Canvas.SetLeft(MapSvg, 0);
                    Canvas.SetTop(MapSvg, 0);
                }
            }
            else
            {
                // 비트맵 이미지 로드 (PNG, JPG 등)
                MapSvg.Visibility = Visibility.Collapsed;
                MapImage.Visibility = Visibility.Visible;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                MapImage.Source = bitmap;
                MapCanvas.Width = bitmap.PixelWidth;
                MapCanvas.Height = bitmap.PixelHeight;

                // 이미지는 (0,0)에 위치
                Canvas.SetLeft(MapImage, 0);
                Canvas.SetTop(MapImage, 0);
            }

            ShowNoMapPanel(false);
        }
        catch
        {
            ShowNoMapPanel(true);
        }
    }

    private void ShowNoMapPanel(bool show)
    {
        NoMapPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        // 맵이 없을 때는 둘 다 숨김, 있을 때는 LoadMapImage에서 관리
        if (show)
        {
            MapImage.Visibility = Visibility.Collapsed;
            MapSvg.Visibility = Visibility.Collapsed;
        }
    }

    private void LoadCurrentMapSettings()
    {
        if (string.IsNullOrEmpty(_currentMapKey))
        {
            TxtCurrentMapName.Text = "(맵 선택 필요)";
            return;
        }

        var config = _trackerService?.GetMapConfig(_currentMapKey);
        if (config == null) return;

        TxtCurrentMapName.Text = config.DisplayName;
        TxtWorldMinX.Text = config.WorldMinX.ToString(CultureInfo.InvariantCulture);
        TxtWorldMaxX.Text = config.WorldMaxX.ToString(CultureInfo.InvariantCulture);
        TxtWorldMinY.Text = config.WorldMinY.ToString(CultureInfo.InvariantCulture);
        TxtWorldMaxY.Text = config.WorldMaxY.ToString(CultureInfo.InvariantCulture);
        ChkInvertY.IsChecked = config.InvertY;
        ChkInvertX.IsChecked = config.InvertX;
    }

    private void UpdateMarkerPosition(ScreenPosition position)
    {
        // 현재 선택된 맵과 다른 경우 맵 전환
        if (!string.Equals(_currentMapKey, position.MapKey, StringComparison.OrdinalIgnoreCase))
        {
            // 맵 선택 변경
            for (int i = 0; i < CmbMapSelect.Items.Count; i++)
            {
                if (CmbMapSelect.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Tag as string, position.MapKey, StringComparison.OrdinalIgnoreCase))
                {
                    CmbMapSelect.SelectedIndex = i;
                    break;
                }
            }
        }

        var showDirection = (_trackerService?.Settings.ShowDirection ?? true) && position.Angle.HasValue;

        if (showDirection)
        {
            PlayerMarker.Visibility = Visibility.Visible;
            PlayerDot.Visibility = Visibility.Collapsed;

            // 마커 위치 설정 (Canvas 중심 기준)
            MarkerTranslation.X = position.X;
            MarkerTranslation.Y = position.Y;

            // 방향 화살표 회전 (화살표만 회전, 중심 원은 고정)
            MarkerRotation.Angle = position.Angle ?? 0;
        }
        else
        {
            PlayerMarker.Visibility = Visibility.Collapsed;
            PlayerDot.Visibility = Visibility.Visible;

            // 원형 마커 위치 (Canvas 중심 기준)
            DotTranslation.X = position.X;
            DotTranslation.Y = position.Y;
        }
    }

    private void UpdateTrailPath()
    {
        if (_trackerService == null) return;
        if (!_trackerService.Settings.ShowTrail) return;

        TrailPath.Points.Clear();
        foreach (var pos in _trackerService.TrailPositions)
        {
            TrailPath.Points.Add(new Point(pos.X, pos.Y));
        }
    }

    private void UpdateCoordinatesDisplay(ScreenPosition position)
    {
        var orig = position.OriginalPosition;
        if (orig != null)
        {
            var angleStr = orig.Angle.HasValue ? $", Angle: {orig.Angle:F1}°" : "";
            TxtCoordinates.Text = $"Map: {orig.MapName}, X: {orig.X:F2}, Y: {orig.Y:F2}{angleStr}";
        }
        else
        {
            TxtCoordinates.Text = $"X: {position.X:F0}, Y: {position.Y:F0}";
        }

        TxtLastUpdate.Text = $"마지막 업데이트: {DateTime.Now:HH:mm:ss}";
    }

    private void UpdateMarkerSize(int size)
    {
        // 새 마커 디자인은 Canvas 기반이므로 ScaleTransform으로 크기 조절
        // 기본 크기(16)를 기준으로 스케일 계산
        var scale = size / 16.0;

        // PlayerMarker와 PlayerDot에 스케일 적용
        if (PlayerMarker.RenderTransform is TranslateTransform)
        {
            // 스케일 트랜스폼 추가 필요시 여기서 처리
            // 현재는 XAML에서 고정 크기 사용
        }
    }

    private void UpdateMarkerVisibility()
    {
        if (_trackerService == null) return;
        var current = _trackerService.CurrentPosition;
        if (current == null)
        {
            PlayerMarker.Visibility = Visibility.Collapsed;
            PlayerDot.Visibility = Visibility.Collapsed;
            return;
        }

        var showDirection = _trackerService.Settings.ShowDirection && current.Angle.HasValue;
        PlayerMarker.Visibility = showDirection ? Visibility.Visible : Visibility.Collapsed;
        PlayerDot.Visibility = showDirection ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetZoom(double zoom)
    {
        // 줌 범위 제한
        _zoomLevel = Math.Clamp(zoom, MinZoom, MaxZoom);
        MapScale.ScaleX = _zoomLevel;
        MapScale.ScaleY = _zoomLevel;

        // 콤보박스 텍스트 업데이트 (이벤트 트리거 방지)
        CmbZoomLevel.SelectionChanged -= CmbZoomLevel_SelectionChanged;
        CmbZoomLevel.Text = $"{_zoomLevel * 100:F0}%";
        CmbZoomLevel.SelectionChanged += CmbZoomLevel_SelectionChanged;
    }

    /// <summary>
    /// SVG 파일을 전처리(CSS 클래스→인라인 스타일 변환) 후 BitmapSource로 변환합니다.
    /// </summary>
    private BitmapSource? ConvertSvgToPngWithPreprocessing(string svgPath, int width, int height)
    {
        try
        {
            // 1. SVG 전처리: CSS 클래스를 인라인 스타일로 변환
            var preprocessor = new SvgStylePreprocessor();
            var processedSvg = preprocessor.ProcessSvgFile(svgPath);

            // 2. 전처리된 SVG를 렌더링
            return RenderSvgContent(processedSvg, width, height);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// SVG 콘텐츠 문자열을 BitmapSource로 렌더링합니다.
    /// </summary>
    private BitmapSource? RenderSvgContent(string svgContent, int width, int height)
    {
        try
        {
            var settings = new WpfDrawingSettings
            {
                IncludeRuntime = true,
                TextAsGeometry = false,
                OptimizePath = true,
                CultureInfo = CultureInfo.InvariantCulture,
                EnsureViewboxSize = false,
                EnsureViewboxPosition = false,
                IgnoreRootViewbox = false
            };

            // 문자열에서 SVG 읽기
            DrawingGroup? drawing;
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svgContent)))
            {
                var converter = new FileSvgReader(settings);
                drawing = converter.Read(stream);
            }

            if (drawing == null)
                return null;

            var bounds = drawing.Bounds;

            // DrawingVisual로 렌더링 - viewBox 크기에 맞춰 정확히 렌더링
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // viewBox 좌표계를 그대로 사용 (0,0 기준)
                // bounds.X, bounds.Y가 0이 아닐 수 있으므로 원점으로 이동
                drawingContext.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
                drawingContext.DrawDrawing(drawing);
                drawingContext.Pop();
            }

            // RenderTargetBitmap으로 변환 - viewBox 크기 사용
            var renderTarget = new RenderTargetBitmap(
                (int)Math.Ceiling(bounds.Width),
                (int)Math.Ceiling(bounds.Height),
                96,
                96,
                PixelFormats.Pbgra32);

            renderTarget.Render(drawingVisual);
            renderTarget.Freeze();

            return renderTarget;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region 퀘스트 목표 마커

    private async Task LoadQuestObjectivesAsync()
    {
        try
        {
            TxtStatus.Text = "Loading quest objectives...";

            await _objectiveService.EnsureLoadedAsync(msg =>
            {
                Dispatcher.Invoke(() => TxtStatus.Text = msg);
            });

            var count = _objectiveService.AllObjectives.Count;
            TxtStatus.Text = $"Loaded {count} quest objectives";

            if (!string.IsNullOrEmpty(_currentMapKey))
            {
                RefreshQuestMarkers();
            }
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error loading objectives: {ex.Message}";
        }
    }

    private void OnQuestProgressChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(RefreshQuestMarkers);
    }

    private void OnObjectiveProgressChanged(object? sender, ObjectiveProgressChangedEventArgs e)
    {
        Dispatcher.Invoke(RefreshQuestMarkers);
    }

    private void ChkShowQuestMarkers_Changed(object sender, RoutedEventArgs e)
    {
        _showQuestMarkers = ChkShowQuestMarkers?.IsChecked ?? true;
        if (QuestMarkersContainer != null)
        {
            QuestMarkersContainer.Visibility = _showQuestMarkers ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void RefreshQuestMarkers()
    {
        if (string.IsNullOrEmpty(_currentMapKey)) return;
        if (!_objectiveService.IsLoaded) return;

        // 기존 마커 제거
        ClearQuestMarkers();

        if (!_showQuestMarkers) return;

        // 맵 설정 가져오기
        var config = _trackerService?.GetMapConfig(_currentMapKey);
        if (config == null) return;

        // 현재 맵의 활성 퀘스트 목표 가져오기 (별칭 포함하여 검색)
        var mapNamesToSearch = new List<string> { _currentMapKey };
        if (config.Aliases != null)
        {
            mapNamesToSearch.AddRange(config.Aliases);
        }
        // 표시 이름도 추가
        if (!string.IsNullOrEmpty(config.DisplayName))
        {
            mapNamesToSearch.Add(config.DisplayName);
        }

        _currentMapObjectives = new List<TaskObjectiveWithLocation>();
        foreach (var mapName in mapNamesToSearch)
        {
            var objectives = _objectiveService.GetActiveObjectivesForMap(mapName, _progressService);
            foreach (var obj in objectives)
            {
                if (!_currentMapObjectives.Any(o => o.ObjectiveId == obj.ObjectiveId))
                {
                    _currentMapObjectives.Add(obj);
                }
            }
        }

        TxtStatus.Text = $"Found {_currentMapObjectives.Count} active objectives for {_currentMapKey}";

        foreach (var objective in _currentMapObjectives)
        {
            foreach (var location in objective.Locations)
            {
                // tarkov.dev API 좌표를 화면 좌표로 변환 (Transform 배열 사용)
                // API position: x, y(높이), z → tarkov.dev 방식: [z, x]
                if (_trackerService != null &&
                    _trackerService.TransformApiCoordinate(_currentMapKey, location.X, location.Y, location.Z) is ScreenPosition screenPos)
                {
                    var marker = CreateQuestMarker(objective, location, screenPos);
                    _questMarkerElements.Add(marker);
                    QuestMarkersContainer.Children.Add(marker);
                }
            }
        }
    }

    private FrameworkElement CreateQuestMarker(TaskObjectiveWithLocation objective, QuestObjectiveLocation location, ScreenPosition screenPos)
    {
        var markerColor = (Color)ColorConverter.ConvertFromString(objective.MarkerColor);
        var markerBrush = new SolidColorBrush(markerColor);
        var glowBrush = new SolidColorBrush(Color.FromArgb(64, markerColor.R, markerColor.G, markerColor.B));

        var canvas = new Canvas
        {
            Width = 0,
            Height = 0,
            Tag = objective
        };

        // 외곽 글로우
        var glow = new Ellipse
        {
            Width = 24,
            Height = 24,
            Fill = glowBrush
        };
        Canvas.SetLeft(glow, -12);
        Canvas.SetTop(glow, -12);
        canvas.Children.Add(glow);

        // 중심 원
        var center = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = markerBrush,
            Stroke = Brushes.White,
            StrokeThickness = 2
        };
        Canvas.SetLeft(center, -7);
        Canvas.SetTop(center, -7);
        canvas.Children.Add(center);

        // 완료 상태 표시 (체크마크)
        if (objective.IsCompleted)
        {
            var checkMark = new TextBlock
            {
                Text = "✓",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            Canvas.SetLeft(checkMark, -5);
            Canvas.SetTop(checkMark, -7);
            canvas.Children.Add(checkMark);

            // 완료된 마커는 반투명
            canvas.Opacity = 0.5;
        }

        // 위치 설정
        Canvas.SetLeft(canvas, screenPos.X);
        Canvas.SetTop(canvas, screenPos.Y);

        // 클릭 이벤트
        canvas.MouseLeftButtonDown += QuestMarker_Click;
        canvas.Cursor = Cursors.Hand;

        // 툴팁
        var tooltip = _loc.CurrentLanguage == AppLanguage.KO && !string.IsNullOrEmpty(objective.DescriptionKo)
            ? objective.DescriptionKo
            : objective.Description;
        var questName = _loc.CurrentLanguage == AppLanguage.KO && !string.IsNullOrEmpty(objective.TaskNameKo)
            ? objective.TaskNameKo
            : objective.TaskName;
        canvas.ToolTip = $"{questName}\n{tooltip}";

        return canvas;
    }

    private void ClearQuestMarkers()
    {
        foreach (var marker in _questMarkerElements)
        {
            if (marker is Canvas c)
            {
                c.MouseLeftButtonDown -= QuestMarker_Click;
            }
        }
        _questMarkerElements.Clear();
        QuestMarkersContainer.Children.Clear();
    }

    private void QuestMarker_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is TaskObjectiveWithLocation objective)
        {
            ShowQuestDrawer(objective);
            e.Handled = true;
        }
    }

    #endregion

    #region 퀘스트 Drawer

    private void ShowQuestDrawer(TaskObjectiveWithLocation? selectedObjective = null)
    {
        // Drawer 열기
        QuestDrawerColumn.Width = new GridLength(320);
        QuestDrawerPanel.Visibility = Visibility.Visible;

        // 현재 맵의 모든 활성 목표를 표시
        var viewModels = _currentMapObjectives.Select(obj => new QuestObjectiveViewModel(obj, _loc)).ToList();
        QuestObjectivesList.ItemsSource = viewModels;

        // 선택된 목표가 있으면 스크롤
        if (selectedObjective != null)
        {
            var selectedVm = viewModels.FirstOrDefault(vm => vm.Objective == selectedObjective);
            // ItemsControl에서는 스크롤 처리가 복잡하므로 생략
        }
    }

    private void BtnCloseQuestDrawer_Click(object sender, RoutedEventArgs e)
    {
        CloseQuestDrawer();
    }

    private void CloseQuestDrawer()
    {
        QuestDrawerColumn.Width = new GridLength(0);
        QuestDrawerPanel.Visibility = Visibility.Collapsed;
    }

    private void QuestObjectiveItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element &&
            element.DataContext is QuestObjectiveViewModel vm)
        {
            // 해당 퀘스트를 QuestListPage에서 열도록 이벤트 발생
            // 또는 해당 마커 위치로 맵 이동
            CenterOnObjective(vm.Objective);
        }
    }

    private void CenterOnObjective(TaskObjectiveWithLocation objective)
    {
        if (_trackerService == null || string.IsNullOrEmpty(_currentMapKey)) return;

        // 첫 번째 위치로 이동
        var location = objective.Locations.FirstOrDefault(l =>
            l.MapName.Equals(_currentMapKey, StringComparison.OrdinalIgnoreCase));

        if (location == null) return;

        // tarkov.dev API 좌표를 화면 좌표로 변환
        var screenPos = _trackerService.TransformApiCoordinate(_currentMapKey, location.X, location.Y, location.Z);
        if (screenPos == null) return;

        // 맵 중심으로 이동
        var viewerWidth = MapViewerGrid.ActualWidth;
        var viewerHeight = MapViewerGrid.ActualHeight;

        MapTranslate.X = viewerWidth / 2 - screenPos.X * _zoomLevel;
        MapTranslate.Y = viewerHeight / 2 - screenPos.Y * _zoomLevel;
    }

    #endregion
}

/// <summary>
/// 퀘스트 목표 표시용 ViewModel
/// </summary>
public class QuestObjectiveViewModel
{
    public TaskObjectiveWithLocation Objective { get; }

    public string QuestName { get; }
    public string Description { get; }
    public string TypeDisplay { get; }
    public Brush TypeBrush { get; }
    public Visibility CompletedVisibility { get; }

    public QuestObjectiveViewModel(TaskObjectiveWithLocation objective, LocalizationService loc)
    {
        Objective = objective;

        QuestName = loc.CurrentLanguage == AppLanguage.KO && !string.IsNullOrEmpty(objective.TaskNameKo)
            ? objective.TaskNameKo
            : objective.TaskName;

        Description = loc.CurrentLanguage == AppLanguage.KO && !string.IsNullOrEmpty(objective.DescriptionKo)
            ? objective.DescriptionKo
            : objective.Description;

        TypeDisplay = GetTypeDisplay(objective.Type);
        TypeBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(objective.MarkerColor));
        CompletedVisibility = objective.IsCompleted ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string GetTypeDisplay(string type) => type switch
    {
        "visit" => "Visit",
        "mark" => "Mark",
        "plantItem" => "Plant",
        "extract" => "Extract",
        "findItem" => "Find",
        _ => type
    };
}

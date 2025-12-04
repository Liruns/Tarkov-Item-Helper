using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
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
            System.Diagnostics.Debug.WriteLine($"[MapTrackerPage] Constructor error: {ex}");
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

    private void MapTrackerPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadSettings();
            PopulateMapComboBox();
            UpdateUI();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapTrackerPage] Loaded error: {ex}");
            MessageBox.Show($"MapTrackerPage load error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MapTrackerPage_Unloaded(object sender, RoutedEventArgs e)
    {
        // 페이지 언로드 시 정리 (서비스는 유지)
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
            LoadMapImage(mapKey);
            LoadCurrentMapSettings();
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
            MapCanvas.Cursor = Cursors.Hand;
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
                // SVG 파일 로드
                MapImage.Visibility = Visibility.Collapsed;
                MapSvg.Visibility = Visibility.Visible;
                MapSvg.Source = new Uri(imagePath, UriKind.Absolute);

                // SVG viewBox 크기 사용
                MapCanvas.Width = config.ImageWidth;
                MapCanvas.Height = config.ImageHeight;
                MapSvg.Width = config.ImageWidth;
                MapSvg.Height = config.ImageHeight;
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
            }

            ShowNoMapPanel(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapTrackerPage] LoadMapImage error: {ex}");
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
        _zoomLevel = Math.Clamp(zoom, MinZoom, MaxZoom);
        MapScale.ScaleX = _zoomLevel;
        MapScale.ScaleY = _zoomLevel;

        // 콤보박스 텍스트 업데이트 (이벤트 트리거 방지)
        CmbZoomLevel.SelectionChanged -= CmbZoomLevel_SelectionChanged;
        CmbZoomLevel.Text = $"{_zoomLevel * 100:F0}%";
        CmbZoomLevel.SelectionChanged += CmbZoomLevel_SelectionChanged;
    }

    #endregion
}

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Interop;
using ResSwitcher.Core;

namespace ResSwitcher.Ui;

/// <summary>
/// WPF 悬浮按钮：圆角长方形双分区（左=切主屏，右=切分辨率）。
/// AllowsTransparency + 矢量渲染，边缘干净；支持拖拽、透明度渐变、右键菜单、位置记忆。
/// 通过回调注入解耦。
/// </summary>
public sealed class OverlayWindow : Window
{
    private const int DragThresholdPx = 4;
    private const int DefaultEdgeMargin = 16;

    private readonly Action _onToggleRes;
    private readonly Action _onTogglePrimary;
    private readonly Action _onOpenSettings;
    private readonly Action _onExit;
    private readonly Action _onConfigDirty;

    private AppConfig _config;
    private Border _root = null!;
    private Grid _leftZone = null!;
    private Grid _rightZone = null!;

    // 拖拽状态
    private Point _dragStartCursor;
    private Point _dragStartForm;
    private bool _isDragging;
    private bool _movedBeyondThreshold;

    public OverlayWindow(AppConfig config,
        Action onToggleRes, Action onTogglePrimary,
        Action onOpenSettings, Action onExit, Action onConfigDirty)
    {
        _config = config;
        _onToggleRes = onToggleRes;
        _onTogglePrimary = onTogglePrimary;
        _onOpenSettings = onOpenSettings;
        _onExit = onExit;
        _onConfigDirty = onConfigDirty;

        SetupWindow();
        ApplyAppearance();
    }

    // ---- 窗体基础 ----

    private void SetupWindow()
    {
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        SizeToContent = SizeToContent.Manual;
        SourceInitialized += (_, _) =>
        {
            DisplayApi.ConfigureToolWindow(new WindowInteropHelper(this).Handle);
            RestoreOrPlaceDefault();
        };

        // 右键菜单（WPF ContextMenu）
        var menu = new ContextMenu();
        var miSettings = new MenuItem { Header = "设置…" };
        var miExit = new MenuItem { Header = "退出" };
        miSettings.Click += (_, _) => _onOpenSettings();
        miExit.Click += (_, _) => _onExit();
        menu.Items.Add(miSettings);
        menu.Items.Add(miExit);
        ContextMenu = menu;
    }

    private void BuildUi()
    {
        int size = Math.Clamp(_config.Button.Size, 24, 128);

        _root = new Border
        {
            Margin = new Thickness(10),
            CornerRadius = new CornerRadius(10),
            Background = MakeBackground(),
            BorderBrush = MakeBorderBrush(),
            BorderThickness = new Thickness(1.2),
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 2,
                Opacity = 0.45,
                Color = Colors.Black
            }
        };
        _root.MouseEnter += OnRootMouseEnter;
        _root.MouseLeave += OnRootMouseLeave;

        var grid = new Grid { Width = size * 2, Height = size };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _leftZone = MakeZone(SwapIconGeometry(), "切换主显示器");
        Grid.SetColumn(_leftZone, 0);
        _rightZone = MakeZone(ResIconGeometry(), "切换分辨率");
        Grid.SetColumn(_rightZone, 1);
        Logger.Info($"图标几何: 左Bounds={SwapIconGeometry().Bounds}, 右Bounds={ResIconGeometry().Bounds}");

        // 拖拽/点击事件绑定到两个分区
        foreach (var zone in new[] { _leftZone, _rightZone })
        {
            zone.MouseDown += OnZoneMouseDown;
            zone.MouseMove += OnZoneMouseMove;
            zone.MouseUp += OnZoneMouseUp;
            AttachHoverHighlight(zone);
        }

        // 中缝分割线
        var divider = new System.Windows.Shapes.Rectangle
        {
            Width = 1.2,
            Fill = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, size * 0.18, 0, size * 0.18)
        };
        Grid.SetColumn(divider, 1);
        divider.HorizontalAlignment = HorizontalAlignment.Left;
        divider.Margin = new Thickness(-0.6, size * 0.18, 0, size * 0.18);

        grid.Children.Add(_leftZone);
        grid.Children.Add(_rightZone);
        grid.Children.Add(divider);
        _root.Child = grid;
        Content = _root;

        Opacity = _config.Button.IdleAlpha;
    }

    private Grid MakeZone(Geometry icon, string tooltip)
    {
        var zone = new Grid { Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = tooltip };
        var path = new System.Windows.Shapes.Path
        {
            Data = icon,
            Fill = Brushes.White,
            Stretch = Stretch.Uniform,
            Width = 22,          // 显式尺寸：NaN + Max 组合在 Grid 中不可靠
            Height = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        zone.Children.Add(path);
        return zone;
    }

    /// <summary>悬停时高亮分区背景（由 XAML Trigger 语义简化为代码事件）。</summary>
    private void AttachHoverHighlight(Grid zone)
    {
        var original = zone.Background;
        zone.MouseEnter += (_, _) => zone.Background = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255));
        zone.MouseLeave += (_, _) => zone.Background = original;
    }

    private void OnRootMouseEnter(object sender, MouseEventArgs e)
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 1.0;
    }

    private void OnRootMouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
            AnimateOpacity(_config.Button.IdleAlpha);
    }

    // ---- 图标几何 ----

    /// <summary>左区：显示器图标（屏幕+底座）。</summary>
    private static Geometry SwapIconGeometry()
    {
        var geo = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var ctx = geo.Open())
        {
            // 外框
            ctx.BeginFigure(new Point(2, 4), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(22, 4), true, true);
            ctx.LineTo(new Point(22, 15), true, true);
            ctx.LineTo(new Point(2, 15), true, true);
            // 内部镂空
            ctx.BeginFigure(new Point(4, 6), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(20, 6), true, true);
            ctx.LineTo(new Point(20, 13), true, true);
            ctx.LineTo(new Point(4, 13), true, true);
            // 底座立柱
            ctx.BeginFigure(new Point(10.5, 15), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(13.5, 15), true, true);
            ctx.LineTo(new Point(13.5, 18), true, true);
            ctx.LineTo(new Point(10.5, 18), true, true);
            // 底座横条
            ctx.BeginFigure(new Point(7, 19), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(17, 19), true, true);
            ctx.LineTo(new Point(17, 20.5), true, true);
            ctx.LineTo(new Point(7, 20.5), true, true);
        }
        return geo;
    }

    /// <summary>右区：双箭头（⇄）。改用 GeometryGroup 双 PathFigure，避免 StreamGeometry 填充规则问题。</summary>
    private static Geometry ResIconGeometry()
    {
        // 上箭头（指向左）：矩形杆 + 三角头
        var upper = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var ctx = upper.Open())
        {
            ctx.BeginFigure(new Point(4, 7), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(17, 7), true, true);
            ctx.LineTo(new Point(17, 4.5), true, true);
            ctx.LineTo(new Point(22, 8.5), true, true);
            ctx.LineTo(new Point(17, 12.5), true, true);
            ctx.LineTo(new Point(17, 10), true, true);
            ctx.LineTo(new Point(4, 10), true, true);
        }
        // 下箭头（指向右）
        var lower = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var ctx = lower.Open())
        {
            ctx.BeginFigure(new Point(20, 14), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(7, 14), true, true);
            ctx.LineTo(new Point(7, 11.5), true, true);
            ctx.LineTo(new Point(2, 15.5), true, true);
            ctx.LineTo(new Point(7, 19.5), true, true);
            ctx.LineTo(new Point(7, 17), true, true);
            ctx.LineTo(new Point(20, 17), true, true);
        }
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        group.Children.Add(upper);
        group.Children.Add(lower);
        return group;
    }

    // ---- 外观 ----

    private Brush MakeBackground()
    {
        var c = ParseColor(_config.Button.Color);
        return new LinearGradientBrush(
            Color.FromRgb((byte)Math.Min(255, c.R + 40), (byte)Math.Min(255, c.G + 40), (byte)Math.Min(255, c.B + 40)),
            Color.FromRgb((byte)(c.R * 0.72), (byte)(c.G * 0.72), (byte)(c.B * 0.72)),
            90);
    }

    private Brush MakeBorderBrush()
    {
        var c = ParseColor(_config.Button.Color);
        return new SolidColorBrush(Color.FromRgb((byte)(c.R * 0.55), (byte)(c.G * 0.55), (byte)(c.B * 0.55)));
    }

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Color.FromRgb(59, 130, 246); }
    }

    private void ApplyAppearance()
    {
        int size = Math.Clamp(_config.Button.Size, 24, 128);
        Width = size * 2 + 20;
        Height = size + 20;
        BuildUi();
    }

    // ---- 位置记忆 ----

    private void RestoreOrPlaceDefault()
    {
        try
        {
            bool hasRecord = _config.Button.X != ButtonCfg.NoPosition && _config.Button.Y != ButtonCfg.NoPosition;
            Logger.Info($"恢复位置: X={_config.Button.X}, Y={_config.Button.Y}, hasRecord={hasRecord}");

            if (hasRecord)
            {
                Left = _config.Button.X - 10;  // 补偿阴影余量
                Top = _config.Button.Y - 10;
            }
            else
            {
                PlaceDefault();
            }
            ClampToScreens();
            Logger.Info($"最终位置: Left={Left}, Top={Top}");
        }
        catch (Exception ex)
        {
            Logger.Error("恢复位置失败，使用默认位置", ex);
            PlaceDefault();
        }
    }

    private void PlaceDefault()
    {
        // 主屏工作区右上角（通过 Win32 枚举，Core 层提供几何查询）
        var area = GetPrimaryWorkArea();
        Left = area.Right - Width + 10 - DefaultEdgeMargin;
        Top = area.Top + DefaultEdgeMargin - 10;
    }

    /// <summary>主屏工作区（WPF 设备无关单位 = 物理像素 / DPI 缩放）。</summary>
    private (double Left, double Top, double Right, double Bottom) GetPrimaryWorkArea()
    {
        double scale = GetDpiScale();
        string primary = MonitorTarget.GetPrimaryDeviceName();
        var (x, y, w, h) = DisplayApi.GetMonitorBounds(primary);
        return (x / scale, y / scale, (x + w) / scale, (y + h) / scale);
    }

    private void ClampToScreens()
    {
        // 用物理像素比对（保存的坐标即物理像素），避免 DPI 换算误差
        // 按钮区域：Left/Top 是 WPF 单位（含 10px 阴影余量），转回物理像素
        double scale = GetDpiScale();
        var r = new Rect((Left + 10) * scale, (Top + 10) * scale,
                         (Width - 20) * scale, (Height - 20) * scale);
        bool visible = false;
        foreach (var m in DisplayApi.EnumerateMonitors())
        {
            try
            {
                var (x, y, w, h) = DisplayApi.GetMonitorBounds(m.DeviceName);
                var bounds = new Rect(x, y, w, h);
                Logger.Info($"钳制检查: 按钮=({r.Left},{r.Top},{r.Right},{r.Bottom}) vs {m.DeviceName}=({bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom}) → {r.IntersectsWith(bounds)}");
                if (r.IntersectsWith(bounds)) { visible = true; break; }
            }
            catch (Exception ex)
            {
                Logger.Warn($"钳制检查跳过显示器 {m.DeviceName}: {ex.Message}");
            }
        }
        if (!visible)
        {
            Logger.Warn("按钮位置完全出屏，重置为默认位置");
            PlaceDefault();
        }
    }

    private double GetDpiScale()
    {
        var src = PresentationSource.FromVisual(this);
        return src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
    }

    // ---- 拖拽 vs 点击 ----

    private void OnZoneMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        _isDragging = true;
        _movedBeyondThreshold = false;
        _dragStartCursor = PointToScreen(e.GetPosition(this));
        _dragStartForm = new Point(Left, Top);
        ((UIElement)sender).CaptureMouse();
    }

    private void OnZoneMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed) return;

        var cur = PointToScreen(e.GetPosition(this));
        var dx = cur.X - _dragStartCursor.X;
        var dy = cur.Y - _dragStartCursor.Y;
        if (Math.Abs(dx) > DragThresholdPx || Math.Abs(dy) > DragThresholdPx)
        {
            if (!_movedBeyondThreshold)
            {
                BeginAnimation(OpacityProperty, null);
                Opacity = 1.0;
            }
            _movedBeyondThreshold = true;
        }

        if (_movedBeyondThreshold)
        {
            Left = _dragStartForm.X + dx;
            Top = _dragStartForm.Y + dy;
        }
    }

    private void OnZoneMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !_isDragging) return;
        _isDragging = false;
        ((UIElement)sender).ReleaseMouseCapture();

        if (!_movedBeyondThreshold)
        {
            if (sender == _leftZone) _onTogglePrimary();
            else _onToggleRes();
        }
        else
        {
            _config.Button.X = (int)(Left + 10);
            _config.Button.Y = (int)(Top + 10);
            _onConfigDirty();
        }
        AnimateOpacity(_config.Button.IdleAlpha);
    }

    // ---- 透明度动画 ----

    private void AnimateOpacity(double to)
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(600))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    // ---- 配置热更新 ----

    public void ApplyConfig(AppConfig config)
    {
        _config = config;
        ApplyAppearance();
        ClampToScreens();
        AnimateOpacity(config.Button.IdleAlpha);
    }

    internal void OpenSettingsFromTray()
    {
        _onOpenSettings();
    }

    internal void ExitFromTray()
    {
        _onExit();
    }

    internal void ApplyPrimaryShift((int X, int Y)? shift)
    {
        if (shift is not { } offset || (offset.X == 0 && offset.Y == 0))
            return;

        double scale = GetDpiScale();
        Left += offset.X / scale;
        Top += offset.Y / scale;
        _config.Button.X = (int)(Left + 10);
        _config.Button.Y = (int)(Top + 10);
        _onConfigDirty();
        Logger.Info($"主屏切换后保持悬浮窗物理位置: 偏移=({offset.X},{offset.Y}), 新位置=({_config.Button.X},{_config.Button.Y})");
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            _config.Button.X = (int)(Left + 10);
            _config.Button.Y = (int)(Top + 10);
            _onConfigDirty();
            Logger.Info($"退出保存位置: X={_config.Button.X}, Y={_config.Button.Y}");
        }
        catch (Exception ex)
        {
            Logger.Error("退出保存位置失败", ex);
        }
        base.OnClosed(e);
    }
}

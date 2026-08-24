using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ResSwitcher.Core;

namespace ResSwitcher.Ui;

public sealed class SettingsWindow : Window
{
    private readonly AppConfig _config;
    private readonly List<int[]> _collection = [];
    private readonly List<DisplayDeviceInfo> _monitors = [];
    private readonly Dictionary<string, List<int[]>> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _profileNames = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<Resolution> _supported = [];
    private string? _activeProfileId;
    private bool _loading;
    private sealed record MonitorOption(string DeviceName, string Label, string Identity);

    private CheckBox _autostart = null!;
    private ComboBox _monitor = null!;
    private ComboBox _preset = null!;
    private Slider _alpha = null!;
    private TextBlock _alphaValue = null!;
    private Button _color = null!;
    private TextBox _size = null!;
    private ListBox _resolutions = null!;
    private bool _refreshingPresets;

    public bool Confirmed { get; private set; }

    public SettingsWindow(AppConfig config)
    {
        _config = config;
        Title = "ResSwitcher 设置";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = Brush("#F7F8FA");
        FontFamily = new FontFamily("Segoe UI Variable Text");
        FontSize = 13;
        UseLayoutRounding = true;
        BuildUi();
        LoadValues();
    }

    private void BuildUi()
    {
        var root = new StackPanel { Margin = new Thickness(28, 24, 28, 20) };
        root.Children.Add(new TextBlock
        {
            Text = "ResSwitcher 设置",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#172033")
        });
        root.Children.Add(new TextBlock
        {
            Text = "调整悬浮按钮与显示器切换行为",
            Foreground = Brush("#687386"),
            Margin = new Thickness(0, 3, 0, 18)
        });

        _autostart = new CheckBox
        {
            Content = "开机自动启动",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#243047"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        root.Children.Add(_autostart);
        root.Children.Add(Row("目标显示器", BuildMonitorControl()));
        root.Children.Add(Row("按钮大小", BuildSizeControl()));
        root.Children.Add(Row("静止透明度", BuildAlphaControl()));
        root.Children.Add(Row("按钮颜色", BuildColorControl()));
        root.Children.Add(BuildResolutionEditor());

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var ok = ActionButton("确定", true);
        ok.IsDefault = true;
        ok.Margin = new Thickness(0, 0, 8, 0);
        var cancel = ActionButton("取消");
        cancel.IsCancel = true;
        ok.Click += (_, _) => SaveAndClose();
        actions.Children.Add(ok);
        actions.Children.Add(cancel);
        root.Children.Add(actions);
        Content = root;
    }

    private static StackPanel Row(string label, UIElement control)
    {
        var row = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = Brush("#687386"),
            Margin = new Thickness(0, 0, 0, 4)
        });
        row.Children.Add(control);
        return row;
    }

    private ComboBox BuildMonitorControl()
    {
        _monitor = new ComboBox { Height = 32, MinWidth = 300 };
        _monitor.SelectionChanged += (_, _) =>
        {
            if (_loading)
                return;

            SaveActiveProfile();
            LoadActiveProfile();
            RefreshPresets();
        };
        return _monitor;
    }

    private StackPanel BuildSizeControl()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        _size = new TextBox { Width = 72, Height = 32, Padding = new Thickness(7, 5, 7, 5) };
        panel.Children.Add(_size);
        panel.Children.Add(new TextBlock
        {
            Text = "  px（24–128）",
            Foreground = Brush("#687386"),
            VerticalAlignment = VerticalAlignment.Center
        });
        return panel;
    }

    private StackPanel BuildAlphaControl()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        _alpha = new Slider
        {
            Minimum = 10,
            Maximum = 100,
            Width = 240,
            Height = 28,
            TickFrequency = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        _alpha.ValueChanged += (_, _) => _alphaValue.Text = $"{_alpha.Value / 100:0.00}";
        _alphaValue = new TextBlock
        {
            Width = 42,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush("#334155")
        };
        panel.Children.Add(_alpha);
        panel.Children.Add(_alphaValue);
        return panel;
    }

    private StackPanel BuildColorControl()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        _color = new Button
        {
            Width = 42,
            Height = 32,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(3),
            ToolTip = "点击更换颜色"
        };
        _color.Click += (_, _) => PickColor();
        panel.Children.Add(_color);
        return panel;
    }

    private UIElement BuildResolutionEditor()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        panel.Children.Add(new TextBlock
        {
            Text = "当前显示器的分辨率切换顺序",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#243047")
        });
        panel.Children.Add(new TextBlock
        {
            Text = "每块显示器单独保存列表；不支持的已配置项目保留但灰显，多个项目按列表循环。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#687386"),
            Margin = new Thickness(0, 3, 0, 6)
        });
        _resolutions = new ListBox { MinHeight = 64, MaxHeight = 118 };
        panel.Children.Add(_resolutions);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
        var remove = ActionButton("删除选中");
        remove.Width = 88;
        remove.Margin = new Thickness(0, 0, 8, 0);
        remove.Click += (_, _) =>
        {
            if (_resolutions.SelectedItem is ListBoxItem { Tag: int index })
            {
                _collection.RemoveAt(index);
                RefreshResolutionList();
            }
        };
        var clear = ActionButton("清空");
        clear.Width = 66;
        clear.Click += (_, _) => { _collection.Clear(); RefreshResolutionList(); };
        buttons.Children.Add(remove);
        buttons.Children.Add(clear);
        panel.Children.Add(buttons);

        panel.Children.Add(new TextBlock
        {
            Text = "从支持列表添加",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#243047"),
            Margin = new Thickness(0, 12, 0, 4)
        });
        _preset = new ComboBox { Height = 32 };
        _preset.SelectionChanged += (_, _) => AddPreset();
        panel.Children.Add(_preset);
        return panel;
    }

    private void LoadValues()
    {
        _loading = true;
        _autostart.IsChecked = _config.Autostart;
        _size.Text = Math.Clamp(_config.Button.Size, 24, 128).ToString();
        _alpha.Value = Math.Clamp(_config.Button.IdleAlpha, 0.1, 1.0) * 100;
        _color.Background = ParseBrush(_config.Button.Color);

        _monitors.AddRange(DisplayApi.EnumerateMonitors());
        var options = new List<MonitorOption>
        {
            new(MonitorTarget.Auto, "自动（当前主显示器）", GetPrimaryMonitor().Identity)
        };
        options.AddRange(_monitors.Select(m => new MonitorOption(m.DeviceName, m.FriendlyName, m.Identity)));
        _monitor.ItemsSource = options;
        _monitor.DisplayMemberPath = nameof(MonitorOption.Label);
        _monitor.SelectedIndex = Math.Max(0, options.FindIndex(o =>
            string.Equals(o.DeviceName, _config.Monitor, StringComparison.OrdinalIgnoreCase)));

        foreach (var profile in _config.MonitorProfiles ?? [])
        {
            if (string.IsNullOrWhiteSpace(profile.DisplayId))
                continue;
            _profiles[profile.DisplayId] = CloneItems(profile.Items);
            _profileNames[profile.DisplayId] = profile.DisplayName;
        }

        // 旧版只有一份全局列表，首次打开时迁移到当时选中的物理显示器。
        if (_profiles.Count == 0)
        {
            var legacy = CloneItems(_config.Collection?.Items);
            if (legacy.Count == 0 && _config.Single is { Width: > 0, Height: > 0 })
                legacy.Add([_config.Single.Width, _config.Single.Height]);
            var target = GetSelectedPhysicalMonitor();
            _profiles[target.Identity] = legacy;
            _profileNames[target.Identity] = target.FriendlyName;
        }

        _loading = false;
        LoadActiveProfile();
        RefreshPresets();
    }

    private void RefreshPresets()
    {
        _refreshingPresets = true;
        try
        {
            _preset.ItemsSource = _supported.ToList();
            _preset.SelectedIndex = -1;
        }
        finally
        {
            _refreshingPresets = false;
        }
    }

    private void AddPreset()
    {
        if (_refreshingPresets || _preset.SelectedItem is not Resolution selected ||
            _collection.Any(item => item[0] == selected.Width && item[1] == selected.Height))
            return;
        _collection.Add([selected.Width, selected.Height]);
        RefreshResolutionList();
        _preset.SelectedIndex = -1;
    }

    private void RefreshResolutionList()
    {
        _resolutions.Items.Clear();
        for (int i = 0; i < _collection.Count; i++)
        {
            var resolution = new Resolution(_collection[i][0], _collection[i][1]);
            bool supported = _supported.Contains(resolution);
            _resolutions.Items.Add(new ListBoxItem
            {
                Content = supported
                    ? $"{i + 1}.  {resolution}"
                    : $"{i + 1}.  {resolution}（当前显示器不支持）",
                Tag = i,
                IsEnabled = supported,
                Foreground = supported ? Brush("#334155") : Brush("#9CA3AF"),
                ToolTip = supported ? null : "当前显示器不支持此分辨率，切换显示器后可能可用"
            });
        }
    }

    private void PickColor()
    {
        Color[] palette =
        [
            Color.FromRgb(59, 130, 246), Color.FromRgb(16, 185, 129),
            Color.FromRgb(239, 68, 68), Color.FromRgb(245, 158, 11),
            Color.FromRgb(236, 72, 153), Color.FromRgb(30, 41, 59),
            Color.FromRgb(255, 255, 255)
        ];
        var current = ((SolidColorBrush)_color.Background).Color;
        int index = Array.FindIndex(palette, color => color == current);
        var next = palette[(index + 1) % palette.Length];
        _color.Background = new SolidColorBrush(next);
        _color.ToolTip = $"当前颜色 #{next.R:X2}{next.G:X2}{next.B:X2}，点击更换";
    }

    private void SaveAndClose()
    {
        if (!int.TryParse(_size.Text, out int size) || size is < 24 or > 128)
        {
            MessageBox.Show(this, "按钮大小必须是 24 到 128 之间的整数。", "输入无效",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveActiveProfile();
        string selectedDevice = GetSelectedDeviceName();
        _config.Autostart = _autostart.IsChecked == true;
        _config.Monitor = selectedDevice;
        _config.Button.Size = size;
        _config.Button.IdleAlpha = _alpha.Value / 100;
        var color = ((SolidColorBrush)_color.Background).Color;
        _config.Button.Color = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        _config.Collection.Items = _collection.Select(item => (int[])item.Clone()).ToList();
        _config.MonitorProfiles = _profiles.Select(pair => new MonitorProfile
        {
            DisplayId = pair.Key,
            DisplayName = _profileNames.TryGetValue(pair.Key, out var name) ? name : pair.Key,
            Items = CloneItems(pair.Value)
        }).ToList();
        Confirmed = true;
        Close();
    }

    private string GetSelectedDeviceName() =>
        (_monitor.SelectedItem as MonitorOption)?.DeviceName ?? MonitorTarget.Auto;

    private DisplayDeviceInfo GetSelectedPhysicalMonitor()
    {
        string device = GetSelectedDeviceName();
        if (device == MonitorTarget.Auto)
            device = MonitorTarget.GetPrimaryDeviceName();
        return _monitors.FirstOrDefault(m =>
                   string.Equals(m.DeviceName, device, StringComparison.OrdinalIgnoreCase))
               ?? new DisplayDeviceInfo(device, device, device);
    }

    private DisplayDeviceInfo GetPrimaryMonitor() =>
        _monitors.FirstOrDefault(m =>
            string.Equals(m.DeviceName, MonitorTarget.GetPrimaryDeviceName(), StringComparison.OrdinalIgnoreCase))
        ?? new DisplayDeviceInfo(MonitorTarget.GetPrimaryDeviceName(), "当前主显示器", MonitorTarget.GetPrimaryDeviceName());

    private void LoadActiveProfile()
    {
        var monitor = GetSelectedPhysicalMonitor();
        _activeProfileId = monitor.Identity;
        if (!_profiles.TryGetValue(_activeProfileId, out var items))
        {
            items = [];
            _profiles[_activeProfileId] = items;
            _profileNames[_activeProfileId] = monitor.FriendlyName;
        }

        _collection.Clear();
        _collection.AddRange(CloneItems(items));
        _supported = DisplayApi.GetSupportedResolutions(monitor.DeviceName).ToHashSet();
        RefreshResolutionList();
    }

    private void SaveActiveProfile()
    {
        if (string.IsNullOrWhiteSpace(_activeProfileId))
            return;
        _profiles[_activeProfileId] = CloneItems(_collection);
    }

    private static List<int[]> CloneItems(IEnumerable<int[]>? items) =>
        (items ?? []).Where(IsValidResolution)
            .Select(item => new[] { item[0], item[1] })
            .DistinctBy(item => (item[0], item[1]))
            .ToList();

    private static bool IsValidResolution(int[]? item) =>
        item is { Length: >= 2 } && item[0] > 0 && item[1] > 0;

    private static Button ActionButton(string text, bool primary = false) => new()
    {
        Content = text,
        Width = 88,
        Height = 32,
        Padding = new Thickness(12, 4, 12, 4),
        Background = primary ? Brush("#2563EB") : Brushes.White,
        Foreground = primary ? Brushes.White : Brush("#334155"),
        BorderBrush = primary ? Brush("#2563EB") : Brush("#CBD5E1"),
        BorderThickness = new Thickness(1),
        FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal
    };

    private static SolidColorBrush ParseBrush(string? hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!); }
        catch { return Brush("#3B82F6"); }
    }

    private static SolidColorBrush Brush(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex)!);
}
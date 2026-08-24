namespace ResSwitcher.Core;

/// <summary>切换结果。</summary>
public enum SwitchResult
{
    /// <summary>切换成功（含目标即当前的幂等情形）。</summary>
    Success,

    /// <summary>尚未配置可切换的分辨率。</summary>
    NotConfigured,

    /// <summary>目标分辨率不被目标显示器支持。</summary>
    UnsupportedResolution,

    /// <summary>Win32 API 调用失败。</summary>
    ApiFailed
}

/// <summary>主屏切换结果（TogglePrimary 用）。</summary>
public enum SwitcherResult
{
    Success,
    ApiFailed
}

/// <summary>
/// 切换目标显示器解析策略：
/// "auto" = 每次点击时自动使用主显示器；否则为固定设备名（如 "\\.\DISPLAY1"）。
/// </summary>
public static class MonitorTarget
{
    public const string Auto = "auto";

    /// <summary>解析实际设备名：auto → 主显示器；无效值 → 回退主显示器。</summary>
    public static string Resolve(string configValue)
    {
        if (string.Equals(configValue, Auto, StringComparison.OrdinalIgnoreCase))
            return GetPrimaryDeviceName();

        if (!string.IsNullOrWhiteSpace(configValue) &&
            DisplayApi.EnumerateMonitors().Any(m =>
                string.Equals(m.DeviceName, configValue, StringComparison.OrdinalIgnoreCase)))
            return configValue;

        return GetPrimaryDeviceName();
    }

    /// <summary>通过 Win32 枚举取主屏设备名（DisplayApi 提供权威实现）。</summary>
    public static string GetPrimaryDeviceName()
        => DisplayApi.GetPrimaryDeviceName();

    /// <summary>从 Screen 推导 Win32 设备名（兼容旧调用）。</summary>
    public static string ToDeviceName(string deviceName) => deviceName;
}

/// <summary>
/// 分辨率切换状态机：封装单/双两种模式的点击切换逻辑。
/// 纯逻辑类，不引用 WinForms 控件；对 DisplayApi 的访问走可注入委托以便测试。
/// </summary>
public sealed class ResolutionSwitcher
{
    // 可注入委托：默认指向真实 DisplayApi，测试中替换为 fake
    internal Func<string, Resolution> _getCurrent = DisplayApi.GetCurrentResolution;
    internal Func<string, List<Resolution>> _getSupported = DisplayApi.GetSupportedResolutions;
    internal Func<string, Resolution, bool> _trySet = DisplayApi.TrySetResolution;
    internal Func<string, string> _resolveMonitor = MonitorTarget.Resolve;
    internal Func<string> _getPrimary = DisplayApi.GetPrimaryDeviceName;
    internal Func<List<DisplayDeviceInfo>> _enumerateMonitors = DisplayApi.EnumerateMonitors;
    internal Func<string, bool> _trySetPrimary = DisplayApi.TrySetPrimaryMonitor;

    private readonly AppConfig _config;

    /// <summary>单模式下首次点击采样的原始分辨率；null 表示尚未采样。</summary>
    private Resolution? _original;

    /// <summary>最近一次切换后实际生效的分辨率。</summary>
    public Resolution? CurrentResolution { get; private set; }

    /// <summary>最近一次失败的可诊断信息，供 UI 展示；成功操作会清空。</summary>
    public string? LastError { get; private set; }

    /// <summary>最近一次主屏切换造成的虚拟桌面坐标偏移，供悬浮窗保持物理位置。</summary>
    internal (int X, int Y)? LastPrimaryShift { get; private set; }

    public ResolutionSwitcher(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 点击悬浮按钮时调用。读取当前实际分辨率 → 决定目标 → 切换 → 用实际结果更新状态。
    /// </summary>
    public SwitchResult Toggle()
    {
        LastError = null;
        // auto 模式：每次点击时解析主显示器，跟随系统主屏变化
        string device;
        try
        {
            device = _resolveMonitor(_config.Monitor);
        }
        catch (Exception ex)
        {
            LastError = Describe("无法解析目标显示器", ex);
            return SwitchResult.ApiFailed;
        }

        Resolution current;
        try
        {
            current = _getCurrent(device);
        }
        catch (Exception ex)
        {
            LastError = Describe("无法读取目标显示器的当前分辨率", ex);
            return SwitchResult.ApiFailed;
        }

        // 先读取当前显示器支持列表，再把 profile 中不支持的项目视为不存在。
        List<Resolution> supported;
        try
        {
            supported = _getSupported(device);
        }
        catch (Exception ex)
        {
            LastError = Describe("无法读取显示器支持的分辨率", ex);
            return SwitchResult.ApiFailed;
        }

        if (supported.Count == 0 && DisplayApi.LastError is not null)
        {
            LastError = DisplayApi.LastError;
            return SwitchResult.ApiFailed;
        }

        Resolution? target = ComputeTarget(current, device, supported);

        // 无配置或当前显示器没有可用项目
        if (target == null)
        {
            CurrentResolution = current;
            LastError = "尚未配置当前显示器可用的分辨率。";
            return SwitchResult.NotConfigured;
        }

        // 幂等：目标即当前，不调用 API
        if (target == current)
        {
            CurrentResolution = current;
            return SwitchResult.Success;
        }

        // 执行切换
        bool changed;
        try
        {
            changed = _trySet(device, target);
        }
        catch (Exception ex)
        {
            LastError = Describe("调用分辨率切换 API 失败", ex);
            return SwitchResult.ApiFailed;
        }

        if (!changed)
        {
            LastError = DisplayApi.LastError ?? $"系统拒绝将显示器切换为 {target}。";
            return SwitchResult.ApiFailed;
        }

        // 以系统反馈为准：再读一次实际生效的分辨率
        try
        {
            CurrentResolution = _getCurrent(device);
        }
        catch (Exception ex)
        {
            LastError = Describe("切换请求已发送，但无法确认系统当前分辨率", ex);
            return SwitchResult.ApiFailed;
        }

        return SwitchResult.Success;
    }

    /// <summary>配置被设置界面修改后调用，重置内部状态。</summary>
    public void OnConfigChanged()
    {
        _original = null;
        LastError = null;
    }

    /// <summary>
    /// 在当前主显示器与另一块活动显示器之间切换。与分辨率目标设置相互独立。
    /// </summary>
    public SwitcherResult TogglePrimary()
    {
        LastError = null;
        LastPrimaryShift = null;
        string primary;
        try
        {
            primary = _getPrimary();
        }
        catch (Exception ex)
        {
            LastError = Describe("无法解析主显示器", ex);
            return SwitcherResult.ApiFailed;
        }

        // 主屏切换与分辨率设置解耦：始终把当前主屏之外的第一块活动显示器设为主屏。
        string target;
        try
        {
            var other = _enumerateMonitors().FirstOrDefault(m =>
                !string.Equals(m.DeviceName, primary, StringComparison.OrdinalIgnoreCase));
            if (other is null)
            {
                LastError = "当前只检测到一个活动显示器，无法切换主屏。";
                return SwitcherResult.ApiFailed;
            }
            target = other.DeviceName;
        }
        catch (Exception ex)
        {
            LastError = Describe("无法枚举活动显示器", ex);
            return SwitcherResult.ApiFailed;
        }

        try
        {
            if (_trySetPrimary(target))
            {
                LastPrimaryShift = DisplayApi.LastPrimaryShift;
                return SwitcherResult.Success;
            }
        }
        catch (Exception ex)
        {
            LastError = Describe("调用主屏切换 API 失败", ex);
            return SwitcherResult.ApiFailed;
        }

        LastError = DisplayApi.LastError ?? $"系统拒绝将 {target} 设为主屏。";
        return SwitcherResult.ApiFailed;
    }

    /// <summary>
    /// 计算目标分辨率（合集轮询语义）：
    /// - 合集为空：无目标（保持当前）。
    /// - 合集只有 1 项：current == 该项 ? original : 该项（同旧单模式）。
    /// - 合集 ≥2 项：current 在合集中 → 切到下一项（循环）；不在 → 切到第 1 项。
    /// </summary>
    private Resolution? ComputeTarget(Resolution current, string device, IReadOnlyCollection<Resolution> supported)
    {
        var items = GetConfiguredItems(device)
            .Where(a => a is { Length: >= 2 } && a[0] > 0 && a[1] > 0)
            .Select(a => new Resolution(a[0], a[1]))
            .Where(supported.Contains)
            .Distinct()
            .ToList();

        if (items.Count == 0)
            return null; // 无配置：不切换

        if (items.Count == 1)
        {
            var target = items[0];
            EnsureOriginal(current, target, supported);
            return current == target ? _original! : target;
        }

        // ≥2 项：轮询
        int idx = items.FindIndex(r => r == current);
        if (idx >= 0)
            return items[(idx + 1) % items.Count];
        return items[0]; // 不在合集中 → 切到第 1 项
    }

    /// <summary>采样 original（仅合集单项时使用）。</summary>
    private void EnsureOriginal(Resolution current, Resolution target, IReadOnlyCollection<Resolution> supported)
    {
        if (_original is not null)
            return;

        if (current != target)
        {
            _original = current;
            return;
        }

        // 启动时已在 target：回退为支持列表中像素数最大的其他模式
        var candidates = supported
            .Where(r => r != target)
            .OrderByDescending(r => (long)r.Width * r.Height)
            .ToList();

        _original = candidates.Count > 0 ? candidates[0] : current;
    }

    private List<int[]> GetConfiguredItems(string device)
    {
        if (_config.MonitorProfiles is not { Count: > 0 })
            return _config.Collection?.Items ?? [];

        DisplayDeviceInfo? monitor = null;
        try
        {
            monitor = _enumerateMonitors().FirstOrDefault(m =>
                string.Equals(m.DeviceName, device, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            // 设备枚举失败时仍允许用 DISPLAY 名匹配旧 profile。
        }

        var keys = new[] { monitor?.Identity, monitor?.DeviceId, monitor?.DeviceName, monitor?.FriendlyName, device }
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var profile = _config.MonitorProfiles.FirstOrDefault(candidate =>
            keys.Any(key => string.Equals(candidate.DisplayId, key, StringComparison.OrdinalIgnoreCase)));
        return profile?.Items ?? [];
    }

    private static string Describe(string message, Exception exception) =>
        $"{message}；异常: {exception.GetType().Name}: {exception.Message}";
}

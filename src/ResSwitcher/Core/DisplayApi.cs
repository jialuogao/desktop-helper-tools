using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ResSwitcher.Core;

/// <summary>显示器设备信息。DeviceName 形如 "\\.\DISPLAY1"。</summary>
public sealed record DisplayDeviceInfo(string DeviceName, string FriendlyName, string DeviceId = "")
{
    /// <summary>配置 profile 使用的稳定键；取不到设备 ID 时回退设备名。</summary>
    public string Identity => string.IsNullOrWhiteSpace(DeviceId) ? DeviceName : DeviceId;
}

/// <summary>分辨率（像素）。</summary>
public sealed record Resolution(int Width, int Height)
{
    public override string ToString() => $"{Width} × {Height}";
}

/// <summary>
/// Win32 显示器封装：枚举显示器、枚举支持分辨率、读取当前分辨率、切换分辨率。
/// 本文件是全部 P/Invoke 的唯一位置。
/// </summary>
public static class DisplayApi
{
    // ---- Win32 常量 ----
    private const int CDS_UPDATEREGISTRY = 0x01;
    private const int CDS_SET_PRIMARY = 0x10;
    private const int DISP_CHANGE_SUCCESSFUL = 0;
    private const int DM_PELSWIDTH = 0x80000;
    private const int DM_PELSHEIGHT = 0x100000;
    private const int DM_POSITION = 0x20;
    private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    private const uint QDC_VIRTUAL_MODE_AWARE = 0x00000010;
    private const uint SDC_APPLY = 0x00000080;
    private const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
    private const uint SDC_SAVE_TO_DATABASE = 0x00000200;
    private const uint SDC_VALIDATE = 0x00000040;
    private const uint SDC_ALLOW_CHANGES = 0x00000400;
    private const uint SDC_VIRTUAL_MODE_AWARE = 0x00008000;
    private const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xFFFFFFFF;
    private const uint DISPLAYCONFIG_PATH_SOURCE_MODE_IDX_INVALID = 0x0000FFFF;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const int IDI_APPLICATION = 32512;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_APPWINDOW = 0x00040000;
    private const uint MF_STRING = 0x00000000;
    private const uint TPM_RIGHTBUTTON = 0x00000002;
    private const uint TPM_NONOTIFY = 0x00000080;
    private const uint TPM_RETURNCMD = 0x00000100;
    private const uint WM_NULL = 0x00000000;
    private const uint TraySettingsCommand = 1;
    private const uint TrayExitCommand = 2;
    private const int ERROR_SUCCESS = 0;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;
    private const uint DISPLAY_DEVICE_ACTIVE = 0x01;
    internal static int DevmodeSizeForTests => Marshal.SizeOf<DEVMODEW>();

    internal const int TrayCallbackMessage = 0x8001;

    internal static (int X, int Y, int Fields) PreparePrimaryMode(int currentX, int currentY, int fields)
        => (0, 0, DM_POSITION);

    internal static (int X, int Y) CalculatePrimaryShift(int targetX, int targetY)
        => (-targetX, -targetY);

    /// <summary>最近一次显示 API 失败的可诊断信息；成功调用会清空。</summary>
    public static string? LastError { get; private set; }

    // ---- P/Invoke（仅本文件允许出现）----

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevicesW(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICEW lpDev, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettingsW(string lpszDeviceName, int iModeNum, ref DEVMODEW lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsExW(string? lpszDeviceName, ref DEVMODEW lpDevMode, IntPtr hwndParent, uint dwFlags, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenuEx(
        IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out POINTL point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFOEXW info);

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    internal static bool AddTrayIcon(IntPtr windowHandle, uint iconId, string tooltip)
    {
        var data = CreateTrayIconData(windowHandle, iconId, tooltip);
        return Shell_NotifyIconW(NIM_ADD, ref data);
    }

    internal static void RemoveTrayIcon(IntPtr windowHandle, uint iconId)
    {
        var data = CreateTrayIconData(windowHandle, iconId, string.Empty);
        Shell_NotifyIconW(NIM_DELETE, ref data);
    }

    internal static void ConfigureToolWindow(IntPtr windowHandle)
    {
        long style = GetWindowLongPtrW(windowHandle, GWL_EXSTYLE).ToInt64();
        style = (style | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        SetWindowLongPtrW(windowHandle, GWL_EXSTYLE, new IntPtr(style));
    }

    internal static uint ShowTrayContextMenu(IntPtr ownerWindowHandle)
    {
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
            return 0;

        try
        {
            if (!AppendMenuW(menu, MF_STRING, (UIntPtr)TraySettingsCommand, "设置…") ||
                !AppendMenuW(menu, MF_STRING, (UIntPtr)TrayExitCommand, "退出"))
                return 0;

            GetCursorPos(out var cursor);
            SetForegroundWindow(ownerWindowHandle);
            uint command = TrackPopupMenuEx(
                menu,
                TPM_RIGHTBUTTON | TPM_NONOTIFY | TPM_RETURNCMD,
                cursor.x,
                cursor.y,
                ownerWindowHandle,
                IntPtr.Zero);

            // 通知区域菜单必须在跟踪结束后收到一条空消息，系统才会完成失活收尾。
            PostMessageW(ownerWindowHandle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            return command;
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int SetDisplayConfig(
        uint numPathArrayElements,
        [In] DISPLAYCONFIG_PATH_INFO[] pathArray,
        uint numModeInfoArrayElements,
        [In] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        uint flags);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

    // ---- 公开 API（签名固定，后续任务依赖）----

    /// <summary>枚举所有活动显示器。</summary>
    public static List<DisplayDeviceInfo> EnumerateMonitors()
    {
        LastError = null;
        var result = new List<DisplayDeviceInfo>();
        try
        {
            for (uint i = 0; ; i++)
            {
                var dev = new DISPLAY_DEVICEW();
                dev.cb = (uint)Marshal.SizeOf<DISPLAY_DEVICEW>();
                if (!EnumDisplayDevicesW(null, i, ref dev, 0))
                    break;

                // 仅保留活动显示器，跳过镜像与未连接设备
                if ((dev.dwFlags & DISPLAY_DEVICE_ACTIVE) == 0)
                    continue;

                string deviceName = dev.DeviceName ?? string.Empty;
                result.Add(new DisplayDeviceInfo(deviceName, GetFriendlyName(deviceName), GetMonitorDeviceId(deviceName)));
            }
        }
        catch (Exception ex)
        {
            SetError("枚举显示器失败", ex);
        }

        return result;
    }

    /// <summary>读取指定显示器当前分辨率（iModeNum = -1，即 ENUM_CURRENT_SETTINGS）。</summary>
    public static Resolution GetCurrentResolution(string deviceName)
    {
        LastError = null;
        var dm = CreateDevmode();
        if (!EnumDisplaySettingsW(deviceName, -1, ref dm))
        {
            SetError($"无法读取显示器 {deviceName} 的当前分辨率");
            throw new InvalidOperationException(LastError);
        }

        return new Resolution(dm.dmPelsWidth, dm.dmPelsHeight);
    }

    /// <summary>枚举指定显示器支持的分辨率：去重，按像素数降序。</summary>
    public static List<Resolution> GetSupportedResolutions(string deviceName)
    {
        LastError = null;
        var seen = new HashSet<(int W, int H)>();
        var list = new List<Resolution>();

        try
        {
            for (int iModeNum = 0; ; iModeNum++)
            {
                var dm = CreateDevmode();
                if (!EnumDisplaySettingsW(deviceName, iModeNum, ref dm))
                    break; // 返回 false：没有更多模式

                int w = dm.dmPelsWidth;
                int h = dm.dmPelsHeight;
                if (w <= 0 || h <= 0)
                    continue;

                if (seen.Add((w, h)))
                    list.Add(new Resolution(w, h));
            }
        }
        catch (Exception ex)
        {
            SetError($"读取显示器 {deviceName} 支持的分辨率失败", ex);
        }

        if (list.Count == 0)
            SetError($"显示器 {deviceName} 没有返回任何可用分辨率");

        // 按像素数降序
        return list.OrderByDescending(r => (long)r.Width * r.Height).ToList();
    }

    /// <summary>切换分辨率：先校验支持列表，不支持返回 false；成功 true。任何失败均不抛异常。</summary>
    public static bool TrySetResolution(string deviceName, Resolution res)
    {
        LastError = null;
        try
        {
            // 必须先查支持列表再切换
            var supported = GetSupportedResolutions(deviceName);
            if (supported.Count == 0 && LastError is not null)
                return false;
            if (!supported.Contains(res))
            {
                SetError($"分辨率 {res} 不在显示器 {deviceName} 的支持列表中");
                return false;
            }

            if (TrySetDisplayConfigResolution(deviceName, res))
                return true;

            string modernError = LastError ?? "现代显示配置 API 未能应用请求";

            var dm = CreateDevmode();
            if (!EnumDisplaySettingsW(deviceName, -1, ref dm))
            {
                SetError($"无法读取显示器 {deviceName} 的当前显示模式");
                return false;
            }

            dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT; // 只切换宽高
            dm.dmPelsWidth = res.Width;
            dm.dmPelsHeight = res.Height;

            int hr = ChangeDisplaySettingsExW(deviceName, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
            if (hr != DISP_CHANGE_SUCCESSFUL)
                SetError($"现代显示配置 API 失败：{modernError}；系统拒绝将 {deviceName} 切换为 {res}，ChangeDisplaySettingsExW 返回 {hr}: {DescribeChangeResult(hr)}");
            return hr == DISP_CHANGE_SUCCESSFUL;
        }
        catch (Exception ex)
        {
            SetError($"调用分辨率切换 API 失败 ({deviceName}, {res})", ex);
            return false;
        }
    }

    /// <summary>读取指定显示器当前几何位置（虚拟屏幕坐标，用于识别主屏：原点 0,0）。</summary>
    public static (int X, int Y) GetMonitorGeometry(string deviceName)
    {
        var geo = GetMonitorBounds(deviceName);
        return (geo.X, geo.Y);
    }

    /// <summary>读取显示器完整边界（物理像素）：位置 + 尺寸。基于 EnumDisplayMonitors，几何信息可靠。</summary>
    public static (int X, int Y, int Width, int Height) GetMonitorBounds(string deviceName)
    {
        try
        {
            (int X, int Y, int W, int H)? found = null;
            MonitorEnumProc cb = (hMon, _, ref rect, _) =>
            {
                var info = new MONITORINFOEXW();
                info.cbSize = Marshal.SizeOf<MONITORINFOEXW>();
                if (GetMonitorInfoW(hMon, ref info) &&
                    string.Equals(info.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    found = (info.rcMonitor.Left, info.rcMonitor.Top,
                             info.rcMonitor.Right - info.rcMonitor.Left,
                             info.rcMonitor.Bottom - info.rcMonitor.Top);
                    return false; // 找到即停
                }
                return true;
            };
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);

            if (found.HasValue)
                return found.Value;

            // 回退：几何未知时用设备名对应的分辨率，位置按 (0,0)
            var res = GetCurrentResolution(deviceName);
            return (0, 0, res.Width, res.Height);
        }
        catch (Exception ex)
        {
            SetError($"读取显示器 {deviceName} 的几何信息失败", ex);
            var res = GetCurrentResolution(deviceName);
            return (0, 0, res.Width, res.Height);
        }
    }

    /// <summary>
    /// 将指定显示器设为主显示器（其当前分辨率与位置不变，原主屏退为扩展屏）。
    /// 原理：以目标屏当前几何信息 + DM_POSITION 标志调用 ChangeDisplaySettingsExW,
    /// 并配合 CDS_SET_PRIMARY（0x10）标志。仅多屏时有意义。
    /// </summary>
    public static bool TrySetPrimaryMonitor(string deviceName)
    {
        LastError = null;
        LastPrimaryShift = null;
        try
        {
            var monitors = EnumerateMonitors();
            if (monitors.Count < 2)
            {
                SetError("当前只检测到一个活动显示器，无法切换主屏");
                return false; // 单屏无需切换
            }

            if (!monitors.Any(m => string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase)))
            {
                SetError($"找不到目标显示器 {deviceName}");
                return false;
            }

            if (TrySetDisplayConfigPrimary(deviceName, out var configShift))
            {
                LastPrimaryShift = configShift;
                return true;
            }

            string modernError = LastError ?? "现代显示配置 API 未能应用请求";

            var dm = CreateDevmode();
            if (!EnumDisplaySettingsW(deviceName, -1, ref dm))
            {
                SetError($"无法读取目标显示器 {deviceName} 的当前显示模式");
                return false;
            }

            var primaryShift = CalculatePrimaryShift(dm.dmPositionX, dm.dmPositionY);
            var primaryMode = PreparePrimaryMode(dm.dmPositionX, dm.dmPositionY, dm.dmFields);
            dm.dmPositionX = primaryMode.X;
            dm.dmPositionY = primaryMode.Y;
            dm.dmFields = primaryMode.Fields;

            // Windows 会随主屏变更整体平移虚拟桌面，保持其他显示器的相对布局。
            int hr = ChangeDisplaySettingsExW(deviceName, ref dm, IntPtr.Zero,
                CDS_UPDATEREGISTRY | CDS_SET_PRIMARY, IntPtr.Zero);
            if (hr != DISP_CHANGE_SUCCESSFUL)
                SetError($"现代显示配置 API 失败：{modernError}；系统拒绝将 {deviceName} 设为主屏，ChangeDisplaySettingsExW 返回 {hr}: {DescribeChangeResult(hr)}");
            if (hr == DISP_CHANGE_SUCCESSFUL)
                LastPrimaryShift = primaryShift;
            return hr == DISP_CHANGE_SUCCESSFUL;
        }
        catch (Exception ex)
        {
            SetError($"调用主屏切换 API 失败 ({deviceName})", ex);
            return false;
        }
    }

    /// <summary>最近一次成功切换主屏造成的虚拟桌面坐标偏移（物理像素）。</summary>
    internal static (int X, int Y)? LastPrimaryShift { get; private set; }

    internal static List<(int X, int Y)> RebasePositions(
        IReadOnlyList<(int X, int Y)> positions, int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= positions.Count)
            throw new ArgumentOutOfRangeException(nameof(targetIndex));

        var shift = CalculatePrimaryShift(positions[targetIndex].X, positions[targetIndex].Y);
        return positions
            .Select(position => (position.X + shift.X, position.Y + shift.Y))
            .ToList();
    }

    /// <summary>主屏设备名：MONITORINFOF_PRIMARY 标志（比"原点 0,0"更权威）。</summary>
    public static string GetPrimaryDeviceName()
    {
        LastError = null;
        try
        {
            string? primary = null;
            MonitorEnumProc cb = (hMon, _, ref rect, _) =>
            {
                var info = new MONITORINFOEXW();
                info.cbSize = Marshal.SizeOf<MONITORINFOEXW>();
                if (GetMonitorInfoW(hMon, ref info) && (info.dwFlags & MONITORINFOF_PRIMARY) != 0)
                {
                    primary = info.DeviceName;
                    return false;
                }
                return true;
            };
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);
            return primary ?? EnumerateMonitors().FirstOrDefault()?.DeviceName ?? "\\\\.\\DISPLAY1";
        }
        catch (Exception ex)
        {
            SetError("读取主显示器失败", ex);
            return EnumerateMonitors().FirstOrDefault()?.DeviceName ?? "\\\\.\\DISPLAY1";
        }
    }

    private static void SetError(string message, Exception? exception = null)
    {
        LastError = exception is null
            ? message
            : $"{message}；异常: {exception.GetType().Name}: {exception.Message}";
        if (exception is null)
            Logger.Warn(message);
        else
            Logger.Error(message, exception);
        Debug.WriteLine($"[ResSwitcher] {LastError}");
    }

    private static string DescribeChangeResult(int result) => result switch
    {
        1 => "需要重启系统",
        -1 => "驱动拒绝了请求",
        -2 => "显示模式不受支持",
        -3 => "设置未更新",
        -4 => "标志参数无效",
        -5 => "参数无效",
        -6 => "多显示器模式不支持",
        _ => "未知系统错误"
    };

    private static bool TrySetDisplayConfigResolution(string deviceName, Resolution resolution)
    {
        if (!TryQueryActiveDisplayConfig(out var paths, out var modes))
            return false;

        int pathIndex = FindPathForDevice(paths, deviceName);
        if (pathIndex < 0)
        {
            SetError($"现代显示配置 API 找不到显示器 {deviceName} 的活动路径");
            return false;
        }

        if (!TryResolveSourceModeIndex(paths[pathIndex], modes, out uint modeIndex))
        {
            SetError($"现代显示配置 API 找不到显示器 {deviceName} 的 source mode");
            return false;
        }

        paths[pathIndex].sourceInfo.modeInfoIdx = EncodeSourceModeIndex(
            paths[pathIndex].sourceInfo.modeInfoIdx, modeIndex);
        modes[modeIndex].sourceMode.width = (uint)resolution.Width;
        modes[modeIndex].sourceMode.height = (uint)resolution.Height;
        return TryApplyDisplayConfig(paths, modes, $"将 {deviceName} 切换为 {resolution}");
    }

    private static bool TrySetDisplayConfigPrimary(
        string deviceName, out (int X, int Y) shift)
    {
        shift = default;
        if (!TryQueryActiveDisplayConfig(out var paths, out var modes))
            return false;

        int targetPathIndex = FindPathForDevice(paths, deviceName);
        if (targetPathIndex < 0)
        {
            SetError($"现代显示配置 API 找不到显示器 {deviceName} 的活动路径");
            return false;
        }

        if (!NormalizeSourceModeIndices(paths, modes))
            return false;

        uint targetModeIndex = GetSourceModeIndex(paths[targetPathIndex].sourceInfo.modeInfoIdx);
        var sourceModeIndices = paths
            .Select(path => GetSourceModeIndex(path.sourceInfo.modeInfoIdx))
            .Distinct()
            .ToList();
        int targetSourceIndex = sourceModeIndices.IndexOf(targetModeIndex);
        if (targetSourceIndex < 0)
        {
            SetError($"现代显示配置 API 找不到显示器 {deviceName} 的 source mode");
            return false;
        }

        var positions = sourceModeIndices
            .Select(index => (
                X: modes[index].sourceMode.position.x,
                Y: modes[index].sourceMode.position.y))
            .ToList();
        var originalTargetPosition = positions[targetSourceIndex];
        var rebasedPositions = RebasePositions(positions, targetSourceIndex);
        for (int i = 0; i < sourceModeIndices.Count; i++)
        {
            modes[sourceModeIndices[i]].sourceMode.position.x = rebasedPositions[i].X;
            modes[sourceModeIndices[i]].sourceMode.position.y = rebasedPositions[i].Y;
        }

        var orderedPaths = paths
            .Skip(targetPathIndex)
            .Concat(paths.Take(targetPathIndex))
            .ToArray();
        if (!TryApplyDisplayConfig(orderedPaths, modes, $"将 {deviceName} 设为主屏"))
            return false;

        shift = CalculatePrimaryShift(originalTargetPosition.X, originalTargetPosition.Y);
        return true;
    }

    internal static uint DecodeSourceModeIndex(uint modeInfoIdx)
    {
        if (modeInfoIdx == DISPLAYCONFIG_PATH_MODE_IDX_INVALID)
            return DISPLAYCONFIG_PATH_SOURCE_MODE_IDX_INVALID;
        return modeInfoIdx >> 16;
    }

    private static uint GetSourceModeIndex(uint modeInfoIdx) => DecodeSourceModeIndex(modeInfoIdx);

    private static bool NormalizeSourceModeIndices(
        DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes)
    {
        for (int i = 0; i < paths.Length; i++)
        {
            if (!TryResolveSourceModeIndex(paths[i], modes, out uint modeIndex))
            {
                SetError($"现代显示配置 API 找不到 source {paths[i].sourceInfo.id} 的 source mode");
                return false;
            }

            paths[i].sourceInfo.modeInfoIdx = EncodeSourceModeIndex(
                paths[i].sourceInfo.modeInfoIdx, modeIndex);
        }

        return true;
    }

    private static bool TryResolveSourceModeIndex(
        DISPLAYCONFIG_PATH_INFO path,
        IReadOnlyList<DISPLAYCONFIG_MODE_INFO> modes,
        out uint modeIndex)
    {
        uint directIndex = GetSourceModeIndex(path.sourceInfo.modeInfoIdx);
        if (directIndex != DISPLAYCONFIG_PATH_SOURCE_MODE_IDX_INVALID && directIndex < modes.Count &&
            modes[(int)directIndex].infoType == DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE &&
            modes[(int)directIndex].id == path.sourceInfo.id &&
            SameLuid(modes[(int)directIndex].adapterId, path.sourceInfo.adapterId))
        {
            modeIndex = directIndex;
            return true;
        }

        for (uint i = 0; i < modes.Count; i++)
        {
            if (modes[(int)i].infoType == DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE &&
                modes[(int)i].id == path.sourceInfo.id &&
                SameLuid(modes[(int)i].adapterId, path.sourceInfo.adapterId))
            {
                modeIndex = i;
                return true;
            }
        }

        modeIndex = DISPLAYCONFIG_PATH_SOURCE_MODE_IDX_INVALID;
        return false;
    }

    private static uint EncodeSourceModeIndex(uint original, uint modeIndex) =>
        (original & 0x0000FFFF) | ((modeIndex & 0xFFFF) << 16);

    private static bool SameLuid(LUID left, LUID right) =>
        left.LowPart == right.LowPart && left.HighPart == right.HighPart;

    private static bool TryQueryActiveDisplayConfig(
        out DISPLAYCONFIG_PATH_INFO[] paths, out DISPLAYCONFIG_MODE_INFO[] modes)
    {
        paths = [];
        modes = [];
        const uint flags = QDC_ONLY_ACTIVE_PATHS | QDC_VIRTUAL_MODE_AWARE;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            int result = GetDisplayConfigBufferSizes(flags, out uint pathCount, out uint modeCount);
            if (result != ERROR_SUCCESS)
            {
                SetError($"读取 Windows 显示配置缓冲区失败，错误码 {result}: {DescribeDisplayConfigResult(result)}");
                return false;
            }

            var pathBuffer = new DISPLAYCONFIG_PATH_INFO[(int)pathCount];
            var modeBuffer = new DISPLAYCONFIG_MODE_INFO[(int)modeCount];
            uint actualPathCount = pathCount;
            uint actualModeCount = modeCount;
            result = QueryDisplayConfig(flags, ref actualPathCount, pathBuffer,
                ref actualModeCount, modeBuffer, IntPtr.Zero);

            if (result == ERROR_SUCCESS)
            {
                paths = pathBuffer.Take((int)actualPathCount).ToArray();
                modes = modeBuffer.Take((int)actualModeCount).ToArray();
                return true;
            }

            if (result != ERROR_INSUFFICIENT_BUFFER)
            {
                SetError($"读取 Windows 活动显示配置失败，错误码 {result}: {DescribeDisplayConfigResult(result)}");
                return false;
            }
        }

        SetError("读取 Windows 活动显示配置失败：显示配置在读取期间持续变化");
        return false;
    }

    private static int FindPathForDevice(
        IReadOnlyList<DISPLAYCONFIG_PATH_INFO> paths, string deviceName)
    {
        for (int i = 0; i < paths.Count; i++)
        {
            var request = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                    adapterId = paths[i].sourceInfo.adapterId,
                    id = paths[i].sourceInfo.id
                }
            };
            int result = DisplayConfigGetDeviceInfo(ref request);
            if (result == ERROR_SUCCESS &&
                string.Equals(request.viewGdiDeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static bool TryApplyDisplayConfig(
        DISPLAYCONFIG_PATH_INFO[] paths,
        DISPLAYCONFIG_MODE_INFO[] modes,
        string operation)
    {
        const uint baseFlags = SDC_USE_SUPPLIED_DISPLAY_CONFIG |
            SDC_ALLOW_CHANGES | SDC_VIRTUAL_MODE_AWARE;
        int validation = SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes,
            SDC_VALIDATE | baseFlags);
        if (validation != ERROR_SUCCESS)
        {
            SetError($"Windows 拒绝验证{operation}，错误码 {validation}: {DescribeDisplayConfigResult(validation)}");
            return false;
        }

        int result = SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes,
            SDC_APPLY | SDC_SAVE_TO_DATABASE | baseFlags);
        if (result != ERROR_SUCCESS)
        {
            SetError($"Windows 拒绝应用{operation}，错误码 {result}: {DescribeDisplayConfigResult(result)}");
            return false;
        }

        LastError = null;
        return true;
    }

    private static string DescribeDisplayConfigResult(int result) => result switch
    {
        5 => "当前会话无权访问显示配置",
        31 => "显示驱动报告一般性失败",
        50 => "系统或驱动不支持现代显示配置 API",
        87 => "显示配置参数无效",
        122 => "显示配置缓冲区不足",
        1610 => "显示配置无法组成有效拓扑",
        _ => "未知显示配置系统错误"
    };

    // ---- 内部辅助 ----

    /// <summary>创建已初始化 dmSize 的 DEVMODEW。</summary>
    private static DEVMODEW CreateDevmode()
    {
        var dm = new DEVMODEW();
        dm.dmSize = (short)Marshal.SizeOf<DEVMODEW>();
        return dm;
    }

    private static NOTIFYICONDATAW CreateTrayIconData(
        IntPtr windowHandle, uint iconId, string tooltip)
    {
        return new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = windowHandle,
            uID = iconId,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = (uint)TrayCallbackMessage,
            hIcon = LoadIconW(IntPtr.Zero, (IntPtr)IDI_APPLICATION),
            szTip = tooltip
        };
    }

    /// <summary>用设备名再查一次，取 DeviceString 作为友好名称；取不到时回退为设备名。</summary>
    private static string GetFriendlyName(string deviceName)
    {
        try
        {
            // EDID 优先：从注册表读真实显示器型号（如 "DELL U3421WE"）,
            // EnumDisplayDevices 的 DeviceString 对现代显示器常返回 "Generic PnP Monitor"
            var edidName = TryGetEdidName(deviceName);
            if (!string.IsNullOrWhiteSpace(edidName))
                return edidName!;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ResSwitcher] EDID 读取失败: {ex.Message}");
        }

        try
        {
            var dev = new DISPLAY_DEVICEW();
            dev.cb = (uint)Marshal.SizeOf<DISPLAY_DEVICEW>();
            if (EnumDisplayDevicesW(deviceName, 0, ref dev, 0))
            {
                string? s = dev.DeviceString;
                if (!string.IsNullOrWhiteSpace(s) && s != "Generic PnP Monitor")
                    return s!;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ResSwitcher] GetFriendlyName({deviceName}) 失败: {ex.Message}");
        }

        return deviceName;
    }

    /// <summary>读取显示器实例 ID，供配置 profile 绑定物理显示器。</summary>
    private static string GetMonitorDeviceId(string deviceName)
    {
        try
        {
            var dev = new DISPLAY_DEVICEW();
            dev.cb = (uint)Marshal.SizeOf<DISPLAY_DEVICEW>();
            if (EnumDisplayDevicesW(deviceName, 0, ref dev, 0) && !string.IsNullOrWhiteSpace(dev.DeviceId))
                return dev.DeviceId!;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ResSwitcher] GetMonitorDeviceId({deviceName}) 失败: {ex.Message}");
        }

        return deviceName;
    }

    /// <summary>
    /// 从注册表 EDID 读取显示器真实型号。
    /// 路径：HKLM\SYSTEM\CurrentControlSet\Enum\DISPLAY\&lt;VENDOR&gt;\&lt;KEY&gt;\Device Parameters\EDID
    /// EDID 字节 54-125 为描述符块，其中 PnP 名称描述符（0xFC）含型号字符串。
    /// </summary>
    private static string? TryGetEdidName(string deviceName)
    {
        var edid = FindEdidForDevice(deviceName);
        if (edid == null || edid.Length < 128)
            return null;

        // 扫描 4 个 18 字节描述符块（偏移 54 起），找 0xFC（显示器名称）标记
        for (int i = 54; i + 18 <= 126; i += 18)
        {
            if (edid[i] == 0x00 && edid[i + 1] == 0x00 && edid[i + 2] == 0x00 && edid[i + 3] == 0xFC)
            {
                var chars = new List<char>();
                for (int j = i + 5; j < i + 18; j++)
                {
                    if (edid[j] == 0x0A) break;
                    if (edid[j] >= 0x20) chars.Add((char)edid[j]);
                }
                var name = new string(chars.ToArray()).Trim();
                if (name.Length >= 3)
                    return name;
            }
        }
        return null;
    }

    /// <summary>在注册表 Enum\DISPLAY 下查找与设备名匹配的 EDID 字节。</summary>
    private static byte[]? FindEdidForDevice(string deviceName)
    {
        // deviceName 如 "\\.\DISPLAY2"；EDID 注册表无法直接按编号匹配,
        // 策略：取所有 DISPLAY 子键下活跃驱动的 EDID；单 EDID 时直接用,
        // 多个时按连接序（注册表键序）对齐 DISPLAY 编号。
        using var baseKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Enum\DISPLAY");
        if (baseKey == null) return null;

        var edids = new List<byte[]>();
        foreach (var vendor in baseKey.GetSubKeyNames())
        {
            using var vendorKey = baseKey.OpenSubKey(vendor);
            if (vendorKey == null) continue;
            foreach (var unit in vendorKey.GetSubKeyNames())
            {
                try
                {
                    using var param = vendorKey.OpenSubKey(unit + @"\Device Parameters");
                    if (param?.GetValue("EDID") is byte[] edid && edid.Length >= 128)
                        edids.Add(edid);
                }
                catch { /* 无权限或无 EDID，跳过 */ }
            }
        }

        if (edids.Count == 0) return null;
        if (edids.Count == 1) return edids[0];

        // 多显示器：按 DISPLAY 编号对齐（DISPLAY1 → 第 1 个 EDID，以此类推）
        var match = System.Text.RegularExpressions.Regex.Match(deviceName, @"DISPLAY(\d+)");
        if (match.Success)
        {
            int idx = int.Parse(match.Groups[1].Value) - 1;
            if (idx >= 0 && idx < edids.Count)
                return edids[idx];
        }
        return edids[0];
    }

    // ---- Win32 结构体（ABI 契约，字段顺序不可改动）----

    /// <summary>DISPLAY_DEVICEW：显示器设备描述。</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICEW
    {
        public uint cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    /// <summary>NOTIFYICONDATAW：通知区域图标数据。</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    /// <summary>DEVMODEW：显示模式。字段顺序与 Win32 ABI 严格一致。</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODEW
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;

        // 联合体：显示模式使用位置/方向字段，打印模式使用纸张字段；两者均占 16 字节。
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;

        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;

        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;

        public int dmDisplayFlags;
        public int dmDisplayFrequency;

        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_2DREGION
    {
        public uint cx;
        public uint cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
    {
        public ulong pixelRate;
        public DISPLAYCONFIG_RATIONAL hSyncFreq;
        public DISPLAYCONFIG_RATIONAL vSyncFreq;
        public DISPLAYCONFIG_2DREGION activeSize;
        public DISPLAYCONFIG_2DREGION totalSize;
        public uint videoStandard;
        public uint scanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_TARGET_MODE
    {
        public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINTL
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_SOURCE_MODE
    {
        public uint width;
        public uint height;
        public uint pixelFormat;
        public POINTL position;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct DISPLAYCONFIG_MODE_INFO
    {
        public const uint SOURCE = 1;
        public const uint TARGET = 2;

        [FieldOffset(0)]
        public uint infoType;
        [FieldOffset(4)]
        public uint id;
        [FieldOffset(8)]
        public LUID adapterId;
        [FieldOffset(16)]
        public DISPLAYCONFIG_TARGET_MODE targetMode;
        [FieldOffset(16)]
        public DISPLAYCONFIG_SOURCE_MODE sourceMode;
    }

    private const uint DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE = DISPLAYCONFIG_MODE_INFO.SOURCE;

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint scanLineOrdering;
        public uint targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public uint type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string viewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEXW
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    private const uint MONITORINFOF_PRIMARY = 1;
}

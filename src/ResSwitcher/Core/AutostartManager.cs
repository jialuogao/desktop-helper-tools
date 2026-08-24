using Microsoft.Win32;

namespace ResSwitcher.Core;

/// <summary>
/// 开机自启管理：写/删 HKCU\Software\Microsoft\Windows\CurrentVersion\Run 下的 ResSwitcher 值。
/// 仅操作 HKCU，无需管理员权限；失败时保留错误上下文，不影响主流程。
/// </summary>
public static class AutostartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ResSwitcher";
    public static string? LastError { get; private set; }

    /// <summary>自启是否已启用（值存在即视为启用）。异常时返回 false 并记录错误。</summary>
    public static bool IsEnabled()
    {
        LastError = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception ex)
        {
            LastError = $"读取开机自启状态失败 ({ex.GetType().Name}: {ex.Message})";
            Logger.Error("读取开机自启状态失败", ex);
            return false;
        }
    }

    /// <summary>启用：写入当前 exe 路径；禁用：删除该值。幂等。</summary>
    public static void SetEnabled(bool enabled)
    {
        LastError = null;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null)
            {
                LastError = "无法打开当前用户的开机自启注册表项。";
                Logger.Warn(LastError);
                return;
            }

            if (enabled)
            {
                // 注意用 ProcessPath：单文件发布下 Assembly.Location 为空
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(ValueName, $"\"{exePath}\"");
                else
                {
                    LastError = "无法确定当前程序路径，开机自启未写入。";
                    Logger.Warn(LastError);
                }
            }
            else
            {
                if (key.GetValue(ValueName) is not null)
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            LastError = $"写入开机自启失败 ({ex.GetType().Name}: {ex.Message})";
            Logger.Error("写入开机自启失败", ex);
        }
    }
}

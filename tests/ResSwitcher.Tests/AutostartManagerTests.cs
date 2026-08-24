using ResSwitcher.Core;
using Xunit;

namespace ResSwitcher.Tests;

/// <summary>
/// 开机自启测试（A1–A3）。操作真实 HKCU Run 键，但使用测试专用值名路径隔离：
/// 直接用 ResSwitcher 值名，测试结束后清理为初始状态。
/// </summary>
public class AutostartManagerTests
{
    private static bool InitialState()
    {
        try { return AutostartManager.IsEnabled(); }
        catch { return false; }
    }

    // A1：SetEnabled(true) 后 IsEnabled() 为 true，且值为当前 exe 路径（带引号）
    [Fact]
    public void A1_SetTrue_ThenIsEnabled_True()
    {
        bool initial = InitialState();
        try
        {
            AutostartManager.SetEnabled(true);

            Assert.True(AutostartManager.IsEnabled());

            using var key = Microsoft.Win32.Registry.CurrentUser
                .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            var val = key?.GetValue("ResSwitcher") as string;
            Assert.False(string.IsNullOrEmpty(val));
            Assert.Contains(Environment.ProcessPath ?? "", val);
        }
        finally
        {
            AutostartManager.SetEnabled(initial);
        }
    }

    // A2：SetEnabled(false) 后 IsEnabled() 为 false
    [Fact]
    public void A2_SetFalse_ThenIsEnabled_False()
    {
        bool initial = InitialState();
        try
        {
            AutostartManager.SetEnabled(true);   // 先确保存在
            AutostartManager.SetEnabled(false);

            Assert.False(AutostartManager.IsEnabled());
        }
        finally
        {
            AutostartManager.SetEnabled(initial);
        }
    }

    // A3：幂等——连续两次 true/false 无异常
    [Fact]
    public void A3_Idempotent_NoException()
    {
        bool initial = InitialState();
        try
        {
            AutostartManager.SetEnabled(true);
            AutostartManager.SetEnabled(true);
            AutostartManager.SetEnabled(false);
            AutostartManager.SetEnabled(false);

            Assert.False(AutostartManager.IsEnabled());
        }
        finally
        {
            AutostartManager.SetEnabled(initial);
        }
    }
}

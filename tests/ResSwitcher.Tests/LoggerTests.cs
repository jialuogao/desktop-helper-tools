using System.IO;
using ResSwitcher.Core;
using Xunit;

namespace ResSwitcher.Tests;

/// <summary>Logger 与显示器几何测试（L1–L5）：迁移回归防护。</summary>
public class LoggerTests : IDisposable
{
    private readonly string _dir;

    public LoggerTests()
    {
        _dir = Logger.LogDir; // 触发目录创建
    }

    public void Dispose()
    {
        // 不删除：日志是诊断资产；测试只验证写入成功
    }

    // L1：Info 写入后文件存在且包含内容
    [Fact]
    public void L1_Info_WritesToFile()
    {
        Logger.Info("L1 测试消息 " + Guid.NewGuid().ToString("N"));

        var file = Logger.LogFile;
        Assert.True(File.Exists(file), "日志文件应存在");
        Assert.Matches(@"^reswitcher-\d{8}-\d{6}-[0-9a-f]{32}\.log$", Path.GetFileName(file));
        Assert.Contains("L1 测试消息", File.ReadAllText(file));
    }

    // L2：Error 带异常时包含类型与堆栈
    [Fact]
    public void L2_ErrorWithException_ContainsDetails()
    {
        try { throw new InvalidOperationException("L2 故意异常"); }
        catch (Exception ex) { Logger.Error("L2 测试", ex); }

        var file = Logger.LogFile;
        var content = File.ReadAllText(file);
        Assert.Contains("L2 测试", content);
        Assert.Contains("InvalidOperationException", content);
        Assert.Contains("Stack:", content);

        var staleFile = Path.Combine(_dir, $"reswitcher-stale-{Guid.NewGuid():N}.log");
        File.WriteAllText(staleFile, "过期日志");
        File.SetLastWriteTimeUtc(staleFile, DateTime.UtcNow.AddDays(-4));
        try
        {
            Logger.Info("L2 清理触发");
            Assert.False(File.Exists(staleFile), "三天前的日志应自动删除");
        }
        finally
        {
            if (File.Exists(staleFile))
                File.Delete(staleFile);
        }
    }

    // L3：MonitorBounds 对每个活动显示器返回正尺寸（EnumDisplayMonitors 路径回归）
    [Fact]
    public void L3_MonitorBounds_AllMonitorsPositiveSize()
    {
        var monitors = DisplayApi.EnumerateMonitors();
        Assert.NotEmpty(monitors);

        foreach (var m in monitors)
        {
            var (x, y, w, h) = DisplayApi.GetMonitorBounds(m.DeviceName);
            Assert.True(w > 0 && h > 0, $"{m.DeviceName} 尺寸应大于 0，实际 {w}x{h}");
        }
    }

    // L4：DEVMODEW 的托管布局必须与 Win32 ABI 一致，否则切换 API 会读错字段
    [Fact]
    public void L4_DevmodeLayout_MatchesWin32Size()
    {
        Assert.Equal(220, DisplayApi.DevmodeSizeForTests);
    }

    // L5：设为主屏时必须把目标显示器位置提交到虚拟桌面原点
    [Fact]
    public void L5_PrimaryMode_MovesTargetToVirtualDesktopOrigin()
    {
        var mode = DisplayApi.PreparePrimaryMode(1920, 120, 0x100000);

        Assert.Equal(0, mode.X);
        Assert.Equal(0, mode.Y);
        Assert.Equal(0x20, mode.Fields);
    }

    // L6：切换主屏时整体平移虚拟桌面，保持显示器之间的相对位置
    [Fact]
    public void L6_PrimaryShift_PreservesRelativeLayout()
    {
        var shift = DisplayApi.CalculatePrimaryShift(1920, 120);

        Assert.Equal(-1920, shift.X);
        Assert.Equal(-120, shift.Y);
    }
}

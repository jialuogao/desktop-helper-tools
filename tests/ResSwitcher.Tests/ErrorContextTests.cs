using System.IO;
using ResSwitcher.Core;
using Xunit;

namespace ResSwitcher.Tests;

/// <summary>错误上下文测试（E1–E3）：失败必须可分类、可操作、可追踪。</summary>
public class ErrorContextTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ResSwitcherErrorTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    // E1：损坏配置回退时保留异常类型并写入日志
    [Fact]
    public void E1_CorruptedConfig_PreservesErrorAndLogsIt()
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "config.json");
        File.WriteAllText(path, "{ invalid json");

        AppConfigStore.Load(path);

        Assert.Contains("读取配置文件失败", AppConfigStore.LastError);
        Assert.Contains("JsonException", File.ReadAllText(Logger.LogFile));
    }

    // E2：注入的系统异常不会被状态机吞掉
    [Fact]
    public void E2_SystemException_PreservesTypeAndMessage()
    {
        var switcher = new ResolutionSwitcher(new AppConfig
        {
            Monitor = "display",
            Collection = new ResCollection { Items = [[1920, 1080], [2560, 1440]] }
        });
        switcher._resolveMonitor = _ => "display";
        switcher._getCurrent = _ => throw new InvalidOperationException("显示器已断开");

        var result = switcher.Toggle();

        Assert.Equal(SwitchResult.ApiFailed, result);
        Assert.Contains("InvalidOperationException", switcher.LastError);
        Assert.Contains("显示器已断开", switcher.LastError);
    }

    // E3：API 拒绝时保留可操作的失败信息
    [Fact]
    public void E3_ApiRejection_PreservesFailureContext()
    {
        var switcher = new ResolutionSwitcher(new AppConfig
        {
            Monitor = "display",
            Collection = new ResCollection { Items = [[1920, 1080], [2560, 1440]] }
        });
        switcher._resolveMonitor = _ => "display";
        switcher._getCurrent = _ => new Resolution(1280, 720);
        switcher._getSupported = _ => [new(1920, 1080), new(2560, 1440)];
        switcher._trySet = (_, _) => false;

        var result = switcher.Toggle();

        Assert.Equal(SwitchResult.ApiFailed, result);
        Assert.Contains("系统拒绝", switcher.LastError);
    }
}
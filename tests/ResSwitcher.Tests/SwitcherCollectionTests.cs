using ResSwitcher.Core;
using Xunit;

namespace ResSwitcher.Tests;

/// <summary>合集轮询状态机测试（D1–D13）。通过注入 fake 委托模拟系统 API，不触碰真实显示器。</summary>
public class SwitcherCollectionTests
{
    private const string Monitor = "\\\\.\\DISPLAY1";
    private static readonly Resolution R1 = new(3440, 1440);
    private static readonly Resolution R2 = new(2560, 1440);
    private static readonly Resolution R3 = new(1920, 1080);
    private static readonly Resolution Other = new(1280, 720);

    /// <summary>构造被测对象并注入 fake。</summary>
    private static (ResolutionSwitcher S, List<Resolution> SetCalls) Create(
        Resolution fakeCurrent, int[][] collection, List<Resolution>? supported = null)
    {
        var setCalls = new List<Resolution>();
        var s = new ResolutionSwitcher(new AppConfig
        {
            Monitor = Monitor,
            Collection = new ResCollection { Items = collection.ToList() }
        });

        var cur = fakeCurrent;
        s._getCurrent = _ => cur;
        s._getSupported = _ => supported ?? [new(3840, 2160), R1, R2, R3, Other];
        s._trySet = (_, res) => { setCalls.Add(res); cur = res; return true; };

        return (s, setCalls);
    }

    // D1：不在合集内首次点击 → 切到合集第 1 项
    [Fact]
    public void D1_FromOther_TogglesToFirstItem()
    {
        var (s, calls) = Create(Other, [[R1.Width, R1.Height], [R2.Width, R2.Height]]);

        var result = s.Toggle();

        Assert.Equal(SwitchResult.Success, result);
        Assert.Equal([R1], calls);
    }

    // D2：在第 1 项 → 切到第 2 项
    [Fact]
    public void D2_AtItem1_TogglesToItem2()
    {
        var (s, calls) = Create(R1, [[R1.Width, R1.Height], [R2.Width, R2.Height]]);

        var result = s.Toggle();

        Assert.Equal(SwitchResult.Success, result);
        Assert.Equal([R2], calls);
    }

    // D3：在最后一项 → 循环回第 1 项
    [Fact]
    public void D3_AtLastItem_CyclesToFirst()
    {
        var (s, calls) = Create(R2, [[R1.Width, R1.Height], [R2.Width, R2.Height]]);

        var result = s.Toggle();

        Assert.Equal(SwitchResult.Success, result);
        Assert.Equal([R1], calls);
    }

    // D4：三项合集完整轮询 + 从外部进入收敛到第 1 项
    [Fact]
    public void D4_ThreeItems_FullCycleSequence()
    {
        var (s, calls) = Create(Other, [[R1.Width, R1.Height], [R2.Width, R2.Height], [R3.Width, R3.Height]]);

        for (int i = 0; i < 5; i++)
            s.Toggle();

        // 序列：r1, r2, r3, r1, r2
        Assert.Equal(new[] { R1, R2, R3, R1, R2 }, calls);
    }

    // D5：合集只有 1 项 → 与当前分辨率来回切换（同旧单模式）
    [Fact]
    public void D5_SingleItem_AlternatesWithCurrent()
    {
        var (s, calls) = Create(new(3840, 2160), [[R2.Width, R2.Height]]);

        s.Toggle(); // → R2
        s.Toggle(); // → 3840x2160（original）
        s.Toggle(); // → R2

        Assert.Equal(new[] { R2, new Resolution(3840, 2160), R2 }, calls);
    }

    // D6：合集为空 → 返回可行动的未配置结果
    [Fact]
    public void D6_EmptyCollection_NoOp()
    {
        var (s, calls) = Create(Other, []);

        var result = s.Toggle();

        Assert.Equal(SwitchResult.NotConfigured, result);
        Assert.Contains("配置", s.LastError);
        Assert.Empty(calls);
    }

    // D7：目标不被支持 → 按不存在处理
    [Fact]
    public void D7_UnsupportedTarget_ReturnsUnsupported()
    {
        var (s, calls) = Create(Other, [[R1.Width, R1.Height]], supported: [Other]);

        var result = s.Toggle();

        Assert.Equal(SwitchResult.NotConfigured, result);
        Assert.Empty(calls);
    }

    // D8：非法合集项被忽略，不应让点击交互抛异常
    [Fact]
    public void D8_InvalidCollectionItems_AreIgnored()
    {
        var (s, calls) = Create(Other, [[0], [], [R1.Width, R1.Height]]);

        var result = s.Toggle();

        Assert.Equal(SwitchResult.Success, result);
        Assert.Equal([R1], calls);
    }

    // D9：auto 解析后使用实际设备名查询支持分辨率
    [Fact]
    public void D9_AutoMonitor_UsesResolvedDeviceForOriginal()
    {
        var (s, calls) = Create(R2, [[R2.Width, R2.Height]], supported: [R1, R2]);
        var supportedDevice = string.Empty;
        s._resolveMonitor = _ => "resolved-display";
        s._getSupported = device =>
        {
            supportedDevice = device;
            return [R1, R2];
        };

        s.Toggle();

        Assert.Equal("resolved-display", supportedDevice);
        Assert.Equal([R1], calls);
    }

    // D10：auto 主屏切换选择当前主屏之外的显示器
    [Fact]
    public void D10_AutoPrimary_UsesOtherMonitor()
    {
        var s = new ResolutionSwitcher(new AppConfig { Monitor = MonitorTarget.Auto });
        string? setTarget = null;
        s._getPrimary = () => "primary";
        s._enumerateMonitors = () =>
        [
            new DisplayDeviceInfo("primary", "主屏"),
            new DisplayDeviceInfo("secondary", "副屏")
        ];
        s._trySetPrimary = device => { setTarget = device; return true; };

        var result = s.TogglePrimary();

        Assert.Equal(SwitcherResult.Success, result);
        Assert.Equal("secondary", setTarget);
    }

    // D13：固定分辨率目标不影响主屏切换，主屏始终在当前主屏与另一块屏之间互换
    [Fact]
    public void D13_FixedResolutionTarget_DoesNotControlPrimaryToggle()
    {
        var s = new ResolutionSwitcher(new AppConfig { Monitor = "secondary" });
        string? setTarget = null;
        s._getPrimary = () => "primary";
        s._enumerateMonitors = () =>
        [
            new DisplayDeviceInfo("primary", "主屏"),
            new DisplayDeviceInfo("secondary", "副屏")
        ];
        s._trySetPrimary = device => { setTarget = device; return true; };

        var result = s.TogglePrimary();

        Assert.Equal(SwitcherResult.Success, result);
        Assert.Equal("secondary", setTarget);
    }

    // D14：auto 主屏切换后，分辨率切换跟随新的主屏
    [Fact]
    public void D14_AutoResolutionTarget_FollowsNewPrimaryAfterToggle()
    {
        var s = new ResolutionSwitcher(new AppConfig
        {
            Monitor = MonitorTarget.Auto,
            Collection = new ResCollection { Items = [[R1.Width, R1.Height], [R2.Width, R2.Height]] }
        });
        var primary = "primary";
        var current = new Dictionary<string, Resolution>
        {
            ["primary"] = R1,
            ["secondary"] = R1
        };
        string? resolutionDevice = null;
        s._getPrimary = () => primary;
        s._resolveMonitor = _ => primary;
        s._enumerateMonitors = () =>
        [
            new DisplayDeviceInfo("primary", "主屏"),
            new DisplayDeviceInfo("secondary", "副屏")
        ];
        s._getCurrent = device => current[device];
        s._getSupported = _ => [R1, R2];
        s._trySetPrimary = device => { primary = device; return true; };
        s._trySet = (device, resolution) =>
        {
            resolutionDevice = device;
            current[device] = resolution;
            return true;
        };

        Assert.Equal(SwitcherResult.Success, s.TogglePrimary());
        Assert.Equal(SwitchResult.Success, s.Toggle());

        Assert.Equal("secondary", resolutionDevice);
        Assert.Equal(R2, current["secondary"]);
    }

    // D11：每个显示器使用自己的分辨率 profile，不复用其他显示器的列表
    [Fact]
    public void D11_UsesProfileForResolvedMonitor()
    {
        var s = new ResolutionSwitcher(new AppConfig
        {
            Monitor = MonitorTarget.Auto,
            MonitorProfiles =
            [
                new MonitorProfile { DisplayId = "primary-id", Items = [[R1.Width, R1.Height]] },
                new MonitorProfile { DisplayId = "secondary-id", Items = [[R2.Width, R2.Height]] }
            ]
        });
        var calls = new List<Resolution>();
        var current = Other;
        s._resolveMonitor = _ => "secondary";
        s._enumerateMonitors = () =>
        [
            new DisplayDeviceInfo("primary", "主屏", "primary-id"),
            new DisplayDeviceInfo("secondary", "副屏", "secondary-id")
        ];
        s._getCurrent = _ => current;
        s._getSupported = _ => [R1, R2, Other];
        s._trySet = (_, resolution) => { calls.Add(resolution); current = resolution; return true; };

        var result = s.Toggle();

        Assert.Equal(SwitchResult.Success, result);
        Assert.Equal([R2], calls);
    }

    // D12：当前显示器不支持的 profile 项按不存在处理，跳过后选择可用项
    [Fact]
    public void D12_IgnoresUnsupportedProfileItems()
    {
        var s = new ResolutionSwitcher(new AppConfig
        {
            Monitor = "secondary",
            MonitorProfiles =
            [new MonitorProfile { DisplayId = "secondary", Items = [[R1.Width, R1.Height], [R2.Width, R2.Height]] }]
        });
        var calls = new List<Resolution>();
        var current = Other;
        s._resolveMonitor = _ => "secondary";
        s._enumerateMonitors = () => [new DisplayDeviceInfo("secondary", "副屏", "secondary")];
        s._getCurrent = _ => current;
        s._getSupported = _ => [R2, Other];
        s._trySet = (_, resolution) => { calls.Add(resolution); current = resolution; return true; };

        var result = s.Toggle();

        Assert.Equal(SwitchResult.Success, result);
        Assert.Equal([R2], calls);
    }

    // D15：切换成功后记录 LastResolutionChange 边界信息
    [Fact]
    public void D15_RecordsLastResolutionChange_WithOldAndNewBounds()
    {
        var (s, calls) = Create(Other, [[R1.Width, R1.Height], [R2.Width, R2.Height]]);
        int boundsCall = 0;
        s._getBounds = _ =>
        {
            boundsCall++;
            return boundsCall == 1 ? (0, 0, 2560, 1440) : (0, 0, 1920, 1080);
        };

        var result = s.Toggle();

        Assert.Equal(SwitchResult.Success, result);
        Assert.NotNull(s.LastResolutionChange);
        Assert.Equal(Monitor, s.LastResolutionChange!.DeviceName);
        Assert.Equal(new DisplayBounds(0, 0, 2560, 1440), s.LastResolutionChange.OldBounds);
        Assert.Equal(new DisplayBounds(0, 0, 1920, 1080), s.LastResolutionChange.NewBounds);
    }
}

using System.IO;
using ResSwitcher.Core;
using Xunit;

namespace ResSwitcher.Tests;

/// <summary>配置读写测试（C1–C6）。全部使用临时目录路径重载，不触碰真实 %APPDATA%。</summary>
public class ConfigStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public ConfigStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ResSwitcherTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "config.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 忽略清理失败 */ }
    }

    // C1：文件不存在 → 返回默认实例，不抛异常
    [Fact]
    public void C1_Load_MissingFile_ReturnsDefault()
    {
        var cfg = AppConfigStore.Load(_path);

        Assert.NotNull(cfg);
        Assert.Equal("auto", cfg.Monitor);
        Assert.Equal(48, cfg.Button.Size);
        Assert.Equal(0.35, cfg.Button.IdleAlpha);
    }

    // C2：损坏 JSON → 返回默认实例，不抛异常
    [Fact]
    public void C2_Load_CorruptedJson_ReturnsDefault()
    {
        File.WriteAllText(_path, "\"{{{");

        var cfg = AppConfigStore.Load(_path);

        Assert.NotNull(cfg);
        Assert.Empty(cfg.Collection.Items);
    }

    // C5：嵌套配置为 null 时仍返回可直接使用的配置
    [Fact]
    public void C5_Load_NullNestedSections_ReturnsUsableDefaults()
    {
        File.WriteAllText(_path, "{\"Button\":null,\"Collection\":null,\"Single\":null}");

        var cfg = AppConfigStore.Load(_path);

        Assert.NotNull(cfg.Button);
        Assert.NotNull(cfg.Collection);
        Assert.NotNull(cfg.Collection.Items);
        Assert.NotNull(cfg.Single);
    }

    // C6：异常尺寸、透明度和非法合集项在加载时被归一化
    [Fact]
    public void C6_Load_InvalidValues_NormalizesConfig()
    {
        File.WriteAllText(_path, "{\"Button\":{\"Size\":999,\"IdleAlpha\":-1},\"Collection\":{\"Items\":[[0,1],[],[1920,1080],[1920,1080]]}}");

        var cfg = AppConfigStore.Load(_path);

        Assert.Equal(128, cfg.Button.Size);
        Assert.Equal(0.1, cfg.Button.IdleAlpha);
        Assert.Equal([[1920, 1080]], cfg.Collection.Items);
    }

    // C7：按显示器保存的分辨率 profile 可往返保存
    [Fact]
    public void C7_SaveThenLoad_RoundTripsMonitorProfiles()
    {
        var cfg = new AppConfig
        {
            MonitorProfiles =
            [
                new MonitorProfile { DisplayId = "display-a", DisplayName = "主屏", Items = [[3440, 1440]] },
                new MonitorProfile { DisplayId = "display-b", DisplayName = "副屏", Items = [[1920, 1080], [1280, 720]] }
            ]
        };

        AppConfigStore.Save(cfg, _path);
        var loaded = AppConfigStore.Load(_path);

        Assert.Equal(2, loaded.MonitorProfiles.Count);
        Assert.Equal("display-a", loaded.MonitorProfiles[0].DisplayId);
        Assert.Equal("主屏", loaded.MonitorProfiles[0].DisplayName);
        Assert.Equal([[1920, 1080], [1280, 720]], loaded.MonitorProfiles[1].Items);
    }

    // C3：Save→Load 往返，所有字段逐项相等
    [Fact]
    public void C3_SaveThenLoad_RoundTripsAllFields()
    {
        var cfg = new AppConfig
        {
            Monitor = "\\\\.\\DISPLAY2",
            Collection = new ResCollection { Items = [[3840, 2160], [1280, 720]] },
            Button = new ButtonCfg { X = 200, Y = 300, Size = 64, Color = "#FF0000", IdleAlpha = 0.5 },
            Autostart = true
        };

        AppConfigStore.Save(cfg, _path);
        var loaded = AppConfigStore.Load(_path);

        Assert.Equal(cfg.Monitor, loaded.Monitor);
        Assert.Equal(cfg.Collection.Items.Count, loaded.Collection.Items.Count);
        Assert.Equal(cfg.Collection.Items[0], loaded.Collection.Items[0]);
        Assert.Equal(cfg.Collection.Items[1], loaded.Collection.Items[1]);
        Assert.Equal(cfg.Button.X, loaded.Button.X);
        Assert.Equal(cfg.Button.Y, loaded.Button.Y);
        Assert.Equal(cfg.Button.Size, loaded.Button.Size);
        Assert.Equal(cfg.Button.Color, loaded.Button.Color);
        Assert.Equal(cfg.Button.IdleAlpha, loaded.Button.IdleAlpha);
        Assert.Equal(cfg.Autostart, loaded.Autostart);
    }

    // C4：原子性——保存后无 .tmp 残留；覆盖已有文件成功
    [Fact]
    public void C4_Save_Atomic_NoTmpLeftover_OverwriteWorks()
    {
        var first = new AppConfig { Collection = new ResCollection { Items = [[1920, 1080]] } };
        AppConfigStore.Save(first, _path);

        // 覆盖已有文件
        var second = new AppConfig { Button = new ButtonCfg { Size = 96 } };
        AppConfigStore.Save(second, _path);

        var loaded = AppConfigStore.Load(_path);
        Assert.Equal(96, loaded.Button.Size);

        // 无 .tmp 残留
        Assert.False(File.Exists(_path + ".tmp"), "Save 后不应残留 .tmp 文件");
    }
}

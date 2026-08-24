using System.IO;
using System.Text.Json;

namespace ResSwitcher.Core;

/// <summary>分辨率项（单模式用）。</summary>
public sealed class ResItem
{
    public int Width { get; set; } = 2560;
    public int Height { get; set; } = 1440;
}

/// <summary>分辨率合集：点击按钮按顺序轮询切换。单项时行为同单模式，空合集提示配置。</summary>
public sealed class ResCollection
{
    public List<int[]> Items { get; set; } = [];
}

/// <summary>单个显示器的分辨率切换 profile。</summary>
public sealed class MonitorProfile
{
    /// <summary>优先使用显示器稳定设备 ID；无法读取时回退为设备名。</summary>
    public string DisplayId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<int[]> Items { get; set; } = [];
}

/// <summary>悬浮按钮外观与位置。</summary>
public sealed class ButtonCfg
{
    // X/Y == int.MinValue 表示无记录（默认位置屏幕1右上角）；负数坐标合法（副屏在主屏左侧）
    public const int NoPosition = int.MinValue;

    public int X { get; set; } = NoPosition;
    public int Y { get; set; } = NoPosition;
    public int Size { get; set; } = 48;
    public string Color { get; set; } = "#3B82F6";
    public double IdleAlpha { get; set; } = 0.35;
}

/// <summary>应用配置模型。属性名固定，后续任务依赖。</summary>
public sealed class AppConfig
{
    public string Monitor { get; set; } = "auto";
    public ResItem Single { get; set; } = new();
    public ResCollection Collection { get; set; } = new();
    public List<MonitorProfile> MonitorProfiles { get; set; } = [];
    public ButtonCfg Button { get; set; } = new();
    public bool Autostart { get; set; } = false;
}

/// <summary>配置读写：JSON 存于 %APPDATA%\ResSwitcher\config.json。</summary>
public static class AppConfigStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    public static string? LastError { get; private set; }

    /// <summary>配置目录：%APPDATA%\ResSwitcher，不存在则创建。</summary>
    public static string GetConfigDir()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ResSwitcher");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ResSwitcher", "config.json");

    /// <summary>从默认路径加载。文件缺失或损坏时返回默认配置，不抛异常。</summary>
    public static AppConfig Load()
    {
        try { return Load(DefaultPath()); }
        catch { return new AppConfig(); }
    }

    /// <summary>从指定路径加载（可测性重载）。</summary>
    public static AppConfig Load(string filePath)
    {
        LastError = null;
        try
        {
            if (!File.Exists(filePath))
                return new AppConfig();

            string json = File.ReadAllText(filePath);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json);
            return Normalize(cfg ?? new AppConfig());
        }
        catch (Exception ex)
        {
            LastError = $"读取配置文件失败 ({ex.GetType().Name}: {ex.Message})";
            Logger.Error($"读取配置文件失败: {filePath}", ex);
            return new AppConfig();
        }
    }

    private static AppConfig Normalize(AppConfig cfg)
    {
        cfg.Monitor = string.IsNullOrWhiteSpace(cfg.Monitor) ? "auto" : cfg.Monitor;
        cfg.Single ??= new ResItem();
        cfg.Collection ??= new ResCollection();
        cfg.Collection.Items = NormalizeItems(cfg.Collection.Items);
        cfg.MonitorProfiles ??= [];
        cfg.MonitorProfiles = cfg.MonitorProfiles
            .Where(profile => profile is not null && !string.IsNullOrWhiteSpace(profile.DisplayId))
            .Select(profile =>
            {
                profile.DisplayId = profile.DisplayId.Trim();
                profile.DisplayName ??= string.Empty;
                profile.Items = NormalizeItems(profile.Items);
                return profile;
            })
            .GroupBy(profile => profile.DisplayId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        cfg.Button ??= new ButtonCfg();
        cfg.Button.Size = Math.Clamp(cfg.Button.Size, 24, 128);
        cfg.Button.IdleAlpha = Math.Clamp(cfg.Button.IdleAlpha, 0.1, 1.0);
        cfg.Button.Color = string.IsNullOrWhiteSpace(cfg.Button.Color) ? "#3B82F6" : cfg.Button.Color;
        return cfg;
    }

    private static List<int[]> NormalizeItems(IEnumerable<int[]>? items) =>
        (items ?? [])
            .Where(item => item is { Length: >= 2 } && item[0] > 0 && item[1] > 0)
            .Select(item => new[] { item[0], item[1] })
            .DistinctBy(item => (item[0], item[1]))
            .ToList();

    /// <summary>保存到默认路径。原子写：先写 .tmp 再替换。</summary>
    public static void Save(AppConfig cfg) => Save(cfg, DefaultPath());

    /// <summary>保存到指定路径（可测性重载）。原子写：先写 .tmp 再替换。</summary>
    public static void Save(AppConfig cfg, string filePath)
    {
        LastError = null;
        string? directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string tmp = filePath + ".tmp";
        try
        {
            string json = JsonSerializer.Serialize(cfg, JsonOpts);
            File.WriteAllText(tmp, json);
            if (File.Exists(filePath))
                File.Replace(tmp, filePath, null);
            else
                File.Move(tmp, filePath);
        }
        catch (Exception ex)
        {
            LastError = $"保存配置文件失败 ({ex.GetType().Name}: {ex.Message})";
            Logger.Error($"保存配置文件失败: {filePath}", ex);
            throw;
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }
}

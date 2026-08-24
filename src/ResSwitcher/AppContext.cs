using System.Windows;
using ResSwitcher.Core;
using ResSwitcher.Ui;

namespace ResSwitcher;

/// <summary>
/// WPF 应用组合根：持有配置/服务并装配悬浮窗与设置窗。
/// </summary>
public sealed class AppContext : Application
{
    private readonly AppConfig _config;
    private readonly ResolutionSwitcher _switcher;
    private readonly OverlayWindow _overlay;

    public AppContext()
    {
        // 全局异常捕获：所有未处理异常写入日志，避免闪退无迹可寻
        DispatcherUnhandledException += (_, e) =>
        {
            Logger.Error("UI 线程未处理异常", e.Exception);
            MessageBox.Show($"发生错误：{e.Exception.GetType().Name}: {e.Exception.Message}\n\n详情见日志：{Logger.LogFile}",
                "ResSwitcher", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // 尽量不崩溃
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Error("非 UI 线程未处理异常", e.ExceptionObject as Exception ?? new Exception("unknown"));

        _config = AppConfigStore.Load();
        string? configLoadError = AppConfigStore.LastError;
        Logger.Info($"启动：Monitor={_config.Monitor}, 合集={_config.Collection.Items.Count}项, Button=({_config.Button.X},{_config.Button.Y})");

        // 同步注册表与配置一致
        AutostartManager.SetEnabled(_config.Autostart);

        _switcher = new ResolutionSwitcher(_config);

        _overlay = new OverlayWindow(
            _config,
            onToggleRes: OnToggleRes,
            onTogglePrimary: OnTogglePrimary,
            onOpenSettings: OpenSettings,
            onExit: Shutdown,
            onConfigDirty: SaveConfig);

        MainWindow = _overlay;
        _overlay.Show();

        if (configLoadError is not null)
            ShowProblem("配置文件无法读取", $"程序已使用默认设置启动。\n\n{configLoadError}\n\n请右键悬浮按钮 →「设置…」重新保存配置。\n\n日志：{Logger.LogFile}", MessageBoxImage.Warning);
        if (AutostartManager.LastError is not null)
            ShowProblem("开机自启未能同步", $"系统没有完成开机自启设置。\n\n{AutostartManager.LastError}\n\n请检查当前用户注册表权限，或在设置中取消勾选开机自动启动。\n\n日志：{Logger.LogFile}", MessageBoxImage.Warning);
    }

    /// <summary>左区单击：切换主显示器。</summary>
    private void OnTogglePrimary()
    {
        var result = _switcher.TogglePrimary();
        if (result == SwitcherResult.Success)
        {
            _overlay.ApplyPrimaryShift(_switcher.LastPrimaryShift);
        }
        else
        {
            string detail = _switcher.LastError ?? DisplayApi.LastError ?? "未返回具体系统信息。";
            ShowProblem("主屏切换失败",
                $"系统没有完成主屏切换。\n\n系统信息：{detail}\n\n可尝试：确认已连接至少两台显示器，并在 Windows 显示设置中确认它们处于扩展模式。\n\n日志：{Logger.LogFile}",
                MessageBoxImage.Warning);
        }
    }

    /// <summary>右区单击：切换分辨率。</summary>
    private void OnToggleRes()
    {
        var result = _switcher.Toggle();
        if (result == SwitchResult.Success)
            return;

        string detail = _switcher.LastError ?? DisplayApi.LastError ?? "未返回具体系统信息。";
        string reason = result switch
        {
            SwitchResult.NotConfigured =>
                "尚未配置可切换的分辨率。\n\n请右键悬浮按钮 →「设置…」，选择显示器后从支持列表添加分辨率。",
            SwitchResult.UnsupportedResolution =>
                $"目标分辨率不被当前显示器支持。\n\n系统信息：{detail}\n\n请右键悬浮按钮 →「设置…」，选择当前显示器并从支持列表添加分辨率。",
            SwitchResult.ApiFailed =>
                $"系统调用失败。\n\n系统信息：{detail}\n\n可尝试：关闭独占全屏应用，改为无边框窗口；确认目标分辨率来自设置窗口的支持列表；关闭可能正在修改显示模式的工具后重试。\n\n日志：{Logger.LogFile}",
            _ => result.ToString()
        };

        var choice = MessageBox.Show(_overlay, reason, "分辨率切换失败",
            MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (choice == MessageBoxResult.OK &&
            (result == SwitchResult.UnsupportedResolution || result == SwitchResult.NotConfigured))
            OpenSettings();
    }

    /// <summary>打开设置窗口；确定后热更新并持久化。</summary>
    private void OpenSettings()
    {
        try
        {
            Logger.Info("打开设置窗口");
            var dlg = new SettingsWindow(_config) { Owner = _overlay };
            dlg.ShowDialog();
            if (dlg.Confirmed)
            {
                AutostartManager.SetEnabled(_config.Autostart);
                if (AutostartManager.LastError is not null)
                    ShowProblem("开机自启设置失败", $"设置已保存，但开机自启没有完成。\n\n{AutostartManager.LastError}\n\n请检查当前用户注册表权限。\n\n日志：{Logger.LogFile}", MessageBoxImage.Warning);
                _switcher.OnConfigChanged();
                _overlay.ApplyConfig(_config);
                SaveConfig();
                Logger.Info("设置已保存并应用");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("设置窗口异常", ex);
            MessageBox.Show(_overlay, $"设置窗口出错：{ex.GetType().Name}: {ex.Message}\n详情见日志：{Logger.LogFile}", "ResSwitcher",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveConfig()
    {
        try
        {
            AppConfigStore.Save(_config);
        }
        catch (Exception ex)
        {
            Logger.Error("保存配置失败", ex);
            ShowProblem("配置保存失败", $"当前设置或悬浮按钮位置没有写入磁盘。\n\n异常：{ex.GetType().Name}: {ex.Message}\n\n请确认配置目录可写：%APPDATA%\\ResSwitcher\n\n日志：{Logger.LogFile}", MessageBoxImage.Error);
        }
    }

    private static void ShowProblem(string title, string message, MessageBoxImage image)
        => MessageBox.Show(message, title, MessageBoxButton.OK, image);
}

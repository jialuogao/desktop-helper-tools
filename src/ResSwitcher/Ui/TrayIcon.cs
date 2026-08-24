using System.Windows;
using System.Windows.Interop;
using ResSwitcher.Core;

namespace ResSwitcher.Ui;

/// <summary>通知区域图标：窗口仍隐藏于任务栏，仅在系统托盘保留入口。</summary>
public sealed class TrayIcon : IDisposable
{
    private const uint IconId = 1;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonUp = 0x0205;

    private readonly Window _owner;
    private readonly HwndSource _source;
    private bool _disposed;

    public TrayIcon(Window owner)
    {
        _owner = owner;
        var handle = new WindowInteropHelper(owner).Handle;
        _source = HwndSource.FromHwnd(handle)
            ?? throw new InvalidOperationException("无法取得悬浮窗句柄，无法创建通知区域图标。");
        _source.AddHook(WndProc);

        if (!DisplayApi.AddTrayIcon(handle, IconId, "ResSwitcher"))
        {
            _source.RemoveHook(WndProc);
            throw new InvalidOperationException("Windows 拒绝创建通知区域图标。");
        }
    }

    private IntPtr WndProc(
        IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != DisplayApi.TrayCallbackMessage)
            return IntPtr.Zero;

        int notification = unchecked((int)lParam.ToInt64());
        if (notification == WmLButtonUp)
            _owner.Dispatcher.BeginInvoke(ShowSettings);
        else if (notification == WmRButtonUp)
            _owner.Dispatcher.BeginInvoke(ShowContextMenu);

        handled = true;
        return IntPtr.Zero;
    }

    private void ShowSettings()
    {
        if (!_disposed && _owner is OverlayWindow overlay)
            overlay.OpenSettingsFromTray();
    }

    private void ShowContextMenu()
    {
        if (_disposed)
            return;

        uint command = DisplayApi.ShowTrayContextMenu(_source.Handle);
        switch (ResolveMenuCommand(command))
        {
            case TrayMenuAction.Settings:
                ShowSettings();
                break;
            case TrayMenuAction.Exit:
                if (_owner is OverlayWindow overlay)
                    overlay.ExitFromTray();
                break;
        }
    }

    internal static TrayMenuAction ResolveMenuCommand(uint command) => command switch
    {
        1 => TrayMenuAction.Settings,
        2 => TrayMenuAction.Exit,
        _ => TrayMenuAction.None
    };

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DisplayApi.RemoveTrayIcon(_source.Handle, IconId);
        _source.RemoveHook(WndProc);
    }
}

internal enum TrayMenuAction
{
    None,
    Settings,
    Exit
}
using ResSwitcher.Core;
using Xunit;

namespace ResSwitcher.Tests;

public class TopmostTests
{
    // ---- T-Topmost-1：EnforceTopmostInWindowPos 纯函数单元测试 ----

    [Fact]
    public void EnforceTopmostInWindowPos_WhenNotTopmost_RewritesToTopmost()
    {
        var pos = new DisplayApi.WINDOWPOS { hwndInsertAfter = IntPtr.Zero }; // HWND_TOP
        bool changed = DisplayApi.EnforceTopmostInWindowPos(ref pos);
        Assert.True(changed);
        Assert.Equal(new IntPtr(-1), pos.hwndInsertAfter);
    }

    [Fact]
    public void EnforceTopmostInWindowPos_WhenAlreadyTopmost_ReturnsFalse()
    {
        var pos = new DisplayApi.WINDOWPOS { hwndInsertAfter = new IntPtr(-1) }; // HWND_TOPMOST
        bool changed = DisplayApi.EnforceTopmostInWindowPos(ref pos);
        Assert.False(changed);
        Assert.Equal(new IntPtr(-1), pos.hwndInsertAfter);
    }

    [Fact]
    public void EnforceTopmostInWindowPos_WhenNotopmost_RewritesToTopmost()
    {
        var pos = new DisplayApi.WINDOWPOS { hwndInsertAfter = new IntPtr(-2) }; // HWND_NOTOPMOST
        bool changed = DisplayApi.EnforceTopmostInWindowPos(ref pos);
        Assert.True(changed);
        Assert.Equal(new IntPtr(-1), pos.hwndInsertAfter);
    }

    // ---- T-Topmost-2：ForceTopmost 句柄为 IntPtr.Zero 时防御性返回 ----

    [Fact]
    public void ForceTopmost_WithZeroHandle_ReturnsFalse()
    {
        bool result = DisplayApi.ForceTopmost(IntPtr.Zero);
        Assert.False(result);
    }
}

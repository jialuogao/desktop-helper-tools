using ResSwitcher.Ui;
using Xunit;

namespace ResSwitcher.Tests;

public class TrayIconTests
{
    [Fact]
    public void NativeSettingsCommand_MapsToSettings()
    {
        Assert.Equal(TrayMenuAction.Settings, TrayIcon.ResolveMenuCommand(1));
    }

    [Fact]
    public void NativeExitCommand_MapsToExit()
    {
        Assert.Equal(TrayMenuAction.Exit, TrayIcon.ResolveMenuCommand(2));
    }

    [Fact]
    public void UnknownNativeCommand_IsIgnored()
    {
        Assert.Equal(TrayMenuAction.None, TrayIcon.ResolveMenuCommand(99));
    }
}
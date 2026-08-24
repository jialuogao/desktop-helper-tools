using System.Windows;
using ResSwitcher;

// WPF 入口：单实例互斥 → 启动 AppContext
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\ResSwitcher", out bool createdNew);
        if (!createdNew)
            return;

        var app = new ResSwitcher.AppContext();
        app.Run();
    }
}

using System.Windows;

namespace DCDWatch;

/// <summary>
/// Khởi động theo cách chạy:
///   --uninstall [--quiet]  gỡ cài đặt (Windows Settings gọi lệnh này)
///   --install              cài im lặng, không hỏi (dùng cho build.ps1)
///   --portable             chạy luôn, không hỏi cài
///   chạy từ ngoài thư mục cài -> hỏi cài; từ thư mục cài -> mở app
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Video tự chạy không cần bấm — cú bấm đầu tiên trên trang phim hay bị quảng cáo nuốt.
        // Phải đặt TRƯỚC khi tạo WebView2 đầu tiên.
        var wv = "--autoplay-policy=no-user-gesture-required";
        // soi trang bằng DevTools từ ngoài: đặt DCDW_DEBUG_PORT=9222 trước khi chạy
        if (int.TryParse(Environment.GetEnvironmentVariable("DCDW_DEBUG_PORT"), out var p))
            wv += " --remote-debugging-port=" + p;
        Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", wv);

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        bool Has(string f) => e.Args.Any(a => string.Equals(a, f, StringComparison.OrdinalIgnoreCase));

        if (Has("--uninstall"))
        {
            RunUninstall(quiet: Has("--quiet"));
            Shutdown();
            return;
        }

        if (Has("--install"))
        {
            Setup.Install(desktopShortcut: true);
            Setup.LaunchInstalled();
            Shutdown();
            return;
        }

        bool askInstall = !Setup.RunningInstalled && !Has("--portable");
#if DEBUG
        askInstall = false;   // dotnet run / F5 thì khỏi hỏi
#endif
        if (askInstall)
        {
            var w = new InstallWindow();
            w.ShowDialog();
            switch (w.Choice)
            {
                case SetupChoice.Installed:
                    Setup.LaunchInstalled();
                    Shutdown();
                    return;
                case SetupChoice.Cancel:
                    Shutdown();
                    return;
            }
        }

        var main = new MainWindow();
        MainWindow = main;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
    }

    private static void RunUninstall(bool quiet)
    {
        bool removeData = false;
        if (!quiet)
        {
            if (MessageBox.Show("Gỡ DCD_WatchNoAds khỏi máy?", "Gỡ cài đặt",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            removeData = MessageBox.Show(
                "Xoá luôn trang đã lưu, cookie và đăng nhập trên các trang web?\n\n" +
                "Chọn No để giữ lại — lần sau cài lại vẫn còn.",
                "Gỡ cài đặt", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        Setup.Uninstall(removeData);

        if (!quiet)
            MessageBox.Show("Đã gỡ DCD_WatchNoAds.", "Gỡ cài đặt",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

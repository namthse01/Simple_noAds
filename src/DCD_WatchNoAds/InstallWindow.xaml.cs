using System.Windows;

namespace DCDWatch;

public enum SetupChoice { Cancel, Installed, RunPortable }

/// <summary>Hiện ra khi chạy exe từ ngoài thư mục cài (vừa tải về chẳng hạn).</summary>
public partial class InstallWindow : Window
{
    public SetupChoice Choice { get; private set; } = SetupChoice.Cancel;

    public InstallWindow()
    {
        InitializeComponent();
        LblDir.Text = Setup.InstallDir;

        var have = Setup.InstalledVersion;
        var mine = Setup.SelfVersion;
        if (have is null)
        {
            LblVersion.Text = "Phiên bản " + mine;
        }
        else
        {
            LblVersion.Text = have == mine
                ? $"Phiên bản {mine} · máy đã cài bản này"
                : $"Phiên bản {mine} · máy đang có bản {have}";
            BtnInstall.Content = have == mine ? "Cài lại" : "Cập nhật";
            Title = "Cập nhật DCD_WatchNoAds";
        }
    }

    private void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        try
        {
            Setup.Install(ChkDesktop.IsChecked == true);
            Choice = SetupChoice.Installed;
            Close();
        }
        catch (Exception ex)
        {
            IsEnabled = true;
            LblError.Text = "Cài không được: " + ex.Message;
            LblError.Visibility = Visibility.Visible;
        }
    }

    private void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        Choice = SetupChoice.RunPortable;
        Close();
    }
}

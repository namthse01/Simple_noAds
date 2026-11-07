using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace DCDWatch;

/// <summary>
/// Chính file exe này vừa là app vừa là bộ cài: chạy từ chỗ khác (Downloads...) thì tự chép mình vào
/// %LOCALAPPDATA%\Programs, tạo shortcut và ghi vào danh sách gỡ cài đặt của Windows.
/// Cài cho riêng người dùng hiện tại nên không cần quyền admin.
/// </summary>
public static class Setup
{
    public const string Name = "DCD_WatchNoAds";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + Name;

    public static string InstallDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Name);
    public static string InstalledExe => Path.Combine(InstallDir, Name + ".exe");

    private static string SelfExe => Environment.ProcessPath ?? "";

    private static string DesktopLnk => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Name + ".lnk");
    private static string StartLnk => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), Name + ".lnk");

    public static bool RunningInstalled =>
        string.Equals(Path.GetFullPath(SelfExe), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase);

    public static string SelfVersion =>
        FileVersionInfo.GetVersionInfo(SelfExe).ProductVersion?.Split('+')[0] ?? "?";

    /// <summary>Bản đã cài trong máy (null nếu chưa cài).</summary>
    public static string? InstalledVersion =>
        File.Exists(InstalledExe)
            ? FileVersionInfo.GetVersionInfo(InstalledExe).ProductVersion?.Split('+')[0] ?? "?"
            : null;

    public static void Install(bool desktopShortcut)
    {
        Directory.CreateDirectory(InstallDir);
        CloseInstalledInstances();

        // file đang bị chiếm (app cũ chưa tắt hẳn) thì thử lại vài lần
        for (int i = 0; ; i++)
        {
            try { File.Copy(SelfExe, InstalledExe, true); break; }
            catch (IOException) when (i < 10) { Thread.Sleep(300); }
        }

        Shortcut(StartLnk);
        if (desktopShortcut) Shortcut(DesktopLnk);
        else TryDelete(DesktopLnk);

        using var k = Registry.CurrentUser.CreateSubKey(UninstallKey);
        k.SetValue("DisplayName", "DCD_WatchNoAds — xem phim không quảng cáo");
        k.SetValue("DisplayVersion", SelfVersion);
        k.SetValue("Publisher", "DragonCloud");
        k.SetValue("DisplayIcon", InstalledExe + ",0");
        k.SetValue("InstallLocation", InstallDir);
        k.SetValue("UninstallString", $"\"{InstalledExe}\" --uninstall");
        k.SetValue("QuietUninstallString", $"\"{InstalledExe}\" --uninstall --quiet");
        k.SetValue("NoModify", 1, RegistryValueKind.DWord);
        k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        k.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord);
        k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
    }

    public static void LaunchInstalled(string? url = null)
    {
        var psi = new ProcessStartInfo(InstalledExe) { UseShellExecute = true, WorkingDirectory = InstallDir };
        if (!string.IsNullOrEmpty(url)) psi.ArgumentList.Add(url);
        Process.Start(psi);
    }

    /// <param name="removeData">Xoá luôn trang đã lưu, cookie, đăng nhập.</param>
    public static void Uninstall(bool removeData)
    {
        CloseInstalledInstances();   // app đang mở thì exe bị khoá, xoá không được
        TryDelete(DesktopLnk);
        TryDelete(StartLnk);
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }

        // exe đang chạy không tự xoá được mình -> nhờ cmd xoá sau khi app thoát
        var dirs = $"rmdir /s /q \"{InstallDir}\"";
        if (removeData) dirs += $" & rmdir /s /q \"{Store.Root}\"";
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 >nul & {dirs}")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    /// <summary>Tắt bản đang chạy trong thư mục cài, để chép đè được khi cập nhật.</summary>
    private static void CloseInstalledInstances()
    {
        foreach (var p in Process.GetProcessesByName(Name))
        {
            try
            {
                if (p.Id == Environment.ProcessId) continue;
                if (!string.Equals(p.MainModule?.FileName, InstalledExe, StringComparison.OrdinalIgnoreCase)) continue;
                p.CloseMainWindow();
                if (!p.WaitForExit(3000)) { p.Kill(); p.WaitForExit(3000); }
            }
            catch { }
        }
    }

    private static void Shortcut(string lnk)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lnk)!);
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t is null) return;
            dynamic sh = Activator.CreateInstance(t)!;
            dynamic s = sh.CreateShortcut(lnk);
            s.TargetPath = InstalledExe;
            s.WorkingDirectory = InstallDir;
            s.IconLocation = InstalledExe + ",0";
            s.Description = "Xem phim không quảng cáo";
            s.Save();
        }
        catch { }
    }

    private static void TryDelete(string f)
    {
        try { if (File.Exists(f)) File.Delete(f); } catch { }
    }
}

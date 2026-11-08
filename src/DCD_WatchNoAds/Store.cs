using System.IO;
using System.Text.Json;

namespace DCDWatch;

public record SavedPage(string Title, string Url);

/// <summary>
/// Cấu hình nhỏ: trang mở lần cuối, bật/tắt chặn quảng cáo, các trang đã lưu.
/// Nằm trong %LOCALAPPDATA%\DCD_WatchNoAds — exe để đâu cũng được, kể cả thư mục chỉ đọc.
/// </summary>
public class Store
{
    /// <summary>Đặt DCDW_PROFILE=thư mục để chạy thử với hồ sơ riêng, không đụng bản đang dùng.</summary>
    public static string Root { get; } =
        Environment.GetEnvironmentVariable("DCDW_PROFILE") is { Length: > 0 } p ? p
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DCD_WatchNoAds");

    /// <summary>Hồ sơ trình duyệt riêng (cookie, đăng nhập) — không dùng chung với DCDownload.</summary>
    public static string WebViewData => Path.Combine(Root, "wvdata");
    private static string File_ => Path.Combine(Root, "settings.json");

    public string LastUrl { get; set; } = "";
    public bool AdBlock { get; set; } = true;
    public bool Maximized { get; set; }
    public List<SavedPage> Saved { get; set; } = new();

    public static Store Load()
    {
        try
        {
            if (File.Exists(File_))
                return JsonSerializer.Deserialize<Store>(File.ReadAllText(File_)) ?? new Store();
        }
        catch { }
        return new Store();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(File_, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

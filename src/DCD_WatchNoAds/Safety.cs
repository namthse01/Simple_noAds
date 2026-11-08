using System.IO;
using Microsoft.Web.WebView2.Core;

namespace DCDWatch;

/// <summary>
/// Lớp an toàn luôn bật (kể cả khi bỏ tick "Chặn QC"): file chạy được, xin quyền, mở ứng dụng ngoài.
/// Đây là mấy đường mà trang lừa đảo / quảng cáo bẩn hay dùng để cài mã độc lên máy.
/// </summary>
public static class Safety
{
    /// <summary>Đuôi file chạy được hoặc tự cài — trang phim không có lý do gì để tải mấy thứ này.</summary>
    private static readonly HashSet<string> DangerousExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".msix", ".msixbundle", ".appx", ".appxbundle", ".appinstaller",
        ".bat", ".cmd", ".com", ".scr", ".pif", ".cpl", ".dll", ".sys", ".drv", ".ocx",
        ".ps1", ".psm1", ".psd1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta",
        ".lnk", ".url", ".scf", ".inf", ".reg", ".msc", ".msp", ".mst", ".gadget", ".application",
        ".jar", ".apk", ".xapk", ".iso", ".img", ".vhd", ".vhdx", ".chm", ".xll", ".library-ms", ".search-ms"
    };

    private static readonly string[] DangerousMime =
    [
        "application/x-msdownload", "application/x-msdos-program", "application/x-ms-installer",
        "application/vnd.microsoft.portable-executable", "application/x-msi", "application/hta",
        "application/vnd.android.package-archive", "application/java-archive"
    ];

    public static bool IsDangerousFile(string fileName, string? mime)
    {
        if (DangerousExt.Contains(Path.GetExtension(fileName))) return true;
        return mime is not null && DangerousMime.Any(m => mime.StartsWith(m, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Giao thức mở ứng dụng ngoài từng bị lợi dụng để chạy lệnh / mở thư mục mạng chứa mã độc —
    /// chặn luôn, không hỏi.
    /// </summary>
    private static readonly string[] DangerousScheme =
    [
        "search-ms", "search", "ms-msdt", "ms-officecmd", "ms-word", "ms-excel", "ms-powerpoint",
        "ms-appinstaller", "ms-cxh", "ms-cxh-full", "ms-settings", "ms-windows-store", "file",
        "vbscript", "javascript", "shell", "hcp", "its", "mk", "ms-its", "res", "jar"
    ];

    public static bool IsDangerousScheme(string uri)
    {
        var i = uri.IndexOf(':');
        var scheme = i > 0 ? uri[..i] : uri;
        return DangerousScheme.Contains(scheme, StringComparer.OrdinalIgnoreCase);
    }

    public static string PermissionName(CoreWebView2PermissionKind k) => k switch
    {
        CoreWebView2PermissionKind.Notifications => "gửi thông báo",
        CoreWebView2PermissionKind.Camera => "camera",
        CoreWebView2PermissionKind.Microphone => "micro",
        CoreWebView2PermissionKind.Geolocation => "vị trí",
        CoreWebView2PermissionKind.ClipboardRead => "đọc clipboard",
        CoreWebView2PermissionKind.MultipleAutomaticDownloads => "tự tải nhiều file",
        CoreWebView2PermissionKind.FileReadWrite => "đọc/ghi file trên máy",
        CoreWebView2PermissionKind.WindowManagement => "quản lý cửa sổ / màn hình",
        CoreWebView2PermissionKind.LocalFonts => "đọc font trên máy",
        CoreWebView2PermissionKind.OtherSensors => "cảm biến",
        CoreWebView2PermissionKind.MidiSystemExclusiveMessages => "thiết bị MIDI",
        _ => k.ToString()
    };

    public static string Host(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && !string.IsNullOrEmpty(u.Host) ? u.Host : "trang này";

    public static string Size(ulong? bytes) => bytes switch
    {
        null or 0 => "không rõ dung lượng",
        < 1024 => $"{bytes} byte",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024UL * 1024 * 1024 => $"{bytes / 1024.0 / 1024:0.#} MB",
        _ => $"{bytes / 1024.0 / 1024 / 1024:0.##} GB"
    };
}

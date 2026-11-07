# DCD_WatchNoAds

Trình duyệt nhỏ cho Windows để **xem phim không quảng cáo** — mà trang **không biết** là đang bị chặn,
nên không hiện bảng "hãy tắt adblock".

## Chặn lén thế nào

Trang chống adblock hay dò theo mấy kiểu dưới đây, và app né từng kiểu:

| Trang dò bằng cách | App làm gì |
|---|---|
| Tải script quảng cáo rồi xem có lỗi không | Trả **200 OK** kèm script rỗng, không trả lỗi |
| Kiểm tra biến `adsbygoogle`, `googletag` | Dựng sẵn bản giả chạy được |
| Script mồi `/ads.js` ngay trên site | Luật theo đường dẫn chỉ áp cho **tên miền ngoài** |
| Thẻ mồi `class="ad-banner"` rồi đo chiều cao | Không ẩn theo tên class, chỉ làm trong suốt khung QC thật |
| Kiểm tra `window.open` có bị thay không | Trả về cửa sổ giả, `toString()` vẫn ra `[native code]` |

Ngoài danh sách host quảng cáo, app còn bắt script nạp quảng cáo từ **tên miền ngẫu nhiên**
(đổi liên tục, kiểu `cd.dirdumaigre.com/s91AwcQjSTBG/152727`) theo hình dạng link.

## Tính năng

- Video bấm toàn màn hình → cả cửa sổ toàn màn hình · `F11` bật/tắt · `Esc` thoát · `Ctrl+L` tới thanh địa chỉ
- ☆ lưu trang, ☰ mở trang đã lưu · mở app lên là quay lại đúng trang đang xem lần trước
- Cửa sổ bật ngầm bị chặn; tab mới sang tên miền khác bị chặn nhưng hiện nút **↗ Mở link vừa chặn**
- Bỏ tick **Chặn QC** nếu trang nào hiển thị lỗi

## Build & cài

Cần [.NET SDK 10](https://dotnet.microsoft.com/download) và WebView2 Runtime (Windows 10/11 có sẵn).

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1            # build + cài + shortcut Desktop/Start Menu
powershell -ExecutionPolicy Bypass -File build.ps1 -NoInstall # chỉ build ra dist\DCD_WatchNoAds.exe
```

`dist\DCD_WatchNoAds.exe` là một file duy nhất, tự chứa .NET runtime (~59 MB) — chép sang máy khác chạy luôn.

Chạy thử khi sửa code: `dotnet run --project src\DCD_WatchNoAds`

## Cấu trúc

```
src/DCD_WatchNoAds/
  MainWindow.xaml(.cs)   cửa sổ: thanh địa chỉ, toàn màn hình, trang đã lưu, chặn tab mới
  StealthBlock.cs        luật chặn + trả lời giả + script chạy trong trang
  Store.cs               cấu hình (%LOCALAPPDATA%\DCD_WatchNoAds\settings.json)
  Theme.xaml             bảng màu Catppuccin Mocha
build.ps1                đóng gói + cài
```

Hồ sơ trình duyệt (cookie, đăng nhập) nằm ở `%LOCALAPPDATA%\DCD_WatchNoAds\wvdata` — không nằm trong repo.

## Soi trang khi quảng cáo lọt

Đặt biến môi trường `DCDW_DEBUG_PORT=9222` trước khi chạy app, rồi mở `http://127.0.0.1:9222`
(hoặc nối DevTools Protocol) để xem trang đang nạp script nào.
"# Simple_noAds" 

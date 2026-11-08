# DCD_WatchNoAds

Trình duyệt nhỏ cho Windows để **xem phim không quảng cáo** — mà trang **không biết** là đang bị chặn,
nên không hiện bảng "hãy tắt adblock".

## Cài đặt

1. Tải **`DCD_WatchNoAds.exe`** ở [Releases](https://github.com/namthse01/Simple_noAds/releases)
2. Nháy đúp → bấm **Cài đặt** → xong, có shortcut ngoài Desktop

Không cần cài .NET hay gì khác, không cần quyền admin. Windows 10/11.

- Windows hiện **"Windows protected your PC"** (do exe chưa ký số) → bấm **More info** → **Run anyway**.
- Có bản mới: tải exe mới về, nháy đúp → **Cập nhật**. Trang đã lưu vẫn còn.
- Gỡ: **Settings → Apps → Installed apps → DCD_WatchNoAds → Uninstall**.
- Không muốn cài: bấm **Chạy luôn, không cài** trên bảng cài đặt.

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

### Banner có nút tắt — ẩn luôn từ đầu

Banner cá cược / catfish do chính trang phim tự đăng (không qua mạng quảng cáo) thường kèm nút
**"Tắt QC ×"**. App dùng nút đó làm dấu để tìm khung banner rồi **ẩn hẳn khung**. App **không bấm** nút nào,
và banner được ẩn trước khi trang kịp vẽ nên không chớp lên lần nào, kể cả banner chèn vào muộn.

- Nhận nút tắt ở nhiều thứ tiếng: Việt, Anh, Trung, Nhật, Hàn, Hindi, Thái, Indonesia, Nga, Ả Rập,
  Tây Ban Nha, Bồ Đào Nha, Pháp, Đức, Thổ Nhĩ Kỳ… Nút chỉ có icon thì app đọc nhãn ẩn của nút (`aria-label`, `title`).
- Nút ghi rõ "tắt + quảng cáo" (`Tắt QC`, `Close ad`, `광고 닫기`, `关闭广告`…) được tin ngay.
  Nút chung chung (`×`, `Close`) chỉ tính khi khung quanh nó có ảnh dẫn link ra tên miền ngoài.
- App không đụng vào khung có video, ô nhập, nhiều chữ hoặc chiếm quá nửa màn hình
  (player, form đăng nhập, bài viết).
- Banner **không có** nút tắt thì không bị ẩn theo cách này.

## Tính năng

- Video bấm toàn màn hình → cả cửa sổ toàn màn hình · `F11` bật/tắt · `Esc` thoát · `Ctrl+L` tới thanh địa chỉ
- ☆ lưu trang, ☰ mở trang đã lưu · mở app lên là quay lại đúng trang đang xem lần trước
- Cửa sổ bật ngầm bị chặn; tab mới sang tên miền khác bị chặn nhưng hiện nút **↗ Mở link vừa chặn**
- Bỏ tick **Chặn QC** nếu trang nào hiển thị lỗi

## Build từ mã nguồn

Cần [.NET SDK 10](https://dotnet.microsoft.com/download) và WebView2 Runtime (Windows 10/11 có sẵn).

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1            # build + cài vào máy này
powershell -ExecutionPolicy Bypass -File build.ps1 -NoInstall # chỉ build ra dist\DCD_WatchNoAds.exe
```

`dist\DCD_WatchNoAds.exe` là **một file duy nhất** (~59 MB, tự chứa .NET runtime), vừa là app vừa là bộ cài —
file này đem lên Releases hoặc gửi thẳng cho người khác.

Lệnh dòng lệnh của exe: `--install` (cài im lặng) · `--uninstall [--quiet]` · `--portable` (chạy không cài).

Chạy thử khi sửa code: `dotnet run --project src\DCD_WatchNoAds` (bản Debug không hỏi cài).

## Cấu trúc

```
src/DCD_WatchNoAds/
  MainWindow.xaml(.cs)   cửa sổ: thanh địa chỉ, toàn màn hình, trang đã lưu, chặn tab mới
  StealthBlock.cs        luật chặn + trả lời giả + script chạy trong trang
  Store.cs               cấu hình (%LOCALAPPDATA%\DCD_WatchNoAds\settings.json)
  Setup.cs               tự cài / cập nhật / gỡ cài đặt (%LOCALAPPDATA%\Programs\DCD_WatchNoAds)
  InstallWindow.xaml     bảng cài đặt khi chạy exe từ ngoài thư mục cài
  Theme.xaml             bảng màu Catppuccin Mocha
build.ps1                đóng gói + cài
```

Hồ sơ trình duyệt (cookie, đăng nhập) nằm ở `%LOCALAPPDATA%\DCD_WatchNoAds\wvdata` — không nằm trong repo.

## Soi trang khi quảng cáo lọt

Đặt biến môi trường `DCDW_DEBUG_PORT=9222` trước khi chạy app, rồi mở `http://127.0.0.1:9222`
(hoặc nối DevTools Protocol) để xem trang đang nạp script nào.
"# Simple_noAds" 

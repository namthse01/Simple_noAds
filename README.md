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

## An toàn khi duyệt web

Trang phim lậu và quảng cáo bẩn hay tìm cách đưa người xem sang trang lừa đảo hoặc cài mã độc lên máy.
App chặn các đường đó như sau:

| Trò của trang | App làm gì |
|---|---|
| Bấm vào đâu cũng bị lái sang trang khác (`location.href = ...`) | Chặn. Chỉ cho đi khi **bạn bấm một link thật** |
| Link trong suốt phủ cả trang, bấm đâu cũng trúng | Chặn |
| Tự chuyển trang sau vài giây, hoặc dùng meta refresh | Chặn |
| Mở tab hoặc cửa sổ mới (popup, popunder) | Chặn |
| Tự tải `.exe`, `.msi`, `.bat`, `.apk`, `.iso`… | Chặn thẳng, không hỏi |
| Tải file khác (`.zip`, `.pdf`…) | Hỏi bạn trước, mặc định là **No** |
| Xin quyền gửi thông báo ("bấm Cho phép để chứng minh không phải robot"), camera, micro, vị trí, clipboard | Tự từ chối, không hiện hộp hỏi |
| Mở ứng dụng ngoài kiểu lừa (`search-ms:`, `ms-msdt:`…) | Chặn thẳng |
| Mở ứng dụng ngoài khác (`mailto:`, `magnet:`…) | Trang tự đòi thì chặn. Bạn tự bấm thì app hỏi trước |
| Trang hoặc file đã bị báo là lừa đảo, mã độc | Microsoft SmartScreen chặn |

- Chuyển trang trong cùng một site, bấm link thật, nút quay lại và tải lại trang vẫn chạy bình thường.
- Mỗi lần chặn, thanh công cụ hiện một dòng **⚠** cho biết vừa chặn gì. Chặn chuyển trang hoặc tab mới thì có thêm
  nút **↗ Mở link vừa chặn** trong 10 giây, nếu bạn thật sự muốn đi.
- Phần tải file, xin quyền và mở ứng dụng ngoài **luôn bật**, kể cả khi bỏ tick **Chặn QC**.
- Vì từ chối mọi quyền, các trang cần camera hay micro (gọi video, họp online) sẽ không dùng được trong app.

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
  StealthBlock.cs        luật chặn + trả lời giả + script chạy trong trang (ẩn QC, chặn chuyển trang lén)
  Safety.cs              chặn file chạy được, quyền, mở ứng dụng ngoài
  Store.cs               cấu hình (%LOCALAPPDATA%\DCD_WatchNoAds\settings.json)
  Setup.cs               tự cài / cập nhật / gỡ cài đặt (%LOCALAPPDATA%\Programs\DCD_WatchNoAds)
  InstallWindow.xaml     bảng cài đặt khi chạy exe từ ngoài thư mục cài
  Theme.xaml             bảng màu Catppuccin Mocha
build.ps1                đóng gói + cài
```

Hồ sơ trình duyệt (cookie, đăng nhập) nằm ở `%LOCALAPPDATA%\DCD_WatchNoAds\wvdata` — không nằm trong repo.

## Soi trang khi quảng cáo lọt

Chạy thử với hồ sơ riêng (không đụng bản đang dùng): đặt `DCDW_PROFILE=<thư mục>`.

Đặt biến môi trường `DCDW_DEBUG_PORT=9222` trước khi chạy app, rồi mở `http://127.0.0.1:9222`
(hoặc nối DevTools Protocol) để xem trang đang nạp script nào.
"# Simple_noAds" 

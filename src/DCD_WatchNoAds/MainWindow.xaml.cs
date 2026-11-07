using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace DCDWatch;

/// <summary>
/// Trình duyệt một khung để xem phim: chặn quảng cáo lén, chặn cửa sổ ngầm,
/// video bấm toàn màn hình thì cả cửa sổ toàn màn hình.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Store _store = Store.Load();
    private CoreWebView2Environment? _env;
    private string? _scriptId;
    private bool _adOn;
    private int _blocked;

    /// <summary>Trang đang mở — không bao giờ chặn chính nó.</summary>
    private string _topUrl = "";
    /// <summary>Lần điều hướng này do người dùng tự gõ/chọn — được đi kể cả tới host quảng cáo.</summary>
    private bool _typed;

    /// <summary>Link mở tab mới sang tên miền khác vừa bị chặn — người dùng muốn thì bấm mở.</summary>
    private string? _lastPopup;
    private readonly DispatcherTimer _popupTimer = new() { Interval = TimeSpan.FromSeconds(10) };

    private bool _full;
    private WindowState _prevState;
    private WindowStyle _prevStyle;
    private ResizeMode _prevResize;

    public MainWindow()
    {
        InitializeComponent();
        _adOn = _store.AdBlock;
        ChkAdBlock.IsChecked = _adOn;
        if (_store.Maximized) WindowState = WindowState.Maximized;
        _popupTimer.Tick += (_, _) => { _popupTimer.Stop(); BtnPopup.Visibility = Visibility.Collapsed; };

        Loaded += async (_, _) => await InitAsync();
        Closed += (_, _) =>
        {
            _store.AdBlock = _adOn;
            _store.Maximized = (_full ? _prevState : WindowState) == WindowState.Maximized;
            _store.Save();
            Web.Dispose();
        };
    }

    private async Task InitAsync()
    {
        try
        {
            _env = await CoreWebView2Environment.CreateAsync(null, Store.WebViewData);
            await Web.EnsureCoreWebView2Async(_env);
        }
        catch (Exception ex)
        {
            // Windows 11 có sẵn; vài máy Windows 10 cũ thì chưa
            var r = MessageBox.Show("Không khởi động được trình duyệt nhúng (WebView2).\n" +
                            "Máy có thể chưa cài Microsoft Edge WebView2 Runtime.\n\n" +
                            "Mở trang tải WebView2 về cài không? (miễn phí, của Microsoft)\n\n" + ex.Message,
                "DCD_WatchNoAds", MessageBoxButton.YesNo, MessageBoxImage.Error);
            if (r == MessageBoxResult.Yes)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                        "https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true });
                }
                catch { }
            }
            Close();
            return;
        }

        var core = Web.CoreWebView2;
        var s = core.Settings;
        s.AreDefaultContextMenusEnabled = true;
        s.IsStatusBarEnabled = false;      // khung link góc dưới — đỡ che phụ đề
        s.AreDevToolsEnabled = true;

        // 3 tham số = thấy cả request bên trong iframe khác tên miền (player nhúng)
        core.AddWebResourceRequestedFilter("*",
            CoreWebView2WebResourceContext.All,
            CoreWebView2WebResourceRequestSourceKinds.All);
        core.WebResourceRequested += OnRequest;

        core.NavigationStarting += (_, e) =>
        {
            bool typed = _typed;
            _typed = false;

            // quảng cáo lái cả trang sang chỗ khác (top.location = ...)
            if (_adOn && !typed && StealthBlock.IsAdHost(e.Uri))
            {
                e.Cancel = true;
                Blocked();
                return;
            }

            _topUrl = e.Uri;
            if (e.IsRedirected) return;
            _blocked = 0;
            UpdateCount();
        };

        core.SourceChanged += (_, _) =>
        {
            if (!TxtAddr.IsKeyboardFocusWithin)
                TxtAddr.Text = core.Source.StartsWith("http") ? core.Source : "";
            // lưu ngay: app bị tắt ngang (tắt máy, kill) vẫn mở lại đúng tập đang xem
            if (core.Source.StartsWith("http") && core.Source != _store.LastUrl)
            {
                _store.LastUrl = core.Source;
                _store.Save();
            }
            UpdateStar();
        };
        core.DocumentTitleChanged += (_, _) =>
            Title = string.IsNullOrWhiteSpace(core.DocumentTitle)
                ? "DCD_WatchNoAds" : core.DocumentTitle + " — DCD_WatchNoAds";
        core.HistoryChanged += (_, _) =>
        {
            BtnBack.IsEnabled = core.CanGoBack;
            BtnFwd.IsEnabled = core.CanGoForward;
        };

        // video bấm toàn màn hình -> cả cửa sổ toàn màn hình
        core.ContainsFullScreenElementChanged += (_, _) => SetFullscreen(core.ContainsFullScreenElement);

        core.NewWindowRequested += OnNewWindow;

        await ApplyAdBlockScriptAsync();

        var start = StartUrl();
        if (start is null) ShowHome();
        else Go(start);
    }

    /// <summary>Trang mở đầu tông tối: hướng dẫn ngắn + các trang đã lưu.</summary>
    private void ShowHome()
    {
        static string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);
        var links = string.Concat(_store.Saved.Select(p =>
            $"<a href=\"{Esc(p.Url)}\">{Esc(p.Title)}<small>{Esc(p.Url)}</small></a>"));
        if (links.Length == 0) links = "<p class=m>Chưa lưu trang nào — mở trang phim rồi bấm ☆ trên thanh công cụ.</p>";

        var html = $$"""
            <!doctype html><html><head><meta charset="utf-8"><style>
            body{margin:0;background:#1E1E2E;color:#CDD6F4;font:14px 'Segoe UI',sans-serif;
                 display:flex;justify-content:center;padding-top:9vh}
            main{width:min(640px,90vw)}
            h1{font-size:26px;margin:0 0 6px}
            .m{color:#9399B2;line-height:1.6}
            h2{font-size:13px;color:#9399B2;text-transform:uppercase;letter-spacing:.08em;margin:28px 0 8px}
            a{display:block;background:#252537;border-radius:9px;padding:11px 14px;margin:6px 0;
              color:#CDD6F4;text-decoration:none}
            a:hover{background:#313244}
            small{display:block;color:#6C7086;font-size:11.5px;margin-top:2px;overflow:hidden;
                  text-overflow:ellipsis;white-space:nowrap}
            kbd{background:#313244;border-radius:4px;padding:1px 6px;font-size:12px}
            </style></head><body><main>
            <h1>DCD_WatchNoAds</h1>
            <p class=m>Gõ địa chỉ trang phim lên thanh trên cùng. Quảng cáo và cửa sổ bật ngầm bị chặn lén —
            trang vẫn tưởng bạn không dùng chặn quảng cáo.<br>
            <kbd>F11</kbd> toàn màn hình · <kbd>Esc</kbd> thoát · <kbd>Ctrl+L</kbd> tới thanh địa chỉ</p>
            <h2>Trang đã lưu</h2>{{links}}
            </main></body></html>
            """;
        try { Web.CoreWebView2.NavigateToString(html); } catch { }
        TxtAddr.Text = "";
        TxtAddr.Focus();
    }

    private string? StartUrl()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1].StartsWith("http")) return args[1];
        return string.IsNullOrEmpty(_store.LastUrl) ? null : _store.LastUrl;
    }

    // ================= chặn =================

    private void OnRequest(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!_adOn || _env is null) return;
        var url = e.Request.Uri;
        if (!StealthBlock.ShouldBlock(url, e.ResourceContext, _topUrl)) return;

        string? origin = null;
        try { if (e.Request.Headers.Contains("Origin")) origin = e.Request.Headers.GetHeader("Origin"); }
        catch { }

        e.Response = StealthBlock.Fake(_env, e.ResourceContext, url, origin);
        Blocked();
    }

    /// <summary>
    /// Mở tab mới: không bao giờ bật cửa sổ mới. Cùng site và người dùng tự bấm thì mở ngay tại đây;
    /// sang tên miền khác (popunder hay đội lốt link) thì chặn, nhưng hiện nút để mở nếu thật sự cần.
    /// </summary>
    private void OnNewWindow(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (!_adOn) return;
        e.Handled = true;

        var uri = e.Uri;
        if (!e.IsUserInitiated || StealthBlock.IsAdHost(uri)) { Blocked(); return; }

        if (Uri.TryCreate(uri, UriKind.Absolute, out var to)
            && Uri.TryCreate(_topUrl, UriKind.Absolute, out var here)
            && StealthBlock.SameSite(to.Host, here.Host))
        {
            Go(uri);
            return;
        }

        Blocked();
        Dispatcher.BeginInvoke(() =>
        {
            _lastPopup = uri;
            BtnPopup.ToolTip = uri;
            BtnPopup.Visibility = Visibility.Visible;
            _popupTimer.Stop();
            _popupTimer.Start();
        });
    }

    private void Blocked()
    {
        _blocked++;
        if (Dispatcher.CheckAccess()) UpdateCount();
        else Dispatcher.BeginInvoke(UpdateCount);
    }

    private void UpdateCount() =>
        LblBlocked.Text = _adOn ? (_blocked > 0 ? "🛡 " + _blocked : "🛡") : "";

    private async Task ApplyAdBlockScriptAsync()
    {
        var core = Web?.CoreWebView2;
        if (core is null) return;
        try
        {
            if (_adOn && _scriptId is null)
                _scriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(StealthBlock.PageScript);
            else if (!_adOn && _scriptId is not null)
            {
                core.RemoveScriptToExecuteOnDocumentCreated(_scriptId);
                _scriptId = null;
            }
        }
        catch { }
        UpdateCount();
    }

    private async void ChkAdBlock_Changed(object sender, RoutedEventArgs e)
    {
        _adOn = ChkAdBlock.IsChecked == true;
        // sự kiện này bắn ngay lúc dựng giao diện, khi Web còn chưa có
        if (Web?.CoreWebView2 is null) return;
        await ApplyAdBlockScriptAsync();
        _blocked = 0;
        try { Web.CoreWebView2.Reload(); } catch { }
    }

    // ================= toàn màn hình =================

    private void SetFullscreen(bool on)
    {
        if (on == _full) return;
        _full = on;
        if (on)
        {
            _prevState = WindowState;
            _prevStyle = WindowStyle;
            _prevResize = ResizeMode;
            Bar.Visibility = Visibility.Collapsed;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            // phải về Normal rồi mới Maximized thì mới phủ cả thanh tác vụ
            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
        }
        else
        {
            Bar.Visibility = Visibility.Visible;
            WindowStyle = _prevStyle;
            ResizeMode = _prevResize;
            WindowState = _prevState;
        }
    }

    /// <summary>
    /// F11: cho cả trang vào chế độ toàn màn hình của trình duyệt — Esc thoát được ngay,
    /// vì phím Esc khi đang bấm trong trang không tới được cửa sổ WPF.
    /// </summary>
    private async Task ToggleFullscreenAsync()
    {
        var core = Web?.CoreWebView2;
        if (core is null) return;
        var js = core.ContainsFullScreenElement
            ? "document.exitFullscreen()"
            : "document.documentElement.requestFullscreen()";
        try
        {
            await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate",
                JsonSerializer.Serialize(new { expression = js, userGesture = true }));
        }
        catch { }
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            e.Handled = true;
            await ToggleFullscreenAsync();
        }
        else if (e.Key == Key.Escape && _full)
        {
            e.Handled = true;
            await ToggleFullscreenAsync();
        }
        else if ((e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
                 || (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Alt))
        {
            e.Handled = true;
            if (_full) await ToggleFullscreenAsync();
            TxtAddr.Focus();
            TxtAddr.SelectAll();
        }
    }

    // ================= điều hướng =================

    private void Go(string url)
    {
        url = url.Trim();
        if (url.Length == 0) return;
        if (!url.Contains("://"))
        {
            // có dấu chấm, không dấu cách -> địa chỉ; còn lại -> tìm kiếm
            url = url.Contains('.') && !url.Contains(' ')
                ? "https://" + url
                : "https://www.google.com/search?q=" + Uri.EscapeDataString(url);
        }
        _typed = true;
        try { Web.CoreWebView2?.Navigate(url); }
        catch { _typed = false; }
        TxtAddr.Text = url;
    }

    private void BtnGo_Click(object sender, RoutedEventArgs e)
    {
        Go(TxtAddr.Text);
        Web.Focus();
    }

    private void TxtAddr_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            Go(TxtAddr.Text);
            Web.Focus();
        }
        else if (e.Key == Key.Escape)
        {
            TxtAddr.Text = Web.CoreWebView2?.Source ?? "";
            Web.Focus();
        }
    }

    private void TxtAddr_GotFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        Dispatcher.BeginInvoke(TxtAddr.SelectAll, DispatcherPriority.Input);

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2?.CanGoBack == true) Web.CoreWebView2.GoBack();
    }

    private void BtnFwd_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2?.CanGoForward == true) Web.CoreWebView2.GoForward();
    }

    private void BtnReload_Click(object sender, RoutedEventArgs e) => Web.CoreWebView2?.Reload();

    private void BtnPopup_Click(object sender, RoutedEventArgs e)
    {
        BtnPopup.Visibility = Visibility.Collapsed;
        if (_lastPopup is not null) Go(_lastPopup);
    }

    // ================= trang đã lưu =================

    private string CurrentUrl => Web.CoreWebView2?.Source ?? "";

    private void UpdateStar() =>
        BtnStar.Content = _store.Saved.Any(p => p.Url == CurrentUrl) ? "★" : "☆";

    private void BtnStar_Click(object sender, RoutedEventArgs e)
    {
        var url = CurrentUrl;
        if (!url.StartsWith("http")) return;
        var hit = _store.Saved.FirstOrDefault(p => p.Url == url);
        if (hit is not null) _store.Saved.Remove(hit);
        else
        {
            var t = Web.CoreWebView2?.DocumentTitle;
            _store.Saved.Insert(0, new SavedPage(string.IsNullOrWhiteSpace(t) ? url : t, url));
        }
        _store.Save();
        UpdateStar();
    }

    private void BtnSaved_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = BtnSaved,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };

        if (_store.Saved.Count == 0)
            menu.Items.Add(new MenuItem { Header = "Chưa lưu trang nào — bấm ☆ để lưu trang đang xem", IsEnabled = false });

        foreach (var p in _store.Saved)
        {
            var title = p.Title.Length > 70 ? p.Title[..70] + "…" : p.Title;
            var item = new MenuItem { Header = title, ToolTip = p.Url };
            var url = p.Url;
            item.Click += (_, _) => Go(url);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }
}

using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;

namespace DCDWatch;

/// <summary>
/// Chặn quảng cáo kiểu "lén": trang KHÔNG được biết là mình đang bị chặn.
///
/// Trang chống adblock phát hiện bằng mấy trò quen thuộc, nên mỗi trò có một cách né:
///  • Tải script quảng cáo rồi xem có lỗi không  → trả 200 + script rỗng/giả, không trả lỗi.
///  • Kiểm tra biến của script quảng cáo (adsbygoogle, googletag) → dựng sẵn bản giả chạy được.
///  • Script mồi "/ads.js" ngay trên site để đặt một biến → luật theo đường dẫn chỉ áp cho
///    tên miền NGOÀI, script của chính site không bao giờ bị đụng.
///  • Thẻ mồi có class kiểu "ad-banner" rồi đo chiều cao → không ẩn theo tên class nữa,
///    chỉ làm trong suốt khung quảng cáo thật (giữ nguyên kích thước).
///  • window.open bị thay → bản thay trả về cửa sổ giả và tự nhận là "[native code]".
/// </summary>
public static class StealthBlock
{
    /// <summary>Host quảng cáo / theo dõi / mở cửa sổ ngầm.</summary>
    private static readonly Regex AdHost = new(
        @"(^|\.)("
        + @"doubleclick\.net|googlesyndication\.com|googleadservices\.com|googletagservices\.com|"
        + @"adservice\.google\.\w+|"
        + @"adnxs\.com|rubiconproject\.com|pubmatic\.com|openx\.net|criteo\.(com|net)|"
        + @"popads\.net|popcash\.net|poptm\.com|propellerads\.com|propu\.sh|"
        + @"exoclick\.com|exosrv\.com|realsrv\.com|pemsrv\.com|juicyads\.com|trafficjunky\.(net|com)|"
        + @"trafficfactory\.biz|tsyndicate\.com|adsterra\.com|hilltopads\.net|clickadu\.com|adcash\.com|"
        + @"ad-maven\.com|onclickalgo\.com|onclickperformance\.com|zeropark\.com|galaksion\.com|"
        + @"highperformanceformat\.com|\w*ratecpm\w*\.com|\w*cpmrate\w*\.com|\w*gatecpm\w*\.com|"
        + @"a-ads\.com|adskeeper\.(com|co\.uk)|"
        + @"mgid\.com|taboola\.com|outbrain\.com|revcontent\.com|bidvertiser\.com|"
        + @"adform\.net|smartadserver\.com|adroll\.com|scorecardresearch\.com|"
        + @"vietadx\.\w+|admicro\.vn|adtima\.vn"
        + @")$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Đường dẫn kiểu quảng cáo — chỉ áp cho tài nguyên phụ của tên miền NGOÀI.</summary>
    private static readonly Regex AdPath = new(
        @"/(ads|adserver|adframe|adservice|advert|adverts|advertising|advertisement|"
        + @"popunder|popads|banners)/|"
        + @"/(ads|adsense|popunder|popup|banner)\.(js|php|html?)(\?|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Đuôi file phim / phụ đề — luật đường dẫn không bao giờ chặn mấy thứ này.</summary>
    private static readonly Regex MediaPath = new(
        @"\.(m3u8|mpd|mp4|m4s|m4v|m4a|ts|webm|mkv|mp3|aac|vtt|srt|ass)(\?|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Script nạp quảng cáo từ tên miền "dùng một lần" (đổi liên tục nên không liệt kê được),
    /// nhận theo hình dạng link: không có đuôi file, đoạn đầu là chuỗi ngẫu nhiên lẫn hoa-thường-số.
    ///   cd.dirdumaigre.com/s91AwcQjSTBG/152727      on.vitalickitten.com/rtoj1KyNyvSqv5nsf/VNGBo
    /// Chỉ áp cho SCRIPT của tên miền ngoài — iframe player phim hay có link kiểu này, không đụng.
    /// </summary>
    private static readonly Regex RandomLoader = new(
        @"^/(?=[A-Za-z0-9]*[A-Z])(?=[A-Za-z0-9]*[a-z])(?=[A-Za-z0-9]*[0-9])[A-Za-z0-9]{10,}(/[A-Za-z0-9_-]+)*/?$",
        RegexOptions.Compiled);

    public static bool IsAdHost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && AdHost.IsMatch(u.Host);

    /// <param name="topUrl">Trang đang mở — dùng để biết request là của chính site hay tên miền ngoài.</param>
    public static bool ShouldBlock(string url, CoreWebView2WebResourceContext ctx, string topUrl)
    {
        if (string.IsNullOrEmpty(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

        // chính trang người dùng mở thì không bao giờ chặn
        if (string.Equals(url, topUrl, StringComparison.OrdinalIgnoreCase)) return false;

        if (AdHost.IsMatch(uri.Host)) return true;

        if (ctx == CoreWebView2WebResourceContext.Document
            || ctx == CoreWebView2WebResourceContext.Media
            || MediaPath.IsMatch(uri.AbsolutePath)) return false;

        // Script mồi chống adblock hay nằm ngay trên site ("/ads.js" đặt một biến) — tha.
        if (Uri.TryCreate(topUrl, UriKind.Absolute, out var top) && SameSite(uri.Host, top.Host)) return false;

        if (ctx == CoreWebView2WebResourceContext.Script && RandomLoader.IsMatch(uri.AbsolutePath)) return true;

        return AdPath.IsMatch(uri.AbsolutePath);
    }

    /// <summary>So tên miền gốc (2 nhãn cuối, 3 nếu kiểu .com.vn) — đủ dùng, không cần bảng PSL.</summary>
    public static bool SameSite(string a, string b) =>
        string.Equals(Base(a), Base(b), StringComparison.OrdinalIgnoreCase);

    private static string Base(string host)
    {
        var p = host.Split('.');
        if (p.Length <= 2) return host;
        int take = p[^2].Length <= 3 && p[^1].Length == 2 ? 3 : 2;   // .com.vn, .co.uk...
        return string.Join('.', p[^Math.Min(take, p.Length)..]);
    }

    // ================= trả lời giả =================

    private static readonly byte[] Gif1x1 = Convert.FromBase64String(
        "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    /// <summary>
    /// Câu trả lời "thành công" thay cho thứ bị chặn — trang tưởng đã tải được.
    /// Trả 204 hay lỗi mạng là trang phát hiện ngay (script onerror, ảnh hỏng).
    /// </summary>
    public static CoreWebView2WebResourceResponse Fake(
        CoreWebView2Environment env, CoreWebView2WebResourceContext ctx, string url, string? origin)
    {
        string type;
        byte[] body;
        switch (ctx)
        {
            case CoreWebView2WebResourceContext.Script:
                type = "application/javascript";
                body = Encoding.UTF8.GetBytes(Surrogate(url));
                break;
            case CoreWebView2WebResourceContext.Image:
                type = "image/gif";
                body = Gif1x1;
                break;
            case CoreWebView2WebResourceContext.Stylesheet:
                type = "text/css";
                body = [];
                break;
            case CoreWebView2WebResourceContext.Document:
                type = "text/html";
                body = Encoding.UTF8.GetBytes("<!doctype html><html><head></head><body></body></html>");
                break;
            default:
                // fetch/xhr tới máy chủ quảng cáo (VAST cho quảng cáo đầu video...) — rỗng = "không có QC"
                type = "text/plain";
                body = [];
                break;
        }

        var h = new StringBuilder("Content-Type: " + type);
        if (!string.IsNullOrEmpty(origin))
            h.Append("\r\nAccess-Control-Allow-Origin: " + origin + "\r\nAccess-Control-Allow-Credentials: true");
        else
            h.Append("\r\nAccess-Control-Allow-Origin: *");

        return env.CreateWebResourceResponse(new MemoryStream(body), 200, "OK", h.ToString());
    }

    /// <summary>Script giả cho các thư viện quảng cáo hay bị trang kiểm tra.</summary>
    private static string Surrogate(string url)
    {
        var u = url.ToLowerInvariant();
        if (u.Contains("adsbygoogle") || u.Contains("show_ads")) return AdSense;
        if (u.Contains("gpt.js") || u.Contains("pubads_impl")) return Gpt;
        return "";
    }

    private const string AdSense = """
        (function(){try{var q=window.adsbygoogle;var a={loaded:true,push:function(){}};
        window.adsbygoogle=a;
        var ins=document.querySelectorAll('ins.adsbygoogle');
        for(var i=0;i<ins.length;i++){ins[i].setAttribute('data-adsbygoogle-status','done');ins[i].setAttribute('data-ad-status','filled');}
        }catch(e){}})();
        """;

    private const string Gpt = """
        (function(){try{
        var P;var f=function(){return P;};
        P=new Proxy(function(){},{get:function(t,k){
          if(k==='then')return undefined;
          if(k==='getSlots'||k==='getTargetingKeys'||k==='getTargeting'||k==='getAttributeKeys')return function(){return [];};
          if(k==='getSlotElementId'||k==='getAdUnitPath'||k==='get'||k==='getAttribute')return function(){return '';};
          if(k==='isInitialLoadDisabled')return function(){return false;};
          if(k==='destroySlots'||k==='setPublisherProvidedId')return function(){return true;};
          return f;},apply:f});
        var g=window.googletag||{};var old=(g.cmd&&g.cmd.length)?Array.prototype.slice.call(g.cmd):[];
        var run=function(fn){try{if(typeof fn==='function')fn();}catch(e){}return 1;};
        g.cmd={push:function(){for(var i=0;i<arguments.length;i++)run(arguments[i]);return 1;}};
        g.apiReady=true;g.pubadsReady=true;
        ['pubads','defineSlot','defineOutOfPageSlot','sizeMapping','companionAds','content'].forEach(function(n){g[n]=f;});
        g.enableServices=function(){};g.display=function(){};g.destroySlots=function(){return true;};
        g.disablePublisherConsole=function(){};g.openConsole=function(){};g.setAdIframeTitle=function(){};
        window.googletag=g;old.forEach(run);
        }catch(e){}})();
        """;

    // ================= script chạy trong trang =================

    /// <summary>
    /// Chạy trước mọi script của trang, trong mọi khung (cả player nhúng từ tên miền khác).
    /// Không xoá thẻ nào, không ẩn theo tên class — chỉ làm trong suốt khung QC thật và cho
    /// lớp phủ nuốt cú bấm "xuyên thấu". Trang đo đạc DOM sẽ thấy mọi thứ y như bình thường.
    /// </summary>
    public const string PageScript = """
        (function () {
          'use strict';

          // ---- cho ham bi thay tu nhan la "[native code]" ----
          var nativeTS = Function.prototype.toString;
          var masks = new WeakMap();
          var mask = function (fn, name) {
            try { masks.set(fn, 'function ' + name + '() { [native code] }'); } catch (e) {}
            return fn;
          };
          try {
            var ts = function toString() {
              if (masks.has(this)) return masks.get(this);
              return nativeTS.call(this);
            };
            mask(ts, 'toString');
            Function.prototype.toString = ts;
          } catch (e) {}

          // ---- cua so ngam: tra ve cua so gia, trang tuong da mo duoc ----
          var fakeWin = function () {
            var noop = function () {};
            var w = {
              closed: false, opener: window, name: '', length: 0,
              location: { href: 'about:blank', assign: noop, replace: noop, reload: noop },
              close: function () { w.closed = true; },
              focus: noop, blur: noop, postMessage: noop, moveTo: noop, resizeTo: noop,
              addEventListener: noop, removeEventListener: noop
            };
            try { w.document = document.implementation.createHTMLDocument(''); } catch (e) {}
            w.window = w; w.self = w; w.top = w; w.parent = w;
            return w;
          };
          try { window.open = mask(function open() { return fakeWin(); }, 'open'); } catch (e) {}

          // ---- khung QC that: trong suot + khong bam duoc, GIU kich thuoc ----
          var css =
            'iframe[src*="doubleclick"],iframe[src*="googlesyndication"],iframe[src*="adservice"],' +
            'iframe[src*="exoclick"],iframe[src*="juicyads"],iframe[src*="popads"],iframe[src*="adsterra"],' +
            'iframe[src*="trafficjunky"],iframe[src*="mgid"],iframe[src*="taboola"],iframe[src*="outbrain"],' +
            'iframe[src*="a-ads.com"],iframe[src*="tsyndicate"],iframe[src*="adskeeper"]' +
            '{opacity:0 !important;pointer-events:none !important;}';

          var styled = false;
          var gan = function () {
            if (styled) return;
            try {
              var noi = document.head || document.documentElement;
              if (!noi) return;
              var s = document.createElement('style');
              s.textContent = css;
              noi.appendChild(s);
              styled = true;
            } catch (e) {}
          };

          // ---- lop phu trong suot nuot cu bam dau: cho bam xuyen qua, khong xoa ----
          var xuyen = function () {
            try {
              var vw = window.innerWidth, vh = window.innerHeight;
              if (!vw || !vh || !document.body) return;
              var nodes = document.querySelectorAll('a,div');
              for (var i = 0; i < nodes.length; i++) {
                var n = nodes[i];
                if (n.querySelector('video,iframe,img,canvas,button,input,svg')) continue;
                if ((n.textContent || '').trim().length) continue;
                var st = window.getComputedStyle(n);
                if (st.position !== 'fixed' && st.position !== 'absolute') continue;
                if ((parseInt(st.zIndex, 10) || 0) < 100) continue;
                if (st.pointerEvents === 'none') continue;
                var r = n.getBoundingClientRect();
                if (r.width >= vw * 0.8 && r.height >= vh * 0.8)
                  n.style.setProperty('pointer-events', 'none', 'important');
              }
            } catch (e) {}
          };

          var quet = function () { gan(); xuyen(); };
          try { document.addEventListener('DOMContentLoaded', quet); } catch (e) {}
          try {
            var lan = 0;
            var hen = setInterval(function () { quet(); if (++lan >= 20) clearInterval(hen); }, 1000);
          } catch (e) {}
          quet();
        })();
        """;
}

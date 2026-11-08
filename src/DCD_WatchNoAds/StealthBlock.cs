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

          // ---- QC co nut tat ("Tat QC x", "Close ad", "광고 닫기", "关闭广告"...): dung nut lam dau
          //      de tim khung QC roi AN HAN khung do (display:none). Khong bam, khong dung chuot. ----
          // bo dau, chu thuong — tu dien ben duoi cung di qua ham nay nen moi ngon ngu so khop nhu nhau
          var bo = function (s) {
            return (s || '').toLowerCase().normalize('NFD').replace(/\p{M}/gu, '')
              .replace(/đ/g, 'd').replace(/ß/g, 'ss').replace(/\s+/g, ' ').trim();
          };
          // "dong / an / bo qua" — nhieu ngon ngu
          var DONG = [
            'close', 'hide', 'skip', 'dismiss',                                  // en
            'tắt', 'đóng', 'ẩn', 'bỏ qua', 'đóng lại',                           // vi
            '关闭', '關閉', '关掉', '關掉', '隐藏', '隱藏', '跳过', '跳過', '收起',  // zh
            '閉じる', '非表示', 'スキップ', '消す',                               // ja
            '닫기', '숨기기', '건너뛰기', '숨김', '닫음',                         // ko
            'बंद', 'बंद करें', 'छोड़ें', 'छुपाएं', 'स्किप',                         // hi
            'ปิด', 'ข้าม', 'ซ่อน',                                                // th
            'tutup', 'lewati', 'sembunyikan',                                    // id / ms
            'cerrar', 'ocultar', 'saltar', 'omitir', 'fechar', 'pular',          // es / pt
            'fermer', 'masquer', 'passer', 'ignorer',                            // fr
            'schließen', 'ausblenden', 'überspringen',                           // de
            'chiudi', 'nascondi', 'salta', 'sluiten', 'verbergen', 'overslaan',  // it / nl
            'zamknij', 'ukryj', 'pomiń', 'kapat', 'gizle', 'atla',               // pl / tr
            'закрыть', 'скрыть', 'пропустить', 'закрити',                        // ru / uk
            'إغلاق', 'اغلاق', 'تخطي', 'إخفاء', 'بستن', 'סגור'                    // ar / fa / he
          ].map(bo);
          // "quang cao"
          var QC = [
            'ad', 'ads', 'advert', 'advertisement', 'advertising', 'sponsored', 'banner',
            'qc', 'quảng cáo', '广告', '廣告', '広告', '광고', 'विज्ञापन', 'ऐड', 'โฆษณา', 'iklan',
            'anuncio', 'anuncios', 'anúncio', 'publicidad', 'publicidade', 'pubblicità', 'pub',
            'publicité', 'annonce', 'werbung', 'anzeige', 'advertentie', 'reklam', 'reklama',
            'реклама', 'рекламу', 'إعلان', 'اعلان', 'الإعلان', 'تبلیغ', 'פרסומת'
          ].map(bo);
          var DAU_X = /[×✕✖✗✘╳❌]/g;
          // chu Latin thi phai dung thanh tu rieng ("an" khong khop "anime");
          // chu CJK / Thai khong co dau cach nen so chuoi con
          var coTu = function (t, ds) {
            for (var i = 0; i < ds.length; i++) {
              var w = ds[i];
              if (/^[a-z0-9 ]+$/.test(w)) {
                var p = t.indexOf(w);
                while (p >= 0) {
                  var a = t.charAt(p - 1), b = t.charAt(p + w.length);
                  if (!/[a-z0-9]/.test(a) && !/[a-z0-9]/.test(b)) return true;
                  p = t.indexOf(w, p + 1);
                }
              } else if (t.indexOf(w) >= 0) return true;
            }
            return false;
          };
          // 2 = chac chan nut tat QC (co ca "dong" lan "quang cao")
          // 1 = nut tat chung chung ("x", "Close") -> chi tin khi khung quanh no dung la QC
          var loai = function (raw) {
            var t = bo(raw);
            if (!t || t.length > 30) return 0;
            var g = t.replace(DAU_X, ' ').replace(/[\s\-–—_:|·•()\[\]!.,]+/g, ' ').trim();
            if (coTu(g, DONG) && coTu(g, QC)) return 2;
            if (!g || g === 'x' || DONG.indexOf(g) >= 0) return 1;
            return 0;
          };

          var hien = function (el) {
            try { return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length); } catch (e) { return false; }
          };
          var linkNgoai = function (a) {
            try {
              var h = a.getAttribute('href');
              if (!h || /^\s*(#|javascript:)/i.test(h)) return false;
              return new URL(h, location.href).hostname !== location.hostname;
            } catch (e) { return false; }
          };
          // co anh/iframe co banner (>= 60x30) — icon nho canh nut thi khong tinh
          var coBanner = function (el) {
            var ms = el.querySelectorAll('img,iframe,canvas,[style*="background-image"]');
            for (var i = 0; i < ms.length; i++) {
              var m = ms[i], r = m.getBoundingClientRect();
              if (r.width >= 60 && r.height >= 30) return true;
              // anh chua tai xong (chua co kich thuoc) van tinh — de an TRUOC khi banner kip hien;
              // tru khi khai bao san la anh nho (icon)
              if (m.tagName === 'IMG' && !(m.complete && m.naturalWidth)) {
                var w = parseInt(m.getAttribute('width'), 10);
                if (!(w > 0 && w < 60)) return true;
              }
            }
            return false;
          };
          // khung QC = to tien gan nhat co banner, gan nhu khong co chu, khong chua video
          var khungQC = function (nut, chac) {
            var el = nut.parentElement;
            for (var i = 0; el && i < 6; i++, el = el.parentElement) {
              if (el === document.body || el === document.documentElement) return null;
              if (!coBanner(el)) continue;
              if (el.querySelector('video,iframe[allowfullscreen],input,textarea')) return null;
              var chu = bo(el.textContent).replace(bo(nut.textContent), '');
              if (chu.length > 40) return null;
              var r = el.getBoundingClientRect();
              if (r.width * r.height > window.innerWidth * window.innerHeight * 0.6) return null;
              if (chac) return el;
              var as = el.querySelectorAll('a[href]');
              for (var j = 0; j < as.length; j++) if (linkNgoai(as[j])) return el;
              return el.querySelector('iframe') ? el : null;
            }
            return null;
          };
          var daAn = [];
          var an = function (box) {
            box.style.setProperty('display', 'none', 'important');
            if (daAn.indexOf(box) < 0) daAn.push(box);
          };
          // KHONG bam nut nao ca — chi an khung QC. Nut tat hien tre (sau vai giay) van tinh.
          var xet = function (nut, k) {
            if (!k) return;
            var box = khungQC(nut, k === 2);
            if (box) an(box);
          };
          var tatQC = function () {
            try {
              if (!document.body) return;
              for (var k = 0; k < daAn.length; k++)        // trang bat lai thi an lai
                if (hien(daAn[k])) daAn[k].style.setProperty('display', 'none', 'important');
              var w = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
              var seen = new Set(), n;
              while ((n = w.nextNode())) {
                var v = n.nodeValue;
                if (!v || v.length > 30 || !v.trim()) continue;
                var el = n.parentElement;
                // "Tat QC" va "x" hay nam o 2 the con canh nhau -> xet ca the cha
                for (var d = 0; el && d < 2; d++, el = el.parentElement) {
                  if (seen.has(el)) break;
                  seen.add(el);
                  if ((el.textContent || '').length > 30) break;
                  xet(el, loai(el.textContent));
                }
              }
              // nut chi co icon: doc nhan an (aria-label / title / alt) — moi ngon ngu nhu tren;
              // ten class/id kieu "close", "btn-close" chi tinh la nut chung chung
              var ic = document.querySelectorAll(
                '[aria-label],[title],img[alt],[class*="close" i],[id*="close" i],[class*="tat-qc" i],[class*="dong-qc" i]');
              for (var m = 0; m < ic.length; m++) {
                var e2 = ic[m];
                if (seen.has(e2) || (e2.textContent || '').trim().length > 3) continue;
                var kk = Math.max(loai(e2.getAttribute('aria-label')), loai(e2.getAttribute('title')),
                                  e2.tagName === 'IMG' ? loai(e2.getAttribute('alt')) : 0);
                if (!kk && /close|tat-qc|dong-qc/i.test((e2.getAttribute('class') || '') + ' ' + (e2.id || ''))) kk = 1;
                xet(e2, kk);
              }
            } catch (e) {}
          };

          var quet = function () { gan(); xuyen(); tatQC(); };
          try { document.addEventListener('DOMContentLoaded', quet); } catch (e) {}
          try {
            var lan = 0;
            var hen = setInterval(function () { quet(); if (++lan >= 20) clearInterval(hen); }, 1000);
          } catch (e) {}
          // Moi lan trang them the moi (ca luc dang tai trang lan dau) -> quet ngay trong khung hinh do,
          // TRUOC khi trinh duyet ve ra man hinh => banner khong kip hien len lan nao.
          try {
            // trang nao DOM doi lien tuc ma quet ton > 8ms thi gian ra 0.5s/lan cho do nang may
            var cho = false, cham = false;
            var chay = function () {
              var t0 = performance.now();
              cho = false; tatQC();
              cham = performance.now() - t0 > 8;
            };
            new MutationObserver(function () {
              if (cho) return;
              cho = true;
              if (cham) setTimeout(chay, 500); else requestAnimationFrame(chay);
            }).observe(document, { childList: true, subtree: true });
          } catch (e) {}
          quet();
        })();
        """;
}

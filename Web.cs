using System.Diagnostics;
using System.Drawing.Drawing2D;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace UsbDesktop;

class TabInfo { public string Title = "New tab"; public Image? Icon; public bool Loading; public WebView2 View = null!; public string Url = ""; }

class IconBtn : RoundButton
{
    public IconBtn(string glyph)
    {
        Glyph = glyph; Pill = true; BackColor = Color.White; ForeColor = Color.FromArgb(95, 99, 104); Hover = Color.FromArgb(232, 234, 237); GlyphScale = 0.42f;
    }
}

class ChromeTabs : Control
{
    public readonly List<TabInfo> Tabs = new(); public int Sel;
    public event Action<int>? Picked, CloseTab; public event Action? NewTab;
    int hover = -1;
    public ChromeTabs()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(222, 225, 230); Height = Ui.D(42); Font = Ui.F(9);
    }
    int TabW => Tabs.Count == 0 ? Ui.D(200) : Math.Min(Ui.D(240), Math.Max(Ui.D(90), (Width - Ui.D(70)) / Tabs.Count));
    Rectangle TabRect(int i) => new(Ui.D(8) + i * TabW, Ui.D(8), TabW, Height - Ui.D(8));
    Rectangle PlusRect => new(Ui.D(12) + Tabs.Count * TabW, Ui.D(11), Ui.D(30), Ui.D(30));
    Rectangle XRect(int i) { var r = TabRect(i); return new Rectangle(r.Right - Ui.D(30), r.Top + (r.Height - Ui.D(22)) / 2, Ui.D(22), Ui.D(22)); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(BackColor);
        var dark = Color.FromArgb(60, 64, 67);
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < Tabs.Count; i++)
            {
                bool sel = i == Sel; if ((pass == 0) == sel) continue;
                var r = TabRect(i);
                if (sel)
                {
                    using var p = U.Round(new Rectangle(r.X, r.Y, r.Width, r.Height + Ui.D(12)), Ui.D(10)); g.FillPath(Brushes.White, p);
                    g.FillRectangle(Brushes.White, r.X, r.Bottom - Ui.D(4), r.Width, Ui.D(8));
                }
                else if (i == hover) { using var p = U.Round(new Rectangle(r.X + 2, r.Y + Ui.D(3), r.Width - 4, r.Height - Ui.D(5)), Ui.D(8)); using var b = new SolidBrush(Color.FromArgb(205, 208, 214)); g.FillPath(b, p); }
                else if (i + 1 != Sel && i + 1 < Tabs.Count) { using var pen = new Pen(Color.FromArgb(170, 174, 180)); g.DrawLine(pen, r.Right, r.Top + Ui.D(8), r.Right, r.Bottom - Ui.D(8)); }
                var t = Tabs[i]; int ix = r.X + Ui.D(12), sz = Ui.D(16);
                if (t.Loading) { using var pen = new Pen(Color.FromArgb(26, 115, 232), Ui.D(2)); g.DrawArc(pen, ix, r.Y + (r.Height - sz) / 2, sz, sz, Environment.TickCount / 3 % 360, 270); }
                else if (t.Icon != null) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(t.Icon, ix, r.Y + (r.Height - sz) / 2, sz, sz); }
                else { using var b = new SolidBrush(Color.FromArgb(154, 160, 166)); g.FillEllipse(b, ix, r.Y + (r.Height - sz) / 2, sz, sz); }
                var tr = new Rectangle(ix + sz + Ui.D(8), r.Y, Math.Max(10, r.Width - sz - Ui.D(58)), r.Height);
                TextRenderer.DrawText(g, string.IsNullOrWhiteSpace(t.Title) ? "New tab" : t.Title, Font, tr, sel ? Color.FromArgb(32, 33, 36) : dark, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                if (sel || i == hover) { var xr = XRect(i); Glyphs.Draw(g, "close", new RectangleF(xr.X + xr.Width * 0.3f, xr.Y + xr.Height * 0.3f, xr.Width * 0.4f, xr.Height * 0.4f), dark, Ui.D(1) + 0.6f); }
            }
        var pr = PlusRect; Glyphs.Draw(g, "plus", new RectangleF(pr.X + pr.Width * 0.28f, pr.Y + pr.Height * 0.28f, pr.Width * 0.44f, pr.Height * 0.44f), dark, Ui.D(1) + 0.8f);
    }
    public void CloseTabAt(int i) => CloseTab?.Invoke(i);
    int Hit(Point p) { for (int i = 0; i < Tabs.Count; i++) if (TabRect(i).Contains(p)) return i; return -1; }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); var h = Hit(e.Location); if (h != hover) { hover = h; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); int h = Hit(e.Location);
        if (h >= 0) { if (e.Button == MouseButtons.Middle || XRect(h).Contains(e.Location)) CloseTab?.Invoke(h); else { Sel = h; Picked?.Invoke(h); Invalidate(); } }
        else if (PlusRect.Contains(e.Location)) NewTab?.Invoke();
    }
}

static class Web
{
    static CoreWebView2Environment? env;
    const string ChromeUA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";
    static readonly List<WeakReference<CoreWebView2>> alive = new();
    static System.Windows.Forms.Timer? keep;

    // Browser profile lives on the USB so logins travel with it. Falls back to the PC's own profile folder if the USB is read-only.
    static async Task<CoreWebView2Environment> Env()
    {
        if (env != null) return env;
        var dirs = new[] { Path.Combine(Paths.Data, "webview"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UsbDesktop", "webview") };
        Exception? last = null;
        foreach (var d in dirs)
        {
            try { Directory.CreateDirectory(d); env = await CoreWebView2Environment.CreateAsync(null, d, new CoreWebView2EnvironmentOptions("--disable-features=msSmartScreenProtection")); return env; }
            catch (WebView2RuntimeNotFoundException) { throw; }
            catch (Exception ex) { last = ex; }
        }
        throw last ?? new Exception("The browser couldn't start");
    }

    // Turns "session" cookies into long-lived ones so a sign-in survives closing the desktop and moving to another PC.
    static void KeepLogins(CoreWebView2 c)
    {
        alive.Add(new WeakReference<CoreWebView2>(c));
        if (keep != null) return;
        keep = new System.Windows.Forms.Timer { Interval = 15000 };
        keep.Tick += async (_, _) => { if (Cfg.Get("keeplogins", "1") != "1") return; foreach (var w in alive.ToList()) if (w.TryGetTarget(out var cw)) await Persist(cw); };
        keep.Start();
    }
    public static async Task Persist(CoreWebView2 c)
    {
        try
        {
            var list = await c.CookieManager.GetCookiesAsync(null);
            foreach (var ck in list.Where(x => x.IsSession && !x.Domain.Contains("localhost")))
            { ck.Expires = DateTime.Now.AddDays(180); c.CookieManager.AddOrUpdateCookie(ck); }
        }
        catch { }
    }
    public static async Task FlushAll() { if (Cfg.Get("keeplogins", "1") != "1") return; foreach (var w in alive.ToList()) if (w.TryGetTarget(out var cw)) await Persist(cw); }

    static string Engine(string q)
    {
        var e = Cfg.Get("engine", "google");
        var u = e == "bing" ? "https://www.bing.com/search?q=" : e == "duck" ? "https://duckduckgo.com/?q=" : "https://www.google.com/search?q=";
        return u + Uri.EscapeDataString(q);
    }
    static string Norm(string u)
    {
        u = u.Trim(); if (u == "") return "https://www.google.com"; if (u.Contains("://") || u.StartsWith("about:") || u.StartsWith("data:")) return u;
        if ((u.Contains('.') && !u.Contains(' ')) || u.StartsWith("localhost")) return "https://" + u;
        return Engine(u);
    }

    // ---------- bookmarks ----------
    static string BmFile => Path.Combine(Paths.Data, "bookmarks.txt");
    static List<(string t, string u)> Bms()
    {
        var l = new List<(string, string)>();
        try { if (File.Exists(BmFile)) foreach (var x in File.ReadAllLines(BmFile)) { var i = x.IndexOf('\t'); if (i > 0) l.Add((x[..i], x[(i + 1)..])); } } catch { }
        return l;
    }
    static void SaveBms(List<(string t, string u)> l) { try { File.WriteAllLines(BmFile, l.Select(x => x.t.Replace('\t', ' ') + "\t" + x.u)); } catch { } }

    static string NewTabHtml()
    {
        var store = Cfg.Get("store_url"); var st = store == "" ? "" : $"<a class=tile href='{Norm(store)}'><div class=ic style='background:#fde8ee'><svg viewBox='0 0 24 24' width=26 height=26><path fill='#e11d48' d='M6 7h12l1 13H5zM9 7a3 3 0 016 0' stroke='#e11d48' stroke-width='1.6' fill='none'/></svg></div>MLD Store</a>";
        string Esc(string x) => System.Net.WebUtility.HtmlEncode(x);
        var bm = string.Concat(Bms().Take(8).Select(b => "<a class=tile href='" + Esc(b.u) + "'><div class=ic><b>" + Esc(b.t.Length > 0 ? b.t.Substring(0, 1).ToUpper() : "?") + "</b></div>" + Esc(b.t.Length > 12 ? b.t.Substring(0, 12) + "..." : b.t) + "</a>"));
        return @"<!doctype html><meta charset=utf-8><title>New tab</title><style>
*{box-sizing:border-box}body{margin:0;font-family:'Segoe UI',Arial,sans-serif;background:#fff;display:flex;flex-direction:column;align-items:center;justify-content:center;height:100vh;color:#3c4043}
h1{font-size:84px;margin:0 0 28px;font-weight:500;letter-spacing:-4px;font-family:'Product Sans','Segoe UI',Arial,sans-serif}
form{width:min(620px,90vw);position:relative}
input{width:100%;padding:15px 24px 15px 54px;border-radius:30px;border:1px solid #dfe1e5;font-size:17px;outline:none;box-shadow:0 1px 6px rgba(32,33,36,.14);background:#fff url(""data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' width='22' height='22'%3E%3Cpath fill='%239aa0a6' d='M15.5 14h-.8l-.3-.3A6.5 6.5 0 109.5 16a6.5 6.5 0 004.2-1.6l.3.3v.8l5 5 1.5-1.5zm-6 0a4.5 4.5 0 110-9 4.5 4.5 0 010 9z'/%3E%3C/svg%3E"") no-repeat 18px center}
input:hover,input:focus{box-shadow:0 2px 12px rgba(32,33,36,.26);border-color:transparent}
.tiles{display:flex;gap:18px;margin-top:46px;flex-wrap:wrap;justify-content:center;max-width:700px}
.tile{width:98px;text-align:center;text-decoration:none;color:#3c4043;font-size:12.5px;padding:8px 0;border-radius:12px}
.tile:hover{background:#f1f3f4}
.ic{width:52px;height:52px;border-radius:50%;background:#f1f3f4;display:flex;align-items:center;justify-content:center;font-size:20px;margin:0 auto 8px;color:#5f6368}
.tile:hover .ic{background:#e8eaed}</style>
<h1><span style='color:#4285f4'>S</span><span style='color:#ea4335'>e</span><span style='color:#fbbc05'>a</span><span style='color:#4285f4'>r</span><span style='color:#34a853'>c</span><span style='color:#ea4335'>h</span></h1>
<form id=f><input id=q placeholder='Search the web or type a URL' autofocus autocomplete=off></form>
<script>document.getElementById('f').addEventListener('submit',function(e){e.preventDefault();var v=document.getElementById('q').value.trim();if(!v)return;var u=/^[\w-]+(\.[\w-]+)+(\/.*)?$/.test(v)&&v.indexOf(' ')<0?'https://'+v:/^https?:\/\//.test(v)?v:'" + Engine("").Replace("'", "") + @"'+encodeURIComponent(v);location.href=u;});</script>
<div class=tiles>
<a class=tile href='https://www.youtube.com'><div class=ic style='background:#fde8e8'><svg viewBox='0 0 24 24' width=26 height=26><rect x=2 y=5 width=20 height=14 rx=4 fill='#ef4444'/><path d='M10 9l5 3-5 3z' fill='#fff'/></svg></div>YouTube</a>
<a class=tile href='https://mail.google.com'><div class=ic style='background:#e8f0fe'><svg viewBox='0 0 24 24' width=26 height=26><rect x=2 y=5 width=20 height=14 rx=2 fill='#fff' stroke='#4285f4' stroke-width=1.6 /><path d='M3 7l9 6 9-6' stroke='#ea4335' stroke-width=1.8 fill='none'/></svg></div>Gmail</a>
<a class=tile href='https://github.com'><div class=ic style='background:#eceff1'><svg viewBox='0 0 24 24' width=26 height=26><circle cx=12 cy=12 r=10 fill='#24292f'/><path d='M9 17c-3 1-3-1-4-1m8 3v-3c0-1 0-1.5-.5-2 2-.2 4-1 4-4.5a3.5 3.5 0 00-1-2.5 3.2 3.2 0 000-2.4s-.8-.2-2.5 1a8.5 8.5 0 00-4.5 0c-1.7-1.2-2.5-1-2.5-1a3.2 3.2 0 000 2.4 3.5 3.5 0 00-1 2.5C6.5 13 8.500 13.800 10.500 14c-.5.500-.5 1-.5 2v3' stroke='#fff' stroke-width=1.2 fill='none'/></svg></div>GitHub</a>
<a class=tile href='https://maps.google.com'><div class=ic style='background:#e6f4ea'><svg viewBox='0 0 24 24' width=26 height=26><path d='M12 2a7 7 0 00-7 7c0 5 7 13 7 13s7-8 7-13a7 7 0 00-7-7z' fill='#34a853'/><circle cx=12 cy=9 r=2.800 fill='#fff'/></svg></div>Maps</a>
<a class=tile href='https://www.wikipedia.org'><div class=ic><b style='font-family:Georgia,serif;font-size:24px;color:#202124'>W</b></div>Wikipedia</a>" + st + bm + "</div>";
    }

    static void Dl(DesktopForm sh, CoreWebView2DownloadStartingEventArgs e, string dir, bool asApp)
    {
        Directory.CreateDirectory(dir);
        var nm = Path.GetFileName(e.ResultFilePath); if (string.IsNullOrWhiteSpace(nm)) nm = "download";
        var dest = U.Unique(dir, nm); e.ResultFilePath = dest; e.Handled = true;
        var op = e.DownloadOperation; var cts = new CancellationTokenSource(); var card = sh.NewDl(Path.GetFileName(dest), cts, Installer.IconKey(dest));
        cts.Token.Register(() => { try { op.Cancel(); } catch { } });
        var sw = Stopwatch.StartNew(); long lastB = 0, lastT = 0; double sp = 0; bool end = false;
        var tm = new System.Windows.Forms.Timer { Interval = 200 };
        tm.Tick += (_, _) =>
        {
            if (end) return; long got = op.BytesReceived; long tot = 0; try { tot = Convert.ToInt64(op.TotalBytesToReceive); } catch { }
            double dt = Math.Max(0.05, (sw.ElapsedMilliseconds - lastT) / 1000.0); double inst = (got - lastB) / dt; sp = sp <= 0 ? inst : sp * 0.7 + inst * 0.3; lastB = got; lastT = sw.ElapsedMilliseconds;
            card.Report(got, tot > 0 ? tot : -1, sp);
        };
        tm.Start();
        op.StateChanged += async (_, _) =>
        {
            if (op.State == CoreWebView2DownloadState.Completed) { end = true; tm.Stop(); tm.Dispose(); await Installer.Finish(sh, card, dest, asApp); }
            else if (op.State == CoreWebView2DownloadState.Interrupted) { end = true; tm.Stop(); tm.Dispose(); card.Fail(cts.IsCancellationRequested ? "Cancelled" : "Download interrupted. Check your connection and try again."); }
        };
    }

    static async Task InstallRuntime(DesktopForm sh, Action done)
    {
        var f = Path.Combine(Paths.Data, "MicrosoftEdgeWebview2Setup.exe"); var cts = new CancellationTokenSource(); var card = sh.NewDl("Browser engine (WebView2)", cts, "browser");
        try
        {
            await Net.Download("https://go.microsoft.com/fwlink/p/?LinkId=2124703", f, card.Report, cts.Token); card.Stage("Installing… approve the Windows prompt");
            using var pr = Process.Start(new ProcessStartInfo(f, "/silent /install") { UseShellExecute = true, Verb = "runas" })!; await pr.WaitForExitAsync();
            try { File.Delete(f); } catch { }
            card.Done("Browser engine installed"); done();
        }
        catch (OperationCanceledException) { card.Fail("Cancelled"); }
        catch (Exception ex) { card.Fail(ex.Message); }
    }

    public static Control Browser(DesktopForm sh, string? url, string? dlDir = null, bool asApp = false)
    {
        dlDir ??= Path.Combine(Paths.Desktop, "Downloads");
        var root = new Panel { BackColor = Color.White }; Ui.Own(root);
        var tabs = new ChromeTabs { Dock = DockStyle.Top };
        var tool = new Panel { Dock = DockStyle.Top, Height = Ui.D(46), BackColor = Color.White };
        var bmBar = new Panel { Dock = DockStyle.Top, Height = Ui.D(30), BackColor = Color.White };
        var line = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(218, 220, 224) };
        var prog = new LineProgress { Dock = DockStyle.Top, Height = Ui.D(3), BackColor = Color.White, Fill1 = Color.FromArgb(26, 115, 232), Fill2 = Color.FromArgb(66, 165, 245) };
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        root.Controls.Add(host); root.Controls.Add(prog); root.Controls.Add(bmBar); root.Controls.Add(line); root.Controls.Add(tool); root.Controls.Add(tabs);

        var back = new IconBtn("back"); var fwd = new IconBtn("fwd"); var rel = new IconBtn("reload"); var home = new IconBtn("home"); var star = new IconBtn("star"); var menuBtn = new IconBtn("menu");
        var (omni, addr) = Ui.Input("Search Google or type a URL", false, 36); omni.Fill = Color.FromArgb(241, 243, 244); omni.Edge = Color.FromArgb(241, 243, 244); addr.BackColor = omni.Fill; addr.ForeColor = Color.FromArgb(32, 33, 36);
        var prof = new PictureBox { Image = U.Avatar(Ui.D(28)), SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Hand, BackColor = Color.White };
        tool.Controls.AddRange(new Control[] { back, fwd, rel, home, omni, star, prof, menuBtn });
        tool.Resize += (_, _) =>
        {
            int b = Ui.D(32), y = (tool.Height - b) / 2, right = tool.Width - Ui.D(8);
            back.SetBounds(Ui.D(8), y, b, b); fwd.SetBounds(Ui.D(42), y, b, b); rel.SetBounds(Ui.D(76), y, b, b); home.SetBounds(Ui.D(110), y, b, b);
            menuBtn.SetBounds(right - b, y, b, b); prof.SetBounds(right - b - Ui.D(36), y + Ui.D(2), Ui.D(28), Ui.D(28));
            omni.SetBounds(Ui.D(150), y - Ui.D(2), Math.Max(Ui.D(100), prof.Left - Ui.D(160)), b + Ui.D(4)); star.SetBounds(omni.Right - Ui.D(36), y, Ui.D(32), b);
            omni.Controls.OfType<TextBox>().First().Width = Math.Max(10, omni.Width - Ui.D(36) - Ui.D(44));
        };
        star.Parent?.Controls.SetChildIndex(star, 0); star.BackColor = omni.Fill; star.Hover = Color.FromArgb(225, 228, 232);

        TabInfo? Cur() => tabs.Tabs.Count > 0 && tabs.Sel < tabs.Tabs.Count ? tabs.Tabs[tabs.Sel] : null;
        void SetAddr(string? s) { if (!addr.Focused) addr.Text = s == null || s.StartsWith("data:") || s == "about:blank" ? "" : s; }
        void RefreshStar() { var u = Cur()?.Url ?? ""; star.Glyph = Bms().Any(b => b.u == u) ? "starfill" : "star"; star.GlyphColor = star.Glyph == "starfill" ? Color.FromArgb(251, 188, 4) : null; star.Invalidate(); }

        void DrawBms()
        {
            bmBar.Controls.Clear(); int x = Ui.D(10);
            foreach (var bk in Bms().Take(12))
            {
                var bb = new RoundButton { Text = bk.t.Length > 18 ? bk.t[..18] + "…" : bk.t, Radius = 6, BackColor = Color.White, ForeColor = Color.FromArgb(60, 64, 67), Hover = Color.FromArgb(232, 234, 237), Font = Ui.F(9) };
                bb.Size = new Size(Math.Min(Ui.D(190), bb.GetPreferredSize(Size.Empty).Width), Ui.D(26)); bb.Location = new Point(x, Ui.D(2)); x += bb.Width + Ui.D(4);
                var u = bk.u; bb.Click += (_, _) => Cur()?.View.CoreWebView2?.Navigate(u);
                var cm = new ContextMenuStrip(); cm.Items.Add("Remove bookmark", null, (_, _) => { var l = Bms(); l.RemoveAll(q => q.u == u); SaveBms(l); DrawBms(); RefreshStar(); });
                bb.ContextMenuStrip = cm; bmBar.Controls.Add(bb);
            }
            bmBar.Visible = bmBar.Controls.Count > 0; if (bmBar.Visible) bmBar.Height = Ui.D(30);
        }
        void Pick(int i)
        {
            if (i < 0 || i >= tabs.Tabs.Count) return; tabs.Sel = i;
            for (int k = 0; k < tabs.Tabs.Count; k++) tabs.Tabs[k].View.Visible = k == i;
            var t = tabs.Tabs[i]; SetAddr(t.View.CoreWebView2?.Source); prog.Indeterminate = t.Loading; tabs.Invalidate(); RefreshStar();
        }
        void ShowError(Exception ex)
        {
            var msg = new PagePanel(Color.White); msg.Padding = new Padding(Ui.D(48), Ui.D(48), Ui.D(48), Ui.D(20));
            var rt = ex is WebView2RuntimeNotFoundException;
            var h = msg.H(rt ? "The browser engine isn't on this PC yet" : "The browser couldn't start"); h.ForeColor = Color.FromArgb(32, 33, 36);
            var d = msg.P(rt ? "It's a small free Microsoft part (WebView2). Tap the button, approve the Windows prompt, and the browser opens by itself." : ex.Message + (Native.Error != null ? "\n" + Native.Error : ""), true); d.ForeColor = Color.FromArgb(95, 99, 104);
            var row = msg.Row(14);
            if (rt) msg.Btn(row, "Install browser engine", async () => await InstallRuntime(sh, () => { host.Controls.Remove(msg); msg.Dispose(); if (tabs.Tabs.Count == 0) AddTab(url ?? ""); }), true).BackColor = Color.FromArgb(26, 115, 232);
            else msg.Btn(row, "Try again", () => { host.Controls.Remove(msg); msg.Dispose(); AddTab(url ?? ""); }, true).BackColor = Color.FromArgb(26, 115, 232);
            host.Controls.Add(msg); msg.BringToFront();
        }
        async void AddTab(string u)
        {
            var ti = new TabInfo(); var wv = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.White }; ti.View = wv; host.Controls.Add(wv); tabs.Tabs.Add(ti); Pick(tabs.Tabs.Count - 1);
            try
            {
                await wv.EnsureCoreWebView2Async(await Env()); var c = wv.CoreWebView2;
                c.Settings.IsPasswordAutosaveEnabled = true; c.Settings.IsGeneralAutofillEnabled = true; c.Settings.UserAgent = ChromeUA; c.Settings.AreDevToolsEnabled = true; c.Settings.IsStatusBarEnabled = false;
                KeepLogins(c);
                c.DownloadStarting += (_, e) => Dl(sh, e, dlDir, asApp);
                c.DocumentTitleChanged += (_, _) => { ti.Title = c.DocumentTitle; tabs.Invalidate(); };
                c.SourceChanged += (_, _) => { ti.Url = c.Source ?? ""; if (Cur() == ti) { SetAddr(c.Source); RefreshStar(); } };
                c.NavigationStarting += (_, _) => { ti.Loading = true; if (Cur() == ti) { prog.Indeterminate = true; rel.Glyph = "close"; rel.Invalidate(); } tabs.Invalidate(); };
                c.NavigationCompleted += (_, _) => { ti.Loading = false; if (Cur() == ti) { prog.Indeterminate = false; prog.Value = 0; rel.Glyph = "reload"; rel.Invalidate(); } tabs.Invalidate(); };
                c.FaviconChanged += async (_, _) => { try { using var s = await c.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png); if (s != null) { using var im = Image.FromStream(s); ti.Icon = new Bitmap(im); tabs.Invalidate(); } } catch { } };
                c.NewWindowRequested += (_, e) => { e.Handled = true; AddTab(e.Uri); };
                c.ProcessFailed += (_, _) => { try { c.Reload(); } catch { } };
                var anim = new System.Windows.Forms.Timer { Interval = 60 }; anim.Tick += (_, _) => { if (ti.Loading) tabs.Invalidate(); }; anim.Start(); wv.Disposed += (_, _) => anim.Dispose();
                if (u == "") c.NavigateToString(NewTabHtml()); else c.Navigate(Norm(u));
            }
            catch (Exception ex) { tabs.Tabs.Remove(ti); host.Controls.Remove(wv); wv.Dispose(); ShowError(ex); }
        }
        tabs.Picked += Pick; tabs.NewTab += () => AddTab("");
        tabs.CloseTab += i =>
        {
            if (i < 0 || i >= tabs.Tabs.Count) return; var t = tabs.Tabs[i]; tabs.Tabs.RemoveAt(i); host.Controls.Remove(t.View); t.View.Dispose();
            if (tabs.Tabs.Count == 0) { AddTab(""); return; } Pick(Math.Min(i, tabs.Tabs.Count - 1));
        };
        back.Click += (_, _) => Cur()?.View.GoBack(); fwd.Click += (_, _) => Cur()?.View.GoForward();
        rel.Click += (_, _) => { var t = Cur(); if (t == null) return; if (t.Loading) t.View.CoreWebView2?.Stop(); else t.View.Reload(); };
        home.Click += (_, _) => Cur()?.View.CoreWebView2?.NavigateToString(NewTabHtml());
        star.Click += (_, _) =>
        {
            var t = Cur(); if (t == null || t.Url == "" || t.Url.StartsWith("data:")) return; var l = Bms();
            if (l.Any(b => b.u == t.Url)) l.RemoveAll(b => b.u == t.Url); else l.Add((string.IsNullOrWhiteSpace(t.Title) ? t.Url : t.Title, t.Url));
            SaveBms(l); DrawBms(); RefreshStar();
        };
        addr.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { var t = Cur(); var c = t?.View.CoreWebView2; if (c != null) c.Navigate(Norm(addr.Text)); else AddTab(addr.Text); e.SuppressKeyPress = true; Cur()?.View.Focus(); } };
        addr.GotFocus += (_, _) => addr.SelectAll();

        var m = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(), ForeColor = Color.FromArgb(32, 33, 36), BackColor = Color.White, Font = Ui.F(10) };
        m.Items.Add("New tab", null, (_, _) => AddTab(""));
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Downloads folder", null, (_, _) => { Directory.CreateDirectory(dlDir); U.Open(dlDir); });
        m.Items.Add("Zoom in", null, (_, _) => { var v = Cur()?.View; if (v != null) v.ZoomFactor = Math.Min(3, v.ZoomFactor + 0.1); });
        m.Items.Add("Zoom out", null, (_, _) => { var v = Cur()?.View; if (v != null) v.ZoomFactor = Math.Max(0.3, v.ZoomFactor - 0.1); });
        m.Items.Add("Print…", null, (_, _) => { try { Cur()?.View.CoreWebView2?.ShowPrintUI(CoreWebView2PrintDialogKind.Browser); } catch { } });
        var se = new ToolStripMenuItem("Search engine");
        foreach (var (n, k) in new[] { ("Google", "google"), ("Bing", "bing"), ("DuckDuckGo", "duck") }) { var kk = k; se.DropDownItems.Add(n, null, (_, _) => { Cfg.Set("engine", kk); sh.Toast("Search engine: " + n); }); }
        m.Items.Add(se); m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Clear browsing data (signs you out everywhere)", null, async (_, _) => { try { var c = Cur()?.View.CoreWebView2; if (c != null) { await c.Profile.ClearBrowsingDataAsync(); sh.Toast("Browsing data cleared"); } } catch (Exception ex) { sh.Toast(ex.Message); } });
        m.Items.Add("Developer tools", null, (_, _) => Cur()?.View.CoreWebView2?.OpenDevToolsWindow());
        menuBtn.Click += (_, _) => m.Show(menuBtn, new Point(menuBtn.Width - m.Width, menuBtn.Height));
        var pm = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(), ForeColor = Color.FromArgb(32, 33, 36), BackColor = Color.White, Font = Ui.F(10) };
        pm.Items.Add(Cfg.Get("name", Environment.UserName)).Enabled = false;
        pm.Items.Add("Sign in to any website once.").Enabled = false; pm.Items.Add("Your logins live on this USB,").Enabled = false; pm.Items.Add("so your accounts travel with you.").Enabled = false;
        prof.Click += (_, _) => pm.Show(prof, new Point(0, prof.Height));

        // keyboard shortcuts
        void Key(Keys k, bool ctrl)
        {
            if (ctrl && k == Keys.T) AddTab(""); else if (ctrl && k == Keys.W) { if (tabs.Tabs.Count > 0) tabs.CloseTabAt(tabs.Sel); }
            else if (ctrl && k == Keys.L) { addr.Focus(); addr.SelectAll(); } else if (k == Keys.F5) Cur()?.View.Reload();
        }

        bool started = false; root.HandleCreated += (_, _) => { if (!started) { started = true; DrawBms(); AddTab(url ?? ""); } };
        root.Disposed += async (_, _) => { try { await FlushAll(); } catch { } };
        return root;
    }

    public static Control Store(DesktopForm sh)
    {
        var p = new Panel { BackColor = Ui.Surface }; Ui.Own(p); bool started = false;
        void Build()
        {
            p.Controls.Clear(); var u = Cfg.Get("store_url");
            if (u != "") { var b = Browser(sh, u, Paths.Apps, true); b.Dock = DockStyle.Fill; p.Controls.Add(b); return; }
            var pg = new PagePanel(); pg.H("MLD Store"); pg.P("Type your store's website address. Apps you download there install straight into your Start menu, with live progress.", true);
            var row = pg.Row(14); var (box, tb) = pg.Field(row, "https://your-store.example.com", 440);
            pg.Btn(row, "Open store", () => { if (tb.Text.Trim() != "") { Cfg.Set("store_url", tb.Text.Trim()); Build(); } }).Margin = new Padding(0, 0, 0, Ui.D(6));
            p.Controls.Add(pg);
        }
        p.HandleCreated += (_, _) => { if (!started) { started = true; Build(); } };
        return p;
    }
}

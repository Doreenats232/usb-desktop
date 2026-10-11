using System.Diagnostics;
using System.Drawing.Drawing2D;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace UsbDesktop;

class TabInfo { public string Title = "New tab"; public Image? Icon; public bool Loading; public WebView2 View = null!; }

class IconBtn : RoundButton
{
    public IconBtn(string glyph)
    {
        Text = glyph; Pill = true; BackColor = Color.White; ForeColor = Color.FromArgb(95, 99, 104); Hover = Color.FromArgb(232, 234, 237); Font = Ui.F(12);
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
    Rectangle PlusRect => new(Ui.D(10) + Tabs.Count * TabW, Ui.D(11), Ui.D(30), Ui.D(30));
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
                if (t.Icon != null) g.DrawImage(t.Icon, ix, r.Y + (r.Height - sz) / 2, sz, sz);
                else { using var b = new SolidBrush(Color.FromArgb(154, 160, 166)); g.FillEllipse(b, ix, r.Y + (r.Height - sz) / 2, sz, sz); }
                var tr = new Rectangle(ix + sz + Ui.D(8), r.Y, r.Width - sz - Ui.D(54), r.Height);
                TextRenderer.DrawText(g, t.Loading ? "Loading…" : t.Title, Font, tr, sel ? Color.FromArgb(32, 33, 36) : dark, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                if (sel || i == hover) TextRenderer.DrawText(g, "✕", Font, XRect(i), dark, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        TextRenderer.DrawText(g, "＋", Ui.F(12), PlusRect, dark, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
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
    static async Task<CoreWebView2Environment> Env() => env ??= await CoreWebView2Environment.CreateAsync(null, Path.Combine(Paths.Data, "webview"));
    static string Norm(string u)
    {
        u = u.Trim(); if (u == "") return "https://www.google.com"; if (u.Contains("://")) return u;
        if (u.Contains('.') && !u.Contains(' ')) return "https://" + u; return "https://www.google.com/search?q=" + Uri.EscapeDataString(u);
    }
    static string NewTabHtml()
    {
        var store = Cfg.Get("store_url"); var st = store == "" ? "" : $"<a class=tile href='{Norm(store)}'><div class=ic>🛍</div>MLD Store</a>";
        return @"<!doctype html><meta charset=utf-8><title>New tab</title><style>
body{margin:0;font-family:'Segoe UI',Arial,sans-serif;background:#fff;display:flex;flex-direction:column;align-items:center;justify-content:center;height:100vh}
h1{font-size:76px;margin:0 0 30px;font-weight:600;letter-spacing:-3px}
form{width:min(600px,88vw)}input{width:100%;box-sizing:border-box;padding:16px 26px;border-radius:32px;border:1px solid #dfe1e5;font-size:17px;outline:none;box-shadow:0 1px 6px rgba(32,33,36,.16)}
input:focus{box-shadow:0 2px 12px rgba(32,33,36,.28)}
.tiles{display:flex;gap:22px;margin-top:44px;flex-wrap:wrap;justify-content:center}
.tile{width:96px;text-align:center;text-decoration:none;color:#3c4043;font-size:13px}
.ic{width:58px;height:58px;border-radius:50%;background:#f1f3f4;display:flex;align-items:center;justify-content:center;font-size:27px;margin:0 auto 9px}
.tile:hover .ic{background:#e8eaed}</style>
<h1><span style='color:#4285f4'>S</span><span style='color:#ea4335'>e</span><span style='color:#fbbc05'>a</span><span style='color:#4285f4'>r</span><span style='color:#34a853'>c</span><span style='color:#ea4335'>h</span></h1>
<form action='https://www.google.com/search'><input name=q placeholder='Search the web or type a URL' autofocus autocomplete=off></form>
<div class=tiles>
<a class=tile href='https://www.youtube.com'><div class=ic>▶️</div>YouTube</a><a class=tile href='https://mail.google.com'><div class=ic>✉️</div>Mail</a>
<a class=tile href='https://github.com'><div class=ic>🐙</div>GitHub</a><a class=tile href='https://maps.google.com'><div class=ic>📍</div>Maps</a>
<a class=tile href='https://www.wikipedia.org'><div class=ic>📚</div>Wikipedia</a>" + st + "</div>";
    }

    static void Dl(DesktopForm sh, CoreWebView2DownloadStartingEventArgs e, string dir)
    {
        Directory.CreateDirectory(dir); var dest = U.Unique(dir, Path.GetFileName(e.ResultFilePath)); e.ResultFilePath = dest; e.Handled = true;
        var card = sh.NewDl("⬇  " + Path.GetFileName(dest), null); var op = e.DownloadOperation; long last = 0, lt = 0; var sw = Stopwatch.StartNew();
        op.BytesReceivedChanged += (_, _) =>
        {
            long now = sw.ElapsedMilliseconds; if (now - lt < 300) return;
            long tot = 0; try { tot = Convert.ToInt64(op.TotalBytesToReceive); } catch { }
            card.Report(op.BytesReceived, tot > 0 ? tot : -1, (op.BytesReceived - last) * 1000.0 / Math.Max(1, now - lt)); last = op.BytesReceived; lt = now;
        };
        op.StateChanged += (_, _) =>
        {
            if (op.State == CoreWebView2DownloadState.Completed) { card.Done("Saved to " + Path.GetFileName(dir)); sh.AfterDownload(dest); }
            else if (op.State == CoreWebView2DownloadState.Interrupted) card.Fail("Download interrupted");
        };
    }

    static async Task InstallRuntime(DesktopForm sh)
    {
        var f = Path.Combine(Paths.Data, "MicrosoftEdgeWebview2Setup.exe"); var cts = new CancellationTokenSource(); var card = sh.NewDl("Browser engine (WebView2)", cts);
        try
        {
            await Net.Download("https://go.microsoft.com/fwlink/p/?LinkId=2124703", f, card.Report, cts.Token); card.Done("Installing… approve the Windows prompt");
            using var pr = Process.Start(new ProcessStartInfo(f, "/silent /install") { UseShellExecute = true, Verb = "runas" })!; await pr.WaitForExitAsync();
            sh.Toast("Browser engine installed. Open Browser again."); try { File.Delete(f); } catch { }
        }
        catch (Exception ex) { card.Fail(ex.Message); }
    }

    public static Control Browser(DesktopForm sh, string? url, string? dlDir = null)
    {
        dlDir ??= Path.Combine(Paths.Desktop, "Downloads");
        var root = new Panel { BackColor = Color.White }; Ui.Own(root);
        var tabs = new ChromeTabs { Dock = DockStyle.Top };
        var tool = new Panel { Dock = DockStyle.Top, Height = Ui.D(46), BackColor = Color.White };
        var prog = new LineProgress { Dock = DockStyle.Top, Height = Ui.D(3), BackColor = Color.White };
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        root.Controls.Add(host); root.Controls.Add(prog); root.Controls.Add(tool); root.Controls.Add(tabs);

        var back = new IconBtn("←"); var fwd = new IconBtn("→"); var rel = new IconBtn("⟳"); var menuBtn = new IconBtn("⋮");
        var (omni, addr) = Ui.Input("Search or type a URL", false, 36); omni.Fill = Color.FromArgb(241, 243, 244); addr.BackColor = omni.Fill; addr.ForeColor = Color.FromArgb(32, 33, 36);
        var prof = new PictureBox { Image = U.Avatar(Ui.D(28)), SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Hand };
        tool.Controls.AddRange(new Control[] { back, fwd, rel, omni, prof, menuBtn });
        tool.Resize += (_, _) =>
        {
            int b = Ui.D(32), y = (tool.Height - b) / 2, right = tool.Width - Ui.D(8);
            back.SetBounds(Ui.D(8), y, b, b); fwd.SetBounds(Ui.D(44), y, b, b); rel.SetBounds(Ui.D(80), y, b, b);
            menuBtn.SetBounds(right - b, y, b, b); prof.SetBounds(right - b - Ui.D(40), y + Ui.D(2), Ui.D(28), Ui.D(28));
            omni.SetBounds(Ui.D(120), y - Ui.D(2), prof.Left - Ui.D(130), b + Ui.D(4));
        };

        TabInfo? Cur() => tabs.Tabs.Count > 0 && tabs.Sel < tabs.Tabs.Count ? tabs.Tabs[tabs.Sel] : null;
        void SetAddr(string? s) { addr.Text = s == null || s.StartsWith("data:") ? "" : s; }
        void Pick(int i)
        {
            if (i < 0 || i >= tabs.Tabs.Count) return; tabs.Sel = i;
            for (int k = 0; k < tabs.Tabs.Count; k++) tabs.Tabs[k].View.Visible = k == i;
            var t = tabs.Tabs[i]; SetAddr(t.View.CoreWebView2?.Source); prog.Indeterminate = t.Loading; tabs.Invalidate();
        }
        void ShowError(Exception ex)
        {
            var msg = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var rt = ex is WebView2RuntimeNotFoundException;
            var l = new Label { Text = rt ? "The browser engine isn't installed on this PC" : "The browser couldn't start", Font = Ui.F(18, true), ForeColor = Color.FromArgb(32, 33, 36), AutoSize = true, Location = new Point(Ui.D(40), Ui.D(50)) };
            var d = new Label { Text = rt ? "It's a small free Microsoft component (WebView2). Install it once and the browser will work." : ex.Message, Font = Ui.F(11), ForeColor = Color.FromArgb(95, 99, 104), AutoSize = true, MaximumSize = new Size(Ui.D(600), 0), Location = new Point(Ui.D(42), Ui.D(100)) };
            msg.Controls.Add(l); msg.Controls.Add(d);
            if (rt) { var b = new RoundButton { Text = "Install browser engine", BackColor = Color.FromArgb(26, 115, 232), Pill = true, Bounds = new Rectangle(Ui.D(42), Ui.D(170), Ui.D(230), Ui.D(44)) }; b.Click += async (_, _) => await InstallRuntime(sh); msg.Controls.Add(b); }
            host.Controls.Add(msg); msg.BringToFront();
        }
        async void AddTab(string u)
        {
            var ti = new TabInfo(); var wv = new WebView2 { Dock = DockStyle.Fill }; ti.View = wv; host.Controls.Add(wv); tabs.Tabs.Add(ti); Pick(tabs.Tabs.Count - 1);
            try
            {
                await wv.EnsureCoreWebView2Async(await Env()); var c = wv.CoreWebView2;
                c.Settings.IsPasswordAutosaveEnabled = true; c.Settings.IsGeneralAutofillEnabled = true;
                c.DownloadStarting += (_, e) => Dl(sh, e, dlDir);
                c.DocumentTitleChanged += (_, _) => { ti.Title = c.DocumentTitle; tabs.Invalidate(); };
                c.SourceChanged += (_, _) => { if (Cur() == ti) SetAddr(c.Source); };
                c.NavigationStarting += (_, _) => { ti.Loading = true; if (Cur() == ti) prog.Indeterminate = true; tabs.Invalidate(); };
                c.NavigationCompleted += (_, _) => { ti.Loading = false; if (Cur() == ti) prog.Indeterminate = false; tabs.Invalidate(); };
                c.FaviconChanged += async (_, _) => { try { using var s = await c.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png); if (s != null) { using var im = Image.FromStream(s); ti.Icon = new Bitmap(im); tabs.Invalidate(); } } catch { } };
                c.NewWindowRequested += (_, e) => { e.Handled = true; AddTab(e.Uri); };
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
        back.Click += (_, _) => Cur()?.View.GoBack(); fwd.Click += (_, _) => Cur()?.View.GoForward(); rel.Click += (_, _) => Cur()?.View.Reload();
        addr.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Cur()?.View.CoreWebView2?.Navigate(Norm(addr.Text)); e.SuppressKeyPress = true; } };
        addr.GotFocus += (_, _) => addr.SelectAll();

        var m = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(), ForeColor = Color.FromArgb(32, 33, 36), Font = Ui.F(10) };
        m.Items.Add("New tab", null, (_, _) => AddTab(""));
        m.Items.Add("Downloads folder", null, (_, _) => { Directory.CreateDirectory(dlDir); U.Open(dlDir); });
        m.Items.Add("Zoom in", null, (_, _) => { var v = Cur()?.View; if (v != null) v.ZoomFactor = Math.Min(3, v.ZoomFactor + 0.1); });
        m.Items.Add("Zoom out", null, (_, _) => { var v = Cur()?.View; if (v != null) v.ZoomFactor = Math.Max(0.3, v.ZoomFactor - 0.1); });
        m.Items.Add("Clear browsing data (sign out of everything)", null, async (_, _) => { try { var c = Cur()?.View.CoreWebView2; if (c != null) { await c.Profile.ClearBrowsingDataAsync(); sh.Toast("Browsing data cleared"); } } catch (Exception ex) { sh.Toast(ex.Message); } });
        m.Items.Add("Developer tools", null, (_, _) => Cur()?.View.CoreWebView2?.OpenDevToolsWindow());
        menuBtn.Click += (_, _) => m.Show(menuBtn, new Point(0, menuBtn.Height));
        var pm = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(), ForeColor = Color.FromArgb(32, 33, 36), Font = Ui.F(10) };
        pm.Items.Add(Cfg.Get("name", Environment.UserName)).Enabled = false;
        pm.Items.Add("Sign in to any website here.").Enabled = false; pm.Items.Add("Your logins are saved on this USB,").Enabled = false; pm.Items.Add("so your accounts travel with you.").Enabled = false;
        prof.Click += (_, _) => pm.Show(prof, new Point(0, prof.Height));

        bool started = false; root.HandleCreated += (_, _) => { if (!started) { started = true; AddTab(url ?? ""); } };
        return root;
    }

    public static Control Store(DesktopForm sh)
    {
        var p = new Panel { BackColor = Ui.Surface }; Ui.Own(p); bool started = false;
        void Build()
        {
            p.Controls.Clear(); var u = Cfg.Get("store_url");
            if (u != "") { var b = Browser(sh, u, Paths.Apps); b.Dock = DockStyle.Fill; p.Controls.Add(b); return; }
            Label L(string t, int x, int y, float sz, bool bold = false) { var l = new Label { Text = t, ForeColor = Ui.Text, BackColor = Color.Transparent, Font = Ui.F(sz, bold), AutoSize = true, Location = new Point(Ui.D(x), Ui.D(y)) }; p.Controls.Add(l); return l; }
            L("🛍  MLD Store", 32, 28, 22, true); L("Type your store's website address and your apps will download straight into your Start menu.", 34, 84, 11);
            var (box, tb) = Ui.Input("https://your-store.example.com"); box.SetBounds(Ui.D(34), Ui.D(130), Ui.D(460), Ui.D(46)); p.Controls.Add(box);
            var go = new RoundButton { Text = "Open store", BackColor = Ui.Accent, Pill = true, Bounds = new Rectangle(Ui.D(506), Ui.D(130), Ui.D(140), Ui.D(46)) };
            go.Click += (_, _) => { if (tb.Text.Trim() != "") { Cfg.Set("store_url", tb.Text.Trim()); Build(); } }; p.Controls.Add(go);
        }
        p.HandleCreated += (_, _) => { if (!started) { started = true; Build(); } };
        return p;
    }
}

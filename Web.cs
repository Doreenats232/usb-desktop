using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace UsbDesktop;

static class Web
{
    static CoreWebView2Environment? env;
    static async Task<CoreWebView2Environment> Env() => env ??= await CoreWebView2Environment.CreateAsync(null, Path.Combine(Paths.Data, "webview"));
    static string Norm(string u)
    {
        u = u.Trim(); if (u == "") return "https://www.google.com"; if (u.Contains("://")) return u;
        if (u.Contains('.') && !u.Contains(' ')) return "https://" + u; return "https://www.google.com/search?q=" + Uri.EscapeDataString(u);
    }
    static void Dl(DesktopForm sh, CoreWebView2DownloadStartingEventArgs e, string dir)
    {
        Directory.CreateDirectory(dir); var dest = U.Unique(dir, Path.GetFileName(e.ResultFilePath));
        e.ResultFilePath = dest; e.Handled = true; sh.Toast("⬇ Downloading " + Path.GetFileName(dest));
        var op = e.DownloadOperation; op.StateChanged += (_, _) => { if (op.State == CoreWebView2DownloadState.Completed) sh.AfterDownload(dest); };
    }

    public static Control Browser(DesktopForm sh, string? url)
    {
        var p = new Panel(); var nav = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(240, 240, 245), Padding = new Padding(4) };
        var addr = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11) };
        Button Nb(string t, DockStyle d) => new() { Text = t, Dock = d, Width = 36, FlatStyle = FlatStyle.Flat };
        var back = Nb("◀", DockStyle.Left); var fwd = Nb("▶", DockStyle.Left); var rel = Nb("⟳", DockStyle.Left); var nt = Nb("＋", DockStyle.Right); var cx = Nb("✕", DockStyle.Right);
        nav.Controls.Add(addr); nav.Controls.AddRange(new Control[] { rel, fwd, back, nt, cx });
        var tabs = new TabControl { Dock = DockStyle.Fill };
        WebView2? Cur() => tabs.SelectedTab?.Controls.OfType<WebView2>().FirstOrDefault();
        async void AddTab(string u)
        {
            try
            {
                var tp = new TabPage("New tab"); var wv = new WebView2 { Dock = DockStyle.Fill }; tp.Controls.Add(wv); tabs.TabPages.Add(tp); tabs.SelectedTab = tp;
                await wv.EnsureCoreWebView2Async(await Env()); var c = wv.CoreWebView2;
                c.DownloadStarting += (_, e) => Dl(sh, e, Path.Combine(Paths.Desktop, "Downloads"));
                c.DocumentTitleChanged += (_, _) => tp.Text = c.DocumentTitle.Length > 18 ? c.DocumentTitle[..18] + "…" : c.DocumentTitle;
                c.SourceChanged += (_, _) => { if (Cur() == wv) addr.Text = c.Source; };
                c.NewWindowRequested += (_, e) => { e.Handled = true; AddTab(e.Uri); };
                c.Navigate(Norm(u));
            }
            catch (Exception ex) { MessageBox.Show("The browser couldn't start: " + ex.Message + "\nIt needs the Microsoft WebView2 runtime."); }
        }
        back.Click += (_, _) => Cur()?.GoBack(); fwd.Click += (_, _) => Cur()?.GoForward(); rel.Click += (_, _) => Cur()?.Reload();
        nt.Click += (_, _) => AddTab(""); cx.Click += (_, _) => { if (tabs.TabCount > 1 && tabs.SelectedTab != null) { var t = tabs.SelectedTab; tabs.TabPages.Remove(t); t.Dispose(); } };
        addr.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Cur()?.CoreWebView2?.Navigate(Norm(addr.Text)); e.SuppressKeyPress = true; } };
        tabs.SelectedIndexChanged += (_, _) => addr.Text = Cur()?.Source?.ToString() ?? "";
        p.Controls.Add(tabs); p.Controls.Add(nav);
        bool started = false; p.HandleCreated += (_, _) => { if (!started) { started = true; AddTab(url ?? ""); } };
        return p;
    }

    public static Control Store(DesktopForm sh)
    {
        var p = new Panel { BackColor = Color.White }; bool started = false;
        void Build()
        {
            p.Controls.Clear(); var u = Cfg.Get("store_url");
            if (u == "")
            {
                p.Controls.Add(new Label { Text = "MLD Store", Font = new Font("Segoe UI", 18, FontStyle.Bold), Location = new Point(24, 20), AutoSize = true });
                p.Controls.Add(new Label { Text = "Type your store's website address:", Font = new Font("Segoe UI", 11), Location = new Point(24, 70), AutoSize = true });
                var tb = new TextBox { Bounds = new Rectangle(24, 100, 420, 30), Font = new Font("Segoe UI", 11) };
                var go = new Button { Text = "Open store", Bounds = new Rectangle(456, 98, 120, 34), FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.White };
                go.Click += (_, _) => { if (tb.Text.Trim() != "") { Cfg.Set("store_url", tb.Text.Trim()); Build(); } };
                p.Controls.Add(tb); p.Controls.Add(go); return;
            }
            var bar = new Label { Dock = DockStyle.Top, Height = 26, Text = "  Downloads are added to your Start menu (zip files unpack automatically)", BackColor = Color.FromArgb(240, 240, 245), TextAlign = ContentAlignment.MiddleLeft };
            var wv = new WebView2 { Dock = DockStyle.Fill }; p.Controls.Add(wv); p.Controls.Add(bar); Init(wv, u);
        }
        async void Init(WebView2 wv, string u)
        {
            try { await wv.EnsureCoreWebView2Async(await Env()); wv.CoreWebView2.DownloadStarting += (_, e) => Dl(sh, e, Paths.Apps); wv.CoreWebView2.Navigate(Norm(u)); }
            catch (Exception ex) { MessageBox.Show("The store couldn't start: " + ex.Message); }
        }
        p.HandleCreated += (_, _) => { if (!started) { started = true; Build(); } };
        return p;
    }
}

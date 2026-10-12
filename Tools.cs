using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
namespace UsbDesktop;

class IdleFilter : IMessageFilter
{
    readonly Action a; public IdleFilter(Action a) { this.a = a; }
    public bool PreFilterMessage(ref Message m) { if (m.Msg == 0x200 || m.Msg == 0x100 || m.Msg == 0x201) a(); return false; }
}

class ClockWidget : Control
{
    public ClockWidget() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); BackColor = Color.Transparent; Size = Ui.D(280, 110); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var t = DateTime.Now.ToString(Cfg.Get("h24", "1") == "1" ? "HH:mm" : "h:mm"); var d = DateTime.Now.ToString("dddd, MMMM d");
        using var f1 = new Font("Segoe UI Light", Ui.D(56), GraphicsUnit.Pixel); using var f2 = new Font("Segoe UI", Ui.D(18), GraphicsUnit.Pixel);
        TextRenderer.DrawText(e.Graphics, t, f1, new Point(Ui.D(5), Ui.D(5)), Color.Black); TextRenderer.DrawText(e.Graphics, t, f1, new Point(Ui.D(3), Ui.D(3)), Color.White);
        TextRenderer.DrawText(e.Graphics, d, f2, new Point(Ui.D(9), Ui.D(78)), Color.Black); TextRenderer.DrawText(e.Graphics, d, f2, new Point(Ui.D(8), Ui.D(77)), Color.White);
    }
}

class Splash : Panel
{
    readonly System.Windows.Forms.Timer t = new() { Interval = 90 }; int n; readonly Bitmap bg, av;
    public Splash(Size s)
    {
        Bounds = new Rectangle(Point.Empty, s); DoubleBuffered = true; bg = Theme.Lock(s); av = U.Avatar(Ui.D(110));
        t.Tick += (_, _) => { n++; Invalidate(); if (n > 24) { t.Stop(); Parent?.Controls.Remove(this); Dispose(); } }; t.Start();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.DrawImage(bg, 0, 0);
        using (var b = new SolidBrush(Color.FromArgb(110, 0, 0, 0))) g.FillRectangle(b, ClientRectangle);
        g.DrawImage(av, (Width - Ui.D(110)) / 2, Height / 2 - Ui.D(120));
        using var f = new Font("Segoe UI Light", 26);
        TextRenderer.DrawText(g, "Welcome, " + Cfg.Get("name", Environment.UserName), f, new Rectangle(0, Height / 2 + Ui.D(10), Width, Ui.D(60)), Color.White, TextFormatFlags.HorizontalCenter);
        g.SmoothingMode = SmoothingMode.AntiAlias; int bw = Ui.D(220), bx = (Width - bw) / 2, by = Height / 2 + Ui.D(96);
        using (var tp = U.Round(new Rectangle(bx, by, bw, Ui.D(6)), Ui.D(3))) using (var tb = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillPath(tb, tp);
        int fw = (int)(bw * Math.Min(1.0, n / 22.0)); if (fw > Ui.D(6)) { using var fp = U.Round(new Rectangle(bx, by, fw, Ui.D(6)), Ui.D(3)); using var fb = new LinearGradientBrush(new Rectangle(bx, by, bw, Ui.D(6)), Ui.Accent, Ui.Accent2, 0f); g.FillPath(fb, fp); }
    }
}

class GlyphLabel : Control
{
    public string Glyph = "";
    public GlyphLabel() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); Font = Ui.F(10.5f); ForeColor = Ui.Text; }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Ui.PBg(this, Ui.Surface)); float s = Height * 0.5f;
        if (Glyph != "") Glyphs.Draw(g, Glyph, new RectangleF(0, (Height - s) / 2f, s, s), Ui.Sub, Math.Max(1.5f, s * 0.11f));
        int x = Glyph != "" ? (int)s + Ui.D(10) : 0;
        TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, Math.Max(4, Width - x), Height), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }
}

class TrayStrip : Control
{
    int batt = -1; public int Battery { get => batt; set { if (batt != value) { batt = value; Invalidate(); } } }
    public TrayStrip() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); Cursor = Cursors.Hand; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Ui.PBg(this, Ui.Surface)); int s = Ui.D(18), y = (Height - s) / 2, x = Width - Ui.D(12) - s;
        var c = Ui.Text; float w = 1.6f * Ui.S;
        if (batt >= 0)
        {
            var t = batt + "%"; var sz = TextRenderer.MeasureText(t, Ui.F(9), new Size(999, 99), TextFormatFlags.NoPadding); x -= sz.Width + Ui.D(4);
            TextRenderer.DrawText(g, t, Ui.F(9), new Point(x + s + Ui.D(6), (Height - sz.Height) / 2), c, TextFormatFlags.NoPadding);
            Glyphs.Draw(g, "battery", new RectangleF(x, y, s, s), c, w); x -= s + Ui.D(12);
        }
        Glyphs.Draw(g, "speaker", new RectangleF(x, y, s, s), c, w); x -= s + Ui.D(14);
        Glyphs.Draw(g, "wifi", new RectangleF(x, y, s, s), c, w);
    }
}

class QuickPanel : RoundPanel
{
    readonly DesktopForm sh; readonly ListBox notes = new(); readonly GlyphLabel net = new(), bat = new(); readonly TrackBar vol = new();
    [DllImport("winmm.dll")] static extern int waveOutSetVolume(IntPtr h, uint v);
    [DllImport("winmm.dll")] static extern int waveOutGetVolume(IntPtr h, out uint v);
    public QuickPanel(DesktopForm sh)
    {
        this.sh = sh; Ui.Own(this); Floating = true; Fill = Ui.Surface; Radius = Ui.D(16); Visible = false;
        int W = Ui.D(372), pad = Ui.D(18), inner = W - pad * 2;
        var cal = new MonthCalendar { BackColor = Ui.Surface2, ForeColor = Ui.Text, TitleBackColor = Theme.Accent, TitleForeColor = Color.White, TrailingForeColor = Color.FromArgb(120, 124, 160), Font = Ui.F(9.5f) };
        cal.Location = new Point((W - cal.Width) / 2, pad); Controls.Add(cal); int y = cal.Bottom + Ui.D(18);
        GlyphLabel Head(string glyph, string t) { var l = new GlyphLabel { Glyph = glyph, Text = t, Font = Ui.F(10.5f, true), Bounds = new Rectangle(pad, y, inner, Ui.D(28)) }; Controls.Add(l); y += Ui.D(30); return l; }
        Head("speaker", "Volume");
        vol.Bounds = new Rectangle(pad - Ui.D(4), y, inner + Ui.D(8), Ui.D(40)); vol.Maximum = 100; vol.TickFrequency = 25; vol.BackColor = Ui.Surface;
        vol.Scroll += (_, _) => { uint v = (uint)(vol.Value * 65535 / 100); waveOutSetVolume(IntPtr.Zero, (v & 0xFFFF) | (v << 16)); }; Controls.Add(vol); y += Ui.D(46);
        net.Glyph = "wifi"; net.Bounds = new Rectangle(pad, y, inner - Ui.D(132), Ui.D(34)); Controls.Add(net);
        var wb = new RoundButton { Text = "Wi-Fi settings", Bounds = new Rectangle(pad + inner - Ui.D(124), y, Ui.D(124), Ui.D(34)), Pill = true, BackColor = Color.FromArgb(52, 55, 86), Font = Ui.F(9.5f) };
        wb.Click += (_, _) => U.Open("ms-settings:network-wifi"); Controls.Add(wb); y += Ui.D(42);
        bat.Glyph = "battery"; bat.Bounds = new Rectangle(pad, y, inner, Ui.D(30)); Controls.Add(bat); y += Ui.D(40);
        var nh = new GlyphLabel { Glyph = "bell", Text = "Notifications", Font = Ui.F(10.5f, true), Bounds = new Rectangle(pad, y, inner - Ui.D(84), Ui.D(30)) }; Controls.Add(nh);
        var clr = new RoundButton { Text = "Clear", Bounds = new Rectangle(pad + inner - Ui.D(76), y, Ui.D(76), Ui.D(30)), Pill = true, BackColor = Color.FromArgb(52, 55, 86), Font = Ui.F(9.5f) };
        clr.Click += (_, _) => { sh.Notes.Clear(); Refresh2(); }; Controls.Add(clr); y += Ui.D(38);
        notes.Bounds = new Rectangle(pad, y, inner, Ui.D(150)); notes.BackColor = Ui.Surface2; notes.ForeColor = Ui.Text; notes.BorderStyle = BorderStyle.None; notes.Font = Ui.F(9.5f); notes.IntegralHeight = false; Controls.Add(notes); y += Ui.D(150) + pad;
        Size = new Size(W, y);
    }
    public void Refresh2()
    {
        notes.Items.Clear(); foreach (var n in Enumerable.Reverse(sh.Notes)) notes.Items.Add(n);
        var ps = SystemInformation.PowerStatus;
        bat.Text = ps.BatteryChargeStatus == BatteryChargeStatus.NoSystemBattery ? "Plugged in" : "Battery " + (int)(Math.Min(1f, ps.BatteryLifePercent) * 100) + "%";
        if (waveOutGetVolume(IntPtr.Zero, out var v) == 0) vol.Value = Math.Clamp((int)((v & 0xFFFF) * 100 / 65535), 0, 100);
        net.Text = "Checking…";
        Task.Run(() =>
        {
            try
            {
                using var ps2 = Process.Start(new ProcessStartInfo("netsh", "wlan show interfaces") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
                var m = Regex.Match(ps2.StandardOutput.ReadToEnd(), @"^\s*SSID\s*:\s*(.+)$", RegexOptions.Multiline);
                return m.Success ? m.Groups[1].Value.Trim() : "Not connected to Wi-Fi";
            }
            catch { return "Network unavailable"; }
        }).ContinueWith(t => { if (!IsDisposed) BeginInvoke(new Action(() => net.Text = t.Result)); });
    }
}

static class Tools
{
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    public static Panel Sticky()
    {
        var p = new Panel { Size = new Size(220, 190), BackColor = Color.FromArgb(255, 239, 140), Padding = new Padding(8, 0, 8, 8) };
        var h = new Label { Text = "Sticky note", Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI Emoji", 10, FontStyle.Bold), ForeColor = Color.FromArgb(90, 70, 0), TextAlign = ContentAlignment.MiddleLeft };
        var tb = new TextBox { Multiline = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = p.BackColor, Font = new Font("Segoe UI", 11), Text = Cfg.Get("sticky").Replace("¶", "\r\n") };
        tb.TextChanged += (_, _) => Cfg.Set("sticky", tb.Text.Replace("\r\n", "¶").Replace("\n", "¶"));
        p.Controls.Add(tb); p.Controls.Add(h); return p;
    }

    public static Control Photo(string path)
    {
        var p = new Panel { BackColor = Color.FromArgb(20, 20, 28) };
        var pb = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Color.FromArgb(32, 32, 44) };
        var cap = new Label { Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter };
        RoundButton Nb(string t, DockStyle d) => new RoundButton { Text = t, Dock = d, Width = 60, FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
        var prev = Nb("", DockStyle.Left); prev.Glyph = "back"; var next = Nb("", DockStyle.Right); next.Glyph = "fwd";
        var exts = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
        var files = Directory.GetFiles(Path.GetDirectoryName(path)!).Where(f => exts.Contains(Path.GetExtension(f).ToLower())).OrderBy(f => f).ToList();
        if (!files.Contains(path)) files.Add(path); int i = files.IndexOf(path);
        void Show() { pb.Image?.Dispose(); pb.Image = U.Load(files[i]); cap.Text = Path.GetFileName(files[i]) + $"   ({i + 1}/{files.Count})"; }
        prev.Click += (_, _) => { i = (i - 1 + files.Count) % files.Count; Show(); }; next.Click += (_, _) => { i = (i + 1) % files.Count; Show(); };
        bar.Controls.Add(cap); bar.Controls.Add(prev); bar.Controls.Add(next); p.Controls.Add(pb); p.Controls.Add(bar); Show(); return p;
    }

    public static Control TaskMgr(DesktopForm sh)
    {
        var p = new Panel(); var top = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Color.FromArgb(32, 32, 48) };
        Label Lb(int x) => new() { Location = new Point(x, 10), AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
        var cpuL = Lb(16); var ramL = Lb(300); var cpuB = new ProgressBar { Bounds = new Rectangle(16, 42, 260, 12) }; var ramB = new ProgressBar { Bounds = new Rectangle(300, 42, 260, 12) };
        top.Controls.AddRange(new Control[] { cpuL, ramL, cpuB, ramB });
        var lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, Font = new Font("Segoe UI", 10) };
        lv.Columns.Add("Name", 260); lv.Columns.Add("PID", 80); lv.Columns.Add("Memory (MB)", 110);
        var end = new RoundButton { Text = "End task", Dock = DockStyle.Bottom, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(150, 60, 60), ForeColor = Color.White };
        end.Click += (_, _) =>
        {
            if (lv.SelectedItems.Count == 0) return; var pid = int.Parse(lv.SelectedItems[0].SubItems[1].Text);
            if (MessageBox.Show("End " + lv.SelectedItems[0].Text + "?", "Task Manager", MessageBoxButtons.YesNo) == DialogResult.Yes) { try { Process.GetProcessById(pid).Kill(); } catch (Exception ex) { sh.Toast(ex.Message); } }
        };
        GetSystemTimes(out long pi, out long pk, out long pu);
        void Tick()
        {
            GetSystemTimes(out long i, out long k, out long u); long di = i - pi, dt = (k - pk) + (u - pu); pi = i; pk = k; pu = u;
            int cpu = dt > 0 ? Math.Clamp((int)(100 - di * 100 / dt), 0, 100) : 0; cpuL.Text = "CPU " + cpu + "%"; cpuB.Value = cpu;
            var gi = GC.GetGCMemoryInfo(); int ram = gi.TotalAvailableMemoryBytes > 0 ? Math.Clamp((int)(gi.MemoryLoadBytes * 100 / gi.TotalAvailableMemoryBytes), 0, 100) : 0; ramL.Text = "Memory " + ram + "%"; ramB.Value = ram;
            int sel = lv.SelectedItems.Count > 0 ? int.Parse(lv.SelectedItems[0].SubItems[1].Text) : -1;
            lv.BeginUpdate(); lv.Items.Clear();
            foreach (var pr in Process.GetProcesses().OrderByDescending(x => { try { return x.WorkingSet64; } catch { return 0L; } }).Take(80))
                try { var it = new ListViewItem(new[] { pr.ProcessName, pr.Id.ToString(), (pr.WorkingSet64 / 1048576).ToString() }); it.Selected = pr.Id == sel; lv.Items.Add(it); } catch { }
            lv.EndUpdate();
        }
        var t = new System.Windows.Forms.Timer { Interval = 2000 }; t.Tick += (_, _) => Tick(); t.Start(); p.Disposed += (_, _) => t.Dispose(); Tick();
        p.Controls.Add(lv); p.Controls.Add(end); p.Controls.Add(top); return p;
    }
}

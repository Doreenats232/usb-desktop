using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace UsbDesktop;

static class Ui
{
    public static readonly float S = Dpi();
    static float Dpi() { try { using var g = Graphics.FromHwnd(IntPtr.Zero); return Math.Max(1f, g.DpiX / 96f); } catch { return 1f; } }
    public static int D(int v) => (int)Math.Round(v * S);
    public static Size D(int w, int h) => new(D(w), D(h));
    public static Font F(float pt, bool bold = false) => new("Segoe UI", pt, bold ? FontStyle.Bold : FontStyle.Regular);
    public static readonly Color Bg = Color.FromArgb(14, 15, 28), Surface = Color.FromArgb(23, 24, 41), Surface2 = Color.FromArgb(34, 36, 58),
        Line = Color.FromArgb(52, 55, 86), Text = Color.FromArgb(241, 242, 250), Sub = Color.FromArgb(165, 168, 200);
    public static Color PBg(Control c, Color fb) { var p = c.Parent; if (p is RoundPanel rp) return rp.Fill; var bc = p?.BackColor ?? fb; return bc.A < 255 ? fb : bc; }
    public static void Dark(Control c) => DarkTheme(c);
    public static Color Accent => Theme.Accent;
    public static Color Accent2 => Theme.P[Theme.Idx].C;
    public static Color Mix(Color a, Color b, float t) => Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    public static string Bytes(long b) => b >= 1073741824 ? (b / 1073741824.0).ToString("0.0") + " GB" : b >= 1048576 ? (b / 1048576.0).ToString("0.0") + " MB" : (b / 1024) + " KB";
    public static string Eta(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m left" : t.TotalMinutes >= 1 ? $"{t.Minutes}m {t.Seconds}s left" : $"{Math.Max(1, t.Seconds)}s left";

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string? list);
    static void DarkTheme(Control c)
    {
        void Do() { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } }
        c.HandleCreated += (_, _) => Do(); if (c.IsHandleCreated) Do();
    }

    static readonly ConditionalWeakTable<Control, object> fitted = new(), owned = new();
    public static void Own(Control c) { owned.AddOrUpdate(c, c); fitted.AddOrUpdate(c, c); }
    public static void Fit(Control c)
    {
        if (S < 1.02f || owned.TryGetValue(c, out _)) return;
        foreach (Control k in c.Controls) FitOne(k);
    }
    static void FitOne(Control k)
    {
        if (fitted.TryGetValue(k, out _)) return; fitted.AddOrUpdate(k, k);
        if (k.Dock == DockStyle.None) k.Bounds = new Rectangle(D(k.Left), D(k.Top), D(k.Width), D(k.Height));
        else if (k.Dock == DockStyle.Top || k.Dock == DockStyle.Bottom) k.Height = D(k.Height);
        else if (k.Dock == DockStyle.Left || k.Dock == DockStyle.Right) k.Width = D(k.Width);
        if (k is ListView lv) foreach (ColumnHeader h in lv.Columns) h.Width = D(h.Width);
        if (k.Padding != Padding.Empty) k.Padding = new Padding(D(k.Padding.Left), D(k.Padding.Top), D(k.Padding.Right), D(k.Padding.Bottom));
        foreach (Control g in k.Controls) FitOne(g);
    }

    public static void Skin(Control c)
    {
        if (owned.TryGetValue(c, out _)) return;
        One(c); foreach (Control k in c.Controls) Skin(k);
    }
    static void One(Control k)
    {
        if (k is RoundButton || k is RoundPanel) return;
        if (k is Label l) { if (l.ForeColor.GetBrightness() < 0.4f) l.ForeColor = Text; l.BackColor = Color.Transparent; }
        else if (k is TextBox t) { t.BackColor = Surface2; t.ForeColor = Text; }
        else if (k is ListView lv) { lv.BackColor = Surface; lv.ForeColor = Text; lv.BorderStyle = BorderStyle.None; DarkTheme(lv); }
        else if (k is NumericUpDown || k is ComboBox || k is ListBox) { k.BackColor = Surface2; k.ForeColor = Text; }
        else if (k is CheckBox || k is RadioButton) { k.ForeColor = Text; k.BackColor = Color.Transparent; }
        else if (k is MenuStrip ms) { ms.BackColor = Surface2; ms.ForeColor = Text; }
        else if (k is Panel p) { if (p.AutoScroll) DarkTheme(p); if (p.BackColor.GetBrightness() > 0.85f || p.BackColor == SystemColors.Control) p.BackColor = (p.Dock == DockStyle.Top || p.Dock == DockStyle.Bottom) && p.Height < 90 ? Surface2 : Surface; }
    }

    public static (RoundPanel box, TextBox tb) Input(string placeholder, bool pw = false, int h = 42)
    {
        var box = new RoundPanel { Height = D(h), Fill = Surface2, Radius = D(h) / 2 };
        var tb = new TextBox { BorderStyle = BorderStyle.None, BackColor = Surface2, ForeColor = Text, Font = F(11), PlaceholderText = placeholder, UseSystemPasswordChar = pw };
        box.Controls.Add(tb);
        box.Resize += (_, _) => { tb.Left = D(18); tb.Width = Math.Max(10, box.Width - D(36)); tb.Top = Math.Max(0, (box.Height - tb.Height) / 2); };
        return (box, tb);
    }
}

class RoundButton : Button
{
    bool hover, down; public int Radius = 10; public bool Pill, OnImage; public Color? Hover;
    protected override void OnResize(EventArgs e) { base.OnResize(e); if (OnImage && Width > 4 && Height > 4) { using var p = U.Round(new Rectangle(0, 0, Width, Height), Pill ? Height / 2 : Ui.D(Radius)); var old = Region; Region = new Region(p); old?.Dispose(); } }
    public RoundButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Color.FromArgb(48, 51, 82); ForeColor = Ui.Text; Font = Ui.F(10); Cursor = Cursors.Hand;
    }
    public override Size GetPreferredSize(Size proposedSize) { var t = TextRenderer.MeasureText(Text, Font); return new Size(t.Width + Ui.D(34), Math.Max(t.Height + Ui.D(16), Ui.D(34))); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        if (!OnImage) g.Clear(Ui.PBg(this, Ui.Surface));
        var c = BackColor; if (!Enabled) c = Color.FromArgb(52, 54, 78); else if (down) c = ControlPaint.Dark(c, 0.08f); else if (hover) c = Hover ?? ControlPaint.Light(c, 0.22f);
        var r = new Rectangle(0, 0, Width - 1, Height - 1); int rad = Pill ? Height / 2 : Math.Min(Ui.D(Radius), Height / 2);
        using (var p = U.Round(r, rad)) using (var br = new SolidBrush(c)) g.FillPath(br, p);
        var fg = Enabled ? ForeColor : Color.FromArgb(120, 122, 150);
        if (Image != null && TextImageRelation == TextImageRelation.ImageAboveText)
        {
            g.DrawImage(Image, (Width - Image.Width) / 2, Ui.D(8), Image.Width, Image.Height);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(2, Ui.D(8) + Image.Height + 2, Width - 4, Height - Image.Height - Ui.D(12)), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return;
        }
        var fl = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | (TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextAlign == ContentAlignment.MiddleRight ? TextFormatFlags.Right : TextFormatFlags.HorizontalCenter);
        if (Width < Ui.D(70)) fl &= ~TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(g, Text, Font, Rectangle.Inflate(r, -Math.Min(Ui.D(10), Width / 6), 0), fg, fl);
    }
}

class RoundPanel : Panel
{
    public int Radius = 14; public Color Fill = Ui.Surface2; public bool Floating;
    public RoundPanel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Floating && Width > 4 && Height > 4) { using var p = U.Round(new Rectangle(0, 0, Width, Height), Radius); var old = Region; Region = new Region(p); old?.Dispose(); }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        if (!Floating) g.Clear(Ui.PBg(this, Ui.Bg)); else g.Clear(Fill);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var p = U.Round(r, Radius); using var br = new SolidBrush(Fill); using var pen = new Pen(Ui.Line);
        g.FillPath(br, p); g.DrawPath(pen, p);
    }
}

class LineProgress : Control
{
    double v; bool indet; float ph; readonly System.Windows.Forms.Timer t = new() { Interval = 30 };
    public LineProgress() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); Height = Ui.D(6); t.Tick += (_, _) => { ph = (ph + 0.018f) % 1f; Invalidate(); }; }
    public double Value { get => v; set { v = Math.Clamp(value, 0, 100); Invalidate(); } }
    public bool Indeterminate { get => indet; set { indet = value; if (value) t.Start(); else t.Stop(); Invalidate(); } }
    protected override void Dispose(bool d) { if (d) t.Dispose(); base.Dispose(d); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Ui.PBg(this, Ui.Surface));
        if (Width < 6 || Height < 2) return;
        var tr = new Rectangle(0, 0, Width - 1, Height - 1);
        using var tp = U.Round(tr, Height / 2); using (var tb = new SolidBrush(Color.FromArgb(58, 61, 92))) g.FillPath(tb, tp);
        int w = indet ? (int)(Width * 0.35) : (int)(Width * v / 100.0); if (w < 2) return;
        int x = indet ? (int)((Width + w) * ph) - w : 0;
        g.SetClip(tp); using var lg = new LinearGradientBrush(new Rectangle(x, 0, w + 1, Height), Ui.Accent, Ui.Accent2, 0f);
        g.FillRectangle(lg, x, 0, w, Height);
    }
}

class DlCard : RoundPanel
{
    readonly Label title = new(), sub = new(), pct = new(); readonly LineProgress bar = new(); readonly RoundButton cancel = new();
    readonly System.Windows.Forms.Timer rm = new() { Interval = 6000 }; public Action? Removed;
    public DlCard(string t, CancellationTokenSource? cts)
    {
        Floating = true; Fill = Ui.Surface2; Radius = Ui.D(14); Size = Ui.D(380, 96);
        title.Text = t; title.Font = Ui.F(10, true); title.ForeColor = Ui.Text; title.AutoEllipsis = true; title.SetBounds(Ui.D(16), Ui.D(10), Ui.D(270), Ui.D(24));
        pct.Font = Ui.F(10, true); pct.ForeColor = Ui.Text; pct.TextAlign = ContentAlignment.MiddleRight; pct.SetBounds(Ui.D(290), Ui.D(10), Ui.D(74), Ui.D(24));
        bar.SetBounds(Ui.D(16), Ui.D(42), Ui.D(348), Ui.D(6)); bar.Indeterminate = true;
        sub.Font = Ui.F(9); sub.ForeColor = Ui.Sub; sub.AutoEllipsis = true; sub.Text = "Connecting…"; sub.SetBounds(Ui.D(16), Ui.D(58), Ui.D(270), Ui.D(24));
        foreach (var l in new[] { title, sub, pct }) l.BackColor = Color.Transparent;
        cancel.Text = "Cancel"; cancel.Pill = true; cancel.Font = Ui.F(9); cancel.SetBounds(Ui.D(294), Ui.D(56), Ui.D(70), Ui.D(28)); cancel.Visible = cts != null;
        cancel.Click += (_, _) => cts?.Cancel();
        Controls.AddRange(new Control[] { title, sub, pct, bar, cancel });
        rm.Tick += (_, _) => { rm.Stop(); Close(); };
    }
    public void Close() { Parent?.Controls.Remove(this); Removed?.Invoke(); Dispose(); }
    public void Report(long got, long total, double speed)
    {
        if (IsDisposed) return; if (InvokeRequired) { BeginInvoke(new Action(() => Report(got, total, speed))); return; }
        if (speed < 0) { sub.Text = "Connection dropped. Reconnecting…"; bar.Indeterminate = true; return; }
        if (total > 0)
        {
            bar.Indeterminate = false; bar.Value = got * 100.0 / total; pct.Text = (int)(got * 100 / total) + "%";
            sub.Text = $"{Ui.Bytes(got)} of {Ui.Bytes(total)}  ·  {Ui.Bytes((long)speed)}/s" + (speed > 1 ? "  ·  " + Ui.Eta(TimeSpan.FromSeconds((total - got) / speed)) : "");
        }
        else { bar.Indeterminate = true; pct.Text = ""; sub.Text = $"{Ui.Bytes(got)}  ·  {Ui.Bytes((long)speed)}/s"; }
    }
    public void Done(string msg) { if (IsDisposed) return; bar.Indeterminate = false; bar.Value = 100; pct.Text = "✓"; sub.Text = msg; cancel.Visible = false; rm.Start(); }
    public void Fail(string msg) { if (IsDisposed) return; bar.Indeterminate = false; pct.Text = "⚠"; sub.Text = msg; cancel.Visible = false; rm.Start(); }
}

class DarkColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Ui.Surface2;
    public override Color ImageMarginGradientBegin => Ui.Surface2; public override Color ImageMarginGradientMiddle => Ui.Surface2; public override Color ImageMarginGradientEnd => Ui.Surface2;
    public override Color MenuItemSelected => Color.FromArgb(62, 66, 104); public override Color MenuItemBorder => Color.FromArgb(62, 66, 104);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(62, 66, 104); public override Color MenuItemSelectedGradientEnd => Color.FromArgb(62, 66, 104);
    public override Color MenuBorder => Ui.Line; public override Color SeparatorDark => Ui.Line; public override Color SeparatorLight => Ui.Line;
    public override Color MenuStripGradientBegin => Ui.Surface2; public override Color MenuStripGradientEnd => Ui.Surface2;
}
class DarkRenderer : ToolStripProfessionalRenderer
{
    public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = Ui.Text; base.OnRenderItemText(e); }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Ui.Text; base.OnRenderArrow(e); }
}

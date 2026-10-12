using System.Drawing.Drawing2D;
namespace UsbDesktop;

class RoundButton : Button
{
    bool hover, down; public int Radius = 10; public bool Pill, OnImage; public Color? Hover;
    public string? Glyph; public float GlyphScale = 0.46f; public Image? Ico; public Color? Underline; public Color? GlyphColor;
    protected override void OnResize(EventArgs e) { base.OnResize(e); if (OnImage && Width > 4 && Height > 4) { using var p = U.Round(new Rectangle(0, 0, Width, Height), Pill ? Height / 2 : Ui.D(Radius)); var old = Region; Region = new Region(p); old?.Dispose(); } }
    public RoundButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Color.FromArgb(48, 51, 82); ForeColor = Ui.Text; Font = Ui.F(10); Cursor = Cursors.Hand;
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        var t = string.IsNullOrEmpty(Text) ? Size.Empty : TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        int w = t.Width + Ui.D(34) + (Ico != null ? Ui.D(26) : 0);
        return new Size(Math.Max(w, Ui.D(40)), Math.Max(t.Height + Ui.D(18), Ui.D(36)));
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        if (!OnImage) g.Clear(Ui.PBg(this, Ui.Surface));
        var c = BackColor; if (!Enabled) c = Color.FromArgb(52, 54, 78); else if (down) c = ControlPaint.Dark(c, 0.08f); else if (hover) c = Hover ?? ControlPaint.Light(c, 0.22f);
        var r = new Rectangle(0, 0, Width - 1, Height - 1); int rad = Pill ? Height / 2 : Math.Min(Ui.D(Radius), Height / 2);
        if (c.A > 0)
        {
            using var p = U.Round(r, rad);
            if (c.A == 255 && Enabled && !Pill && Height > Ui.D(28) && Hover == null && BackColor != Ui.Surface && BackColor != Ui.Surface2)
            { using var lg = new LinearGradientBrush(r, ControlPaint.Light(c, 0.12f), ControlPaint.Dark(c, 0.06f), 90f); g.FillPath(lg, p); }
            else using (var br = new SolidBrush(c)) g.FillPath(br, p);
        }
        var fg = Enabled ? ForeColor : Color.FromArgb(120, 122, 150);
        if (Glyph != null && string.IsNullOrEmpty(Text))
        {
            float s = Math.Min(Width, Height) * GlyphScale; Glyphs.Draw(g, Glyph, new RectangleF((Width - s) / 2f, (Height - s) / 2f, s, s), GlyphColor ?? fg, Math.Max(1.5f, s * 0.11f));
        }
        else if (Image != null && TextImageRelation == TextImageRelation.ImageAboveText)
        {
            int isz = Image.Width; g.DrawImage(Image, new Rectangle((Width - isz) / 2, Ui.D(8), isz, isz));
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Ui.D(3), Ui.D(8) + isz + Ui.D(3), Width - Ui.D(6), Height - isz - Ui.D(14)), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
        else
        {
            int x = Math.Min(Ui.D(12), Math.Max(2, Width / 6));
            bool left = TextAlign == ContentAlignment.MiddleLeft;
            if (Ico != null) { int s = Math.Min(Height - Ui.D(10), Ui.D(30)); int ix = left ? x : Math.Max(x, (Width - s - Ui.D(8) - TextRenderer.MeasureText(Text, Font).Width) / 2); g.DrawImage(Ico, new Rectangle(ix, (Height - s) / 2, s, s)); x = ix + s + Ui.D(8); left = true; }
            var fl = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | (left ? TextFormatFlags.Left : TextAlign == ContentAlignment.MiddleRight ? TextFormatFlags.Right : TextFormatFlags.HorizontalCenter);
            if (Width < Ui.D(70)) fl &= ~TextFormatFlags.EndEllipsis;
            var tr = left ? new Rectangle(x, 0, Math.Max(4, Width - x - Ui.D(8)), Height) : new Rectangle(x, 0, Math.Max(4, Width - x * 2), Height);
            TextRenderer.DrawText(g, Text, Font, tr, fg, fl);
        }
        if (Underline != null && Enabled) { using var ub = new SolidBrush(Underline.Value); using var up = U.Round(new Rectangle(Width / 2 - Width / 5, Height - Ui.D(4), Width * 2 / 5, Ui.D(3)), 2); g.FillPath(ub, up); }
    }
}

class RoundPanel : Panel
{
    public int Radius = 14; public Color Fill = Ui.Surface2; public bool Floating; public Color? Edge;
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
        using var p = U.Round(r, Radius); using var br = new SolidBrush(Fill); using var pen = new Pen(Edge ?? Ui.Line);
        g.FillPath(br, p); g.DrawPath(pen, p);
    }
}

// Play Store style progress line: smooth easing, rounded ends, moving shine.
class LineProgress : Control
{
    double v, shown; bool indet; float ph; public Color? Fill1, Fill2;
    readonly System.Windows.Forms.Timer t = new() { Interval = 20 };
    public LineProgress()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); Height = Ui.D(8);
        t.Tick += (_, _) => { ph = (ph + 0.012f) % 1f; if (!indet) shown += (v - shown) * 0.16; if (Visible) Invalidate(); }; t.Start();
    }
    public double Value { get => v; set { v = Math.Clamp(value, 0, 100); if (shown > v) shown = v; Invalidate(); } }
    public bool Indeterminate { get => indet; set { indet = value; Invalidate(); } }
    protected override void Dispose(bool d) { if (d) t.Dispose(); base.Dispose(d); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Ui.PBg(this, Ui.Surface));
        if (Width < 6 || Height < 2) return;
        var tr = new Rectangle(0, 0, Width - 1, Height - 1);
        using var tp = U.Round(tr, Height / 2); using (var tb = new SolidBrush(Color.FromArgb(58, 61, 92))) g.FillPath(tb, tp);
        var a = Fill1 ?? Ui.Accent; var b = Fill2 ?? Ui.Accent2;
        int w = indet ? (int)(Width * 0.34) : (int)Math.Round((Width - 1) * shown / 100.0); if (w < Height) { if (indet || shown <= 0.2) { if (!indet) return; } else w = Height; }
        int x = indet ? (int)((Width + w) * ph) - w : 0;
        var st = g.Save(); g.SetClip(tp, CombineMode.Intersect);
        var fr = new Rectangle(x, 0, Math.Max(2, w), Height);
        using (var lg = new LinearGradientBrush(new Rectangle(fr.X, fr.Y, fr.Width + 1, fr.Height), a, b, 0f)) { using var fp = U.Round(fr, Height / 2); g.FillPath(lg, fp); }
        if (!indet && w > Ui.D(24))
        {
            int sx = (int)(w * ph * 1.4f) - Ui.D(40); using var sh = new LinearGradientBrush(new Rectangle(sx, 0, Ui.D(60), Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(120, 255, 255, 255), 0f);
            g.SetClip(new Rectangle(0, 0, w, Height), CombineMode.Intersect); g.FillRectangle(sh, sx, 0, Ui.D(60), Height);
        }
        g.Restore(st);
    }
}

class Toggle : Control
{
    bool on; float k; public event Action<bool>? Changed; readonly System.Windows.Forms.Timer t = new() { Interval = 15 };
    public Toggle() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); Size = Ui.D(46, 26); Cursor = Cursors.Hand; t.Tick += (_, _) => { float tg = on ? 1 : 0; k += (tg - k) * 0.35f; if (Math.Abs(tg - k) < 0.02f) { k = tg; t.Stop(); } Invalidate(); }; }
    public bool Checked { get => on; set { if (on == value) return; on = value; t.Start(); Changed?.Invoke(on); } }
    public void SetSilent(bool v) { on = v; k = v ? 1 : 0; Invalidate(); }
    protected override void OnClick(EventArgs e) { Checked = !on; base.OnClick(e); }
    protected override void Dispose(bool d) { if (d) t.Dispose(); base.Dispose(d); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Ui.PBg(this, Ui.Surface));
        var r = new Rectangle(0, 0, Width - 1, Height - 1); using var p = U.Round(r, Height / 2);
        using (var br = new SolidBrush(Ui.Mix(Color.FromArgb(70, 74, 108), Ui.Accent, k))) g.FillPath(br, p);
        int d = Height - Ui.D(8), x = (int)(Ui.D(4) + (Width - d - Ui.D(8)) * k);
        using (var sh = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(sh, x, Ui.D(5), d, d);
        using (var kb = new SolidBrush(Color.White)) g.FillEllipse(kb, x, Ui.D(4), d, d);
    }
}

// Auto-flowing page: every label measures itself, so text can never overlap, at any DPI or font size.
class PagePanel : FlowLayoutPanel
{
    public PagePanel(Color? bg = null)
    {
        Ui.Own(this); Dock = DockStyle.Fill; FlowDirection = FlowDirection.TopDown; WrapContents = false; AutoScroll = true;
        BackColor = bg ?? Ui.Surface; Padding = new Padding(Ui.D(28), Ui.D(22), Ui.D(28), Ui.D(22)); Ui.Dark(this);
    }
    int Inner => Math.Max(Ui.D(220), ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - Ui.D(4));
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        foreach (Control c in Controls) { if (c is Label l && "wrap".Equals(l.Tag)) l.MaximumSize = new Size(Inner, 0); else if (c is FlowLayoutPanel f && "row".Equals(f.Tag)) f.MaximumSize = new Size(Inner, 0); }
    }
    public Label H(string t) { var l = new Label { Text = t, AutoSize = true, Font = Ui.F(19, true), ForeColor = Ui.Text, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Ui.D(8)), Tag = "wrap", MaximumSize = new Size(Inner, 0) }; Controls.Add(l); return l; }
    public Label P(string t, bool sub = false, bool bold = false, float size = 10.5f, int top = 4)
    { var l = new Label { Text = t, AutoSize = true, Font = Ui.F(size, bold), ForeColor = sub ? Ui.Sub : Ui.Text, BackColor = Color.Transparent, Margin = new Padding(0, Ui.D(top), 0, Ui.D(4)), Tag = "wrap", MaximumSize = new Size(Inner, 0) }; Controls.Add(l); return l; }
    public FlowLayoutPanel Row(int top = 6)
    { var f = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = Color.Transparent, Margin = new Padding(0, Ui.D(top), 0, Ui.D(4)), Tag = "row", MaximumSize = new Size(Inner, 0) }; Controls.Add(f); return f; }
    public RoundButton Btn(Control parent, string t, Action a, bool primary = true, string? glyph = null)
    {
        var b = new RoundButton { Text = t, BackColor = primary ? Ui.Accent : Color.FromArgb(52, 55, 86), ForeColor = Color.White, Margin = new Padding(0, 0, Ui.D(10), Ui.D(6)), Font = Ui.F(10) };
        b.Size = b.GetPreferredSize(Size.Empty); b.Click += (_, _) => a(); parent.Controls.Add(b); return b;
    }
    public Toggle Switch(string text, bool on, Action<bool> a)
    {
        var row = Row(8); var tg = new Toggle { Margin = new Padding(0, Ui.D(2), Ui.D(12), 0) }; tg.SetSilent(on); tg.Changed += a; row.Controls.Add(tg);
        var l = new Label { Text = text, AutoSize = true, Font = Ui.F(10.5f), ForeColor = Ui.Text, BackColor = Color.Transparent, Margin = new Padding(0, Ui.D(3), 0, 0) }; l.Click += (_, _) => tg.Checked = !tg.Checked; row.Controls.Add(l); return tg;
    }
    public (RoundPanel box, TextBox tb) Field(Control parent, string placeholder, int width, bool pw = false, string text = "")
    {
        var (box, tb) = Ui.Input(placeholder, pw, 40); box.Width = Ui.D(width); box.Margin = new Padding(0, 0, Ui.D(10), Ui.D(6)); tb.Text = text; parent.Controls.Add(box); return (box, tb);
    }
    public NumericUpDown Num(Control parent, int min, int max, int val, int inc, Action<int>? ch = null)
    {
        var n = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Clamp(val, min, max), Increment = inc, Font = Ui.F(11), BackColor = Ui.Surface2, ForeColor = Ui.Text, Width = Ui.D(96), Margin = new Padding(0, Ui.D(2), Ui.D(12), Ui.D(6)), BorderStyle = BorderStyle.FixedSingle };
        if (ch != null) n.ValueChanged += (_, _) => ch((int)n.Value); parent.Controls.Add(n); return n;
    }
    public Label Inline(Control parent, string t, bool sub = false) { var l = new Label { Text = t, AutoSize = true, Font = Ui.F(10.5f), ForeColor = sub ? Ui.Sub : Ui.Text, BackColor = Color.Transparent, Margin = new Padding(0, Ui.D(8), Ui.D(10), 0) }; parent.Controls.Add(l); return l; }
    public Panel Gap(int h) { var p = new Panel { Height = Ui.D(h), Width = Ui.D(10), BackColor = Color.Transparent, Margin = Padding.Empty }; Controls.Add(p); return p; }
}

// Download / install card, Play Store style.
class DlCard : RoundPanel
{
    readonly Label title = new(), sub = new(), pct = new(); readonly LineProgress bar = new(); readonly RoundButton cancel = new(); readonly PictureBox ic = new();
    readonly System.Windows.Forms.Timer rm = new() { Interval = 5000 }; public Action? Removed; bool finished;
    public DlCard(string t, CancellationTokenSource? cts, string icon = "store")
    {
        Floating = true; Fill = Color.FromArgb(30, 32, 54); Edge = Color.FromArgb(70, 74, 120); Radius = Ui.D(16); Size = Ui.D(400, 104);
        ic.SetBounds(Ui.D(14), Ui.D(14), Ui.D(46), Ui.D(46)); ic.SizeMode = PictureBoxSizeMode.Zoom; ic.BackColor = Color.Transparent; ic.Image = Icons.Scaled(Icons.App(icon), Ui.D(46));
        title.Text = t; title.Font = Ui.F(10.5f, true); title.ForeColor = Ui.Text; title.AutoEllipsis = true; title.SetBounds(Ui.D(72), Ui.D(12), Ui.D(250), Ui.D(24));
        pct.Font = Ui.F(14, true); pct.ForeColor = Ui.Text; pct.TextAlign = ContentAlignment.MiddleRight; pct.SetBounds(Ui.D(300), Ui.D(10), Ui.D(70), Ui.D(30));
        sub.Font = Ui.F(9); sub.ForeColor = Ui.Sub; sub.AutoEllipsis = true; sub.Text = "Starting…"; sub.SetBounds(Ui.D(72), Ui.D(38), Ui.D(250), Ui.D(22));
        bar.SetBounds(Ui.D(72), Ui.D(72), Ui.D(296), Ui.D(8)); bar.Indeterminate = true;
        foreach (var l in new[] { title, sub, pct }) l.BackColor = Color.Transparent;
        cancel.Glyph = "close"; cancel.Pill = true; cancel.BackColor = Color.FromArgb(0, 0, 0, 0); cancel.Hover = Color.FromArgb(60, 64, 100); cancel.GlyphScale = 0.4f; cancel.SetBounds(Ui.D(366), Ui.D(8), Ui.D(26), Ui.D(26)); cancel.Visible = cts != null;
        cancel.Click += (_, _) => cts?.Cancel();
        Controls.AddRange(new Control[] { ic, title, sub, pct, bar, cancel });
        rm.Tick += (_, _) => { rm.Stop(); Close(); };
    }
    protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); if (finished) Close(); }
    public void Close() { if (IsDisposed) return; Parent?.Controls.Remove(this); Removed?.Invoke(); Dispose(); }
    void UI(Action a) { if (IsDisposed) return; if (InvokeRequired) { try { BeginInvoke(a); } catch { } } else a(); }
    public void Report(long got, long total, double speed) => UI(() =>
    {
        if (speed < 0) { sub.Text = "Connection dropped. Reconnecting…"; bar.Indeterminate = true; return; }
        if (total > 0)
        {
            bar.Indeterminate = false; double p = Math.Min(100, got * 100.0 / total); bar.Value = p; pct.Text = (int)p + "%";
            sub.Text = $"{Ui.Bytes(got)} of {Ui.Bytes(total)}  ·  {Ui.Bytes((long)speed)}/s" + (speed > 1 && total > got ? "  ·  " + Ui.Eta(TimeSpan.FromSeconds((total - got) / speed)) : "");
        }
        else { bar.Indeterminate = true; pct.Text = ""; sub.Text = $"{Ui.Bytes(got)} downloaded  ·  {Ui.Bytes((long)speed)}/s"; }
    });
    public void Stage(string text, double pctVal = -1) => UI(() =>
    {
        sub.Text = text; if (pctVal < 0) { bar.Indeterminate = true; pct.Text = ""; } else { bar.Indeterminate = false; bar.Value = pctVal; pct.Text = (int)pctVal + "%"; }
    });
    public void Done(string msg) => UI(() => { finished = true; bar.Indeterminate = false; bar.Value = 100; bar.Fill1 = Color.FromArgb(34, 197, 94); bar.Fill2 = Color.FromArgb(110, 231, 160); pct.Text = "100%"; sub.Text = msg; cancel.Visible = false; rm.Start(); });
    public void Fail(string msg) => UI(() => { finished = true; bar.Indeterminate = false; bar.Fill1 = Color.FromArgb(239, 68, 68); bar.Fill2 = Color.FromArgb(251, 146, 120); pct.Text = "!"; sub.Text = msg; cancel.Visible = false; rm.Interval = 9000; rm.Start(); });
}

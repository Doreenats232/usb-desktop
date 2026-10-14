using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace UsbDesktop;

static class Ui
{
    public static readonly float S = Dpi();
    static float Dpi() { try { using var g = Graphics.FromHwnd(IntPtr.Zero); return Math.Max(1f, g.DpiX / 96f); } catch { return 1f; } }
    public static int D(int v) => (int)Math.Round(v * S);
    public static int Unscale(int v) => (int)Math.Round(v / S);
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

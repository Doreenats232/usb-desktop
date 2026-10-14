using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace UsbDesktop;

static class PcInfo
{
    public static string Cpu()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            var n = (k?.GetValue("ProcessorNameString") as string ?? "Unknown CPU");
            n = n.Replace("(R)", "").Replace("(TM)", "").Replace(" CPU", "").Replace("Processor", ""); return Regex.Replace(n, @"\s+", " ").Trim();
        }
        catch { return "Unknown CPU"; }
    }
    public static string Id()
    {
        string g = Environment.MachineName;
        try { using var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64); using var k = b.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"); g = k?.GetValue("MachineGuid") as string ?? g; } catch { }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(g)))[..16];
    }
    public static string Ram() { try { return Math.Round(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824.0) + " GB"; } catch { return "?"; } }
    public static string Os() => Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : Environment.OSVersion.Version.Major >= 10 ? "Windows 10" : "Windows";
    static string Gb(long b) => b >= 1099511627776 ? Math.Round(b / 1099511627776.0, 1) + " TB" : (b / 1073741824) + " GB";
    public static string Usb() { try { var d = new DriveInfo(Path.GetPathRoot(Paths.Root)!); return Gb(d.AvailableFreeSpace) + " free of " + Gb(d.TotalSize); } catch { return "?"; } }
    public static string PcDisk() { try { var d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!); return Gb(d.AvailableFreeSpace) + " free of " + Gb(d.TotalSize); } catch { return "?"; } }

    // Remembers which computers this USB has been plugged into (only a short hash is stored, never the machine's real ID).
    public static (bool returning, int visits, DateTime last) Visit()
    {
        var f = Path.Combine(Paths.Data, "pcs.txt"); var id = Id(); var lines = new List<string>(); bool ret = false; int visits = 1; var last = DateTime.Now;
        try
        {
            if (File.Exists(f)) lines = File.ReadAllLines(f).ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                var p = lines[i].Split('|'); if (p.Length < 5 || p[0] != id) continue;
                ret = true; visits = int.Parse(p[4]) + 1; last = new DateTime(long.Parse(p[3])); lines[i] = $"{id}|{Environment.MachineName}|{p[2]}|{DateTime.Now.Ticks}|{visits}"; break;
            }
            if (!ret) lines.Add($"{id}|{Environment.MachineName}|{DateTime.Now.Ticks}|{DateTime.Now.Ticks}|1");
            File.WriteAllLines(f, lines);
        }
        catch { }
        return (ret, visits, last);
    }
}

// Animated companion that pops up when the USB desktop opens on a computer.
class Companion : Form
{
    readonly string title, body; readonly (string k, string v)[] chips; readonly System.Windows.Forms.Timer tm = new() { Interval = 33 };
    int t; bool closing; Point end; readonly string who;
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x80; return cp; } }

    public static void Greet()
    {
        var (ret, visits, last) = PcInfo.Visit(); var pc = Environment.MachineName; var me = Cfg.Get("name", Environment.UserName);
        string title, body;
        if (ret)
        {
            var span = DateTime.Now - last; string ago = span.TotalHours < 2 ? "just a little while ago" : span.TotalDays < 1 ? "earlier today" : span.TotalDays < 2 ? "yesterday" : (int)span.TotalDays + " days ago";
            title = $"Ohh, we're back on {pc}!"; body = $"This is visit number {visits}. We were last here {ago}. Everything is right where you left it, {me}.";
        }
        else { title = "Ooh, a new computer!"; body = $"I see you've plugged me into {pc}. Let me look around… It runs {PcInfo.Os()} with {Environment.ProcessorCount} processor cores. Nice to meet you here, {me}!"; }
        var chips = new[] { ("CPU", PcInfo.Cpu()), ("RAM", PcInfo.Ram()), ("USB", PcInfo.Usb()), ("PC disk", PcInfo.PcDisk()) };
        new Companion(title, body, chips, Cfg.Get("buddy", "Nova")).Show();
    }

    Companion(string title, string body, (string, string)[] chips, string who)
    {
        this.title = title; this.body = body; this.chips = chips; this.who = who;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; DoubleBuffered = true; BackColor = Ui.Surface; Opacity = 0; StartPosition = FormStartPosition.Manual;
        Size = Ui.D(540, 262); var wa = Screen.PrimaryScreen!.WorkingArea; end = new Point(wa.Right - Width - Ui.D(18), wa.Bottom - Height - Ui.D(14)); Location = new Point(end.X, end.Y + Ui.D(40));
        Resize += (_, _) => { using var p = U.Round(new Rectangle(0, 0, Width, Height), Ui.D(20)); var old = Region; Region = new Region(p); old?.Dispose(); };
        MouseClick += (_, _) => closing = true;
        tm.Tick += (_, _) =>
        {
            t += 33; if (t > 12500) closing = true;
            double op = closing ? Opacity - 0.09 : Math.Min(1.0, t / 380.0); Opacity = Math.Max(0, op);
            double ease = 1 - Math.Pow(1 - Math.Min(1.0, t / 480.0), 3); Location = new Point(end.X, end.Y + (int)(Ui.D(40) * (1 - ease)) + (closing ? (int)((1 - Opacity) * Ui.D(20)) : 0));
            if (closing && Opacity <= 0.02) { tm.Stop(); Close(); return; }
            Invalidate();
        };
        tm.Start();
    }
    protected override void Dispose(bool d) { if (d) tm.Dispose(); base.Dispose(d); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        using (var bg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(34, 36, 62), Color.FromArgb(22, 23, 42), 60f)) g.FillRectangle(bg, ClientRectangle);
        using (var lg = new LinearGradientBrush(new Rectangle(0, 0, Width, Ui.D(4)), Theme.Accent, Ui.Accent2, 0f)) g.FillRectangle(lg, 0, 0, Width, Ui.D(4));
        using (var pn = new Pen(Color.FromArgb(70, 74, 120))) g.DrawRectangle(pn, 0, 0, Width - 1, Height - 1);
        Mascot(g, Ui.D(84), Ui.D(112), Ui.D(128));
        int tx = Ui.D(168), tw = Width - tx - Ui.D(20);
        TextRenderer.DrawText(g, who.ToUpperInvariant(), Ui.F(8, true), new Rectangle(tx, Ui.D(18), tw, Ui.D(18)), Color.FromArgb(150, 154, 210), TextFormatFlags.NoPadding | TextFormatFlags.Left);
        TextRenderer.DrawText(g, title, Ui.F(14, true), new Rectangle(tx, Ui.D(36), tw, Ui.D(30)), Color.White, TextFormatFlags.NoPadding | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        int n = Math.Clamp((t - 450) / 22, 0, body.Length); var shown = body[..n] + (n < body.Length && (t / 250) % 2 == 0 ? "|" : "");
        TextRenderer.DrawText(g, shown, Ui.F(10), new Rectangle(tx, Ui.D(72), tw, Ui.D(90)), Color.FromArgb(214, 216, 240), TextFormatFlags.NoPadding | TextFormatFlags.Left | TextFormatFlags.WordBreak);
        int x = tx, y = Height - Ui.D(76), ch = Ui.D(28), gap = Ui.D(6); int reveal = Math.Clamp((t - 1400) / 260, 0, chips.Length);
        for (int i = 0; i < reveal; i++)
        {
            var (k, v) = chips[i]; var fk = Ui.F(8, true); var fv = Ui.F(9);
            int kw = TextRenderer.MeasureText(k, fk, new Size(999, 99), TextFormatFlags.NoPadding).Width, vw = Math.Min(tw - kw - Ui.D(28), TextRenderer.MeasureText(v, fv, new Size(999, 99), TextFormatFlags.NoPadding).Width);
            int w = kw + vw + Ui.D(26); if (x + w > tx + tw) { x = tx; y += ch + gap; }
            using var p = U.Round(new Rectangle(x, y, w, ch), ch / 2); using (var b = new SolidBrush(Color.FromArgb(48, 51, 84))) g.FillPath(b, p);
            TextRenderer.DrawText(g, k, fk, new Rectangle(x + Ui.D(10), y, kw + 2, ch), Color.FromArgb(150, 154, 210), TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, v, fv, new Rectangle(x + Ui.D(16) + kw, y, vw + 2, ch), Color.White, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            x += w + gap;
        }
    }

    void Mascot(Graphics g, int cx, int cy, int s)
    {
        float bob = (float)Math.Sin(t / 380.0) * s * 0.035f; float fy = cy + bob;
        using (var sb = new SolidBrush(Color.FromArgb(55, 0, 0, 0))) { float sw = s * (0.55f - bob / s); g.FillEllipse(sb, cx - sw / 2, cy + s * 0.52f, sw, s * 0.09f); }
        var body = new RectangleF(cx - s * 0.42f, fy - s * 0.40f, s * 0.84f, s * 0.82f);
        float ax = cx + (float)Math.Sin(t / 520.0) * s * 0.07f, ay = fy - s * 0.60f;
        using (var pn = new Pen(Color.FromArgb(190, 194, 255), s * 0.03f) { StartCap = LineCap.Round }) g.DrawLine(pn, cx, fy - s * 0.38f, ax, ay);
        using (var ab = new LinearGradientBrush(new RectangleF(ax - s * 0.06f, ay - s * 0.06f, s * 0.12f, s * 0.12f), Color.FromArgb(255, 230, 120), Color.FromArgb(255, 150, 90), 90f)) g.FillEllipse(ab, ax - s * 0.06f, ay - s * 0.06f, s * 0.12f, s * 0.12f);
        var saved = g.Save(); g.TranslateTransform(cx + s * 0.40f, fy + s * 0.08f); g.RotateTransform(t < 2800 ? -40 + (float)Math.Sin(t / 85.0) * 28 : -12 + (float)Math.Sin(t / 600.0) * 4);
        using (var ar = new LinearGradientBrush(new RectangleF(-s * 0.05f, -s * 0.02f, s * 0.1f, s * 0.3f), Theme.Accent, Ui.Accent2, 90f)) { using var arm = U.Round(new Rectangle((int)(-s * 0.05f), (int)(-s * 0.02f), (int)(s * 0.1f), (int)(s * 0.3f)), (int)(s * 0.05f)); g.FillPath(ar, arm); }
        g.Restore(saved);
        using (var br = new LinearGradientBrush(body, Ui.Accent2, Theme.Accent, 80f)) g.FillEllipse(br, body);
        using (var hl = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillEllipse(hl, body.X + s * 0.12f, body.Y + s * 0.07f, s * 0.34f, s * 0.2f);
        var m = PointToClient(Cursor.Position); float dx = m.X - cx, dy = m.Y - fy, len = (float)Math.Max(1, Math.Sqrt(dx * dx + dy * dy)); dx = dx / len * s * 0.045f; dy = dy / len * s * 0.045f;
        bool blink = t % 3300 > 3150; float eh = s * 0.26f * (blink ? 0.12f : 1f);
        foreach (var sx in new[] { -1, 1 })
        {
            float ex = cx + sx * s * 0.18f - s * 0.1f, ey = fy - s * 0.1f + (s * 0.26f - eh) / 2;
            g.FillEllipse(Brushes.White, ex, ey, s * 0.2f, eh);
            if (!blink) { using var pb = new SolidBrush(Color.FromArgb(30, 32, 60)); g.FillEllipse(pb, ex + s * 0.05f + dx, ey + eh / 2 - s * 0.05f + dy, s * 0.1f, s * 0.1f); g.FillEllipse(Brushes.White, ex + s * 0.09f + dx, ey + eh / 2 - s * 0.045f + dy, s * 0.035f, s * 0.035f); }
            using var ch = new SolidBrush(Color.FromArgb(95, 255, 140, 170)); g.FillEllipse(ch, cx + sx * s * 0.3f - s * 0.06f, fy + s * 0.1f, s * 0.12f, s * 0.07f);
        }
        using (var mp = new Pen(Color.FromArgb(30, 32, 60), s * 0.035f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(mp, cx - s * 0.12f, fy + s * 0.07f, s * 0.24f, s * 0.17f, 20, 140);
        for (int i = 0; i < 4; i++)
        {
            double a = i * 1.7 + t / 1400.0; float px = cx + (float)Math.Cos(a) * s * 0.62f, py = fy + (float)Math.Sin(a * 1.3) * s * 0.5f, z = s * 0.06f * (float)(0.5 + 0.5 * Math.Sin(t / 260.0 + i * 1.9));
            using var sp = new SolidBrush(Color.FromArgb(200, 255, 255, 255)); g.FillPolygon(sp, new[] { new PointF(px, py - z), new PointF(px + z * 0.3f, py - z * 0.3f), new PointF(px + z, py), new PointF(px + z * 0.3f, py + z * 0.3f), new PointF(px, py + z), new PointF(px - z * 0.3f, py + z * 0.3f), new PointF(px - z, py), new PointF(px - z * 0.3f, py - z * 0.3f) });
        }
    }
}

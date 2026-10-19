using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
namespace UsbDesktop;

// Vector-drawn, full-colour app icons and UI glyphs. Everything is drawn at high resolution and
// scaled down smoothly, so nothing looks pixelated and nothing depends on emoji fonts.
static class Icons
{
    static readonly Dictionary<string, Bitmap> cache = new();
    const int N = 256;

    static GraphicsPath RR(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath(); float d = Math.Max(0.01f, Math.Min(r * 2, Math.Min(w, h)));
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90); p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    static void Fill(Graphics g, GraphicsPath p, Color a, Color b, float ang = 90f)
    {
        var r = p.GetBounds(); if (r.Width < 1 || r.Height < 1) return;
        using var br = new LinearGradientBrush(Rectangle.Inflate(Rectangle.Round(r), 1, 1), a, b, ang); g.FillPath(br, p);
    }
    static Color C(int r, int g, int b, int a = 255) => Color.FromArgb(a, r, g, b);
    static void Soft(Graphics g, RectangleF r, float rad, int a = 16)
    {
        for (int i = 10; i >= 1; i--)
        {
            var q = RectangleF.Inflate(r, i * 2.2f, i * 2.2f); q.Offset(0, 9);
            using var p = RR(q.X, q.Y, q.Width, q.Height, rad + i * 2); using var b = new SolidBrush(C(0, 0, 0, a / 2)); g.FillPath(b, p);
        }
    }
    static void Gloss(Graphics g, GraphicsPath clip, RectangleF r)
    {
        var st = g.Save(); g.SetClip(clip, CombineMode.Intersect);
        using var br = new LinearGradientBrush(new RectangleF(r.X, r.Y, r.Width, r.Height * 0.55f), C(255, 255, 255, 90), C(255, 255, 255, 0), 90f);
        g.FillRectangle(br, r.X, r.Y, r.Width, r.Height * 0.55f); g.Restore(st);
    }
    static Bitmap Make(Action<Graphics> draw)
    {
        var b = new Bitmap(N, N); using var g = Graphics.FromImage(b);
        g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        draw(g); return b;
    }
    static void Tile(Graphics g, Color a, Color b, Action<Graphics> glyph)
    {
        var r = new RectangleF(22, 18, 212, 212); Soft(g, r, 52);
        using var p = RR(r.X, r.Y, r.Width, r.Height, 52); Fill(g, p, a, b, 70f); Gloss(g, p, r);
        using (var pen = new Pen(C(255, 255, 255, 70), 3)) g.DrawPath(pen, p);
        glyph(g);
    }
    static void Txt(Graphics g, string s, float size, Color c, RectangleF r, bool bold = true)
    {
        using var f = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); using var br = new SolidBrush(c);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(s, f, br, r, sf);
    }
    static void Page(Graphics g, Color band, string label)
    {
        var r = new RectangleF(54, 22, 148, 206); Soft(g, r, 18, 14);
        var pg = new GraphicsPath(); pg.AddLines(new[] { new PointF(54 + 18, 22), new PointF(202 - 40, 22), new PointF(202, 22 + 40), new PointF(202, 228 - 18) });
        pg.AddArc(202 - 36, 228 - 36, 36, 36, 0, 90); pg.AddLine(202 - 18, 228, 54 + 18, 228); pg.AddArc(54, 228 - 36, 36, 36, 90, 90); pg.AddLine(54, 228 - 18, 54, 22 + 18); pg.AddArc(54, 22, 36, 36, 180, 90); pg.CloseFigure();
        Fill(g, pg, C(255, 255, 255), C(226, 232, 242), 90f);
        using (var pen = new Pen(C(160, 170, 190), 3)) g.DrawPath(pen, pg);
        using (var fold = new GraphicsPath()) { fold.AddPolygon(new[] { new PointF(162, 22), new PointF(202, 62), new PointF(162, 62) }); Fill(g, fold, C(205, 214, 230), C(176, 188, 210), 45f); }
        using var bp = RR(54, 128, 148, 62, 10); Fill(g, bp, ControlPaint.Light(band, 0.25f), ControlPaint.Dark(band, 0.1f), 90f);
        Txt(g, label, 40, Color.White, new RectangleF(54, 128, 148, 62));
        using var lp = new Pen(C(190, 200, 220), 8) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(lp, 80, 84, 150, 84); g.DrawLine(lp, 80, 106, 176, 106);
    }

    public static Bitmap App(string key)
    {
        lock (cache)
        {
            if (cache.TryGetValue(key, out var hit)) return hit;
            var b = Build(key); cache[key] = b; return b;
        }
    }
    public static Bitmap Scaled(Image src, int s)
    {
        s = Math.Max(8, s); var b = new Bitmap(s, s); using var g = Graphics.FromImage(b);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawImage(src, new Rectangle(0, 0, s, s)); return b;
    }

    public static Bitmap ForFile(string path)
    {
        if (Directory.Exists(path)) return App("folder");
        var e = Path.GetExtension(path).ToLowerInvariant();
        switch (e)
        {
            case ".png": case ".jpg": case ".jpeg": case ".gif": case ".bmp": case ".webp": return App("photo");
            case ".mp3": case ".wav": case ".flac": case ".m4a": case ".ogg": return App("music");
            case ".mp4": case ".mkv": case ".avi": case ".mov": case ".webm": return App("video");
            case ".zip": case ".rar": case ".7z": case ".tar": case ".gz": return App("zip");
            case ".txt": case ".md": case ".log": case ".ini": return App("doc:TXT:59,130,246");
            case ".pdf": return App("doc:PDF:220,38,38");
            case ".doc": case ".docx": return App("doc:DOC:37,99,235");
            case ".xls": case ".xlsx": case ".csv": return App("doc:XLS:22,163,74");
            case ".ppt": case ".pptx": return App("doc:PPT:234,88,12");
            case ".html": case ".htm": return App("doc:WEB:20,184,166");
            case ".js": case ".cs": case ".py": case ".css": case ".json": case ".bat": case ".ts": case ".java": case ".cpp": case ".xml": return App("code");
            case ".apk": return App("doc:APK:34,160,90");
            case ".iso": return App("doc:ISO:100,116,139");
            case ".exe": case ".lnk": case ".msi":
                try { using var ic = Icon.ExtractAssociatedIcon(path)!; using var bm = ic.ToBitmap(); var b = new Bitmap(N, N); using var g = Graphics.FromImage(b); g.InterpolationMode = InterpolationMode.NearestNeighbor; g.DrawImage(bm, 32, 32, 192, 192); return b; } catch { }
                return App("exe");
        }
        return App("doc:" + (e.Length > 1 ? e[1..Math.Min(e.Length, 5)].ToUpperInvariant() : "FILE") + ":100,116,139");
    }

    static Bitmap Build(string key)
    {
        if (key.StartsWith("doc:")) { var p = key.Split(':'); var c = p[2].Split(','); return Make(g => Page(g, C(int.Parse(c[0]), int.Parse(c[1]), int.Parse(c[2])), p[1])); }
        switch (key)
        {
            case "folder":
                return Make(g =>
                {
                    Soft(g, new RectangleF(24, 56, 208, 160), 22, 12);
                    using var back = RR(24, 44, 208, 150, 20); Fill(g, back, C(245, 170, 30), C(222, 130, 8), 90f);
                    using var tab = new GraphicsPath(); tab.AddArc(24, 36, 40, 40, 180, 90); tab.AddLine(44, 36, 92, 36); tab.AddLine(92, 36, 108, 62); tab.AddLine(108, 62, 232, 62); tab.AddLine(232, 62, 232, 90); tab.AddLine(24, 90, 24, 56); tab.CloseFigure(); Fill(g, tab, C(255, 205, 85), C(245, 165, 25), 90f);
                    using var front = RR(24, 86, 208, 138, 20); Fill(g, front, C(255, 214, 102), C(247, 168, 26), 90f); Gloss(g, front, new RectangleF(24, 86, 208, 138));
                    using var pen = new Pen(C(255, 255, 255, 120), 3); g.DrawPath(pen, front);
                });
            case "notepad":
                return Make(g => Tile(g, C(76, 168, 255), C(26, 92, 214), gg =>
                {
                    using var pg = RR(66, 56, 124, 150, 14); Fill(gg, pg, C(255, 255, 255), C(228, 236, 250), 90f);
                    using var hd = new GraphicsPath(); hd.AddArc(66, 56, 28, 28, 180, 90); hd.AddLine(80, 56, 176, 56); hd.AddArc(162, 56, 28, 28, 270, 90); hd.AddLine(190, 84, 190, 84); hd.AddLine(190, 84, 66, 84); hd.CloseFigure(); Fill(gg, hd, C(255, 99, 99), C(224, 52, 52), 90f);
                    using var lp = new Pen(C(120, 150, 205), 7) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    for (int i = 0; i < 4; i++) gg.DrawLine(lp, 82, 108 + i * 24, i == 3 ? 140 : 174, 108 + i * 24);
                    using var rp = new Pen(C(255, 255, 255), 6) { StartCap = LineCap.Round, EndCap = LineCap.Round }; foreach (var x in new[] { 92, 128, 164 }) gg.DrawLine(rp, x, 48, x, 66);
                }));
            case "calc":
                return Make(g => Tile(g, C(58, 66, 90), C(24, 28, 44), gg =>
                {
                    using var dp = RR(60, 52, 136, 48, 12); Fill(gg, dp, C(170, 240, 210), C(96, 200, 160), 90f);
                    Txt(gg, "3.14", 34, C(20, 70, 60), new RectangleF(60, 52, 128, 48));
                    var cols = new[] { C(255, 255, 255, 235), C(255, 255, 255, 235), C(255, 255, 255, 235), C(255, 150, 60) };
                    for (int r = 0; r < 3; r++) for (int c = 0; c < 4; c++) { using var bp = RR(60 + c * 36, 112 + r * 34, 28, 26, 8); using var br = new SolidBrush(cols[c]); gg.FillPath(br, bp); }
                }));
            case "paint":
                return Make(g => Tile(g, C(255, 120, 170), C(200, 40, 120), gg =>
                {
                    using var pal = new GraphicsPath(); pal.AddEllipse(46, 60, 164, 130); Fill(gg, pal, C(255, 247, 232), C(238, 214, 178), 90f);
                    using var hole = new SolidBrush(C(200, 50, 120)); gg.FillEllipse(hole, 142, 128, 34, 30);
                    var dots = new[] { (C(239, 68, 68), 74, 86), (C(250, 204, 21), 118, 72), (C(34, 197, 94), 164, 86), (C(59, 130, 246), 66, 134), (C(168, 85, 247), 98, 160) };
                    foreach (var (c, x, y) in dots) { using var br = new SolidBrush(c); gg.FillEllipse(br, x - 15, y - 15, 30, 30); using var pn = new Pen(C(255, 255, 255, 150), 3); gg.DrawEllipse(pn, x - 15, y - 15, 30, 30); }
                    using var brush = new Pen(C(120, 80, 40), 12) { StartCap = LineCap.Round, EndCap = LineCap.Round }; gg.DrawLine(brush, 150, 188, 204, 72); using var tip = new Pen(C(59, 130, 246), 12) { StartCap = LineCap.Round, EndCap = LineCap.Round }; gg.DrawLine(tip, 204, 72, 212, 54);
                }));
            case "term":
                return Make(g => Tile(g, C(52, 60, 84), C(14, 18, 30), gg =>
                {
                    using var win = RR(46, 62, 164, 132, 16); using (var b = new SolidBrush(C(12, 16, 26))) gg.FillPath(b, win);
                    using var tb = new GraphicsPath(); tb.AddArc(46, 62, 32, 32, 180, 90); tb.AddLine(62, 62, 194, 62); tb.AddArc(178, 62, 32, 32, 270, 90); tb.AddLine(210, 84, 46, 84); tb.CloseFigure(); using (var b = new SolidBrush(C(40, 48, 70))) gg.FillPath(b, tb);
                    foreach (var (c, x) in new[] { (C(255, 95, 86), 62), (C(255, 189, 46), 82), (C(39, 201, 63), 102) }) { using var b = new SolidBrush(c); gg.FillEllipse(b, x - 6, 67, 12, 12); }
                    using var pn = new Pen(C(74, 222, 128), 11) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }; gg.DrawLines(pn, new[] { new PointF(70, 110), new PointF(98, 132), new PointF(70, 154) }); gg.DrawLine(pn, 112, 156, 150, 156);
                }));
            case "settings":
                return Make(g => Tile(g, C(130, 146, 170), C(60, 72, 96), gg =>
                {
                    var pts = new List<PointF>(); float cx = 128, cy = 128;
                    for (int i = 0; i < 16; i++) { double a0 = i * Math.PI / 8, a1 = a0 + Math.PI / 16, a2 = a0 + Math.PI / 8 - Math.PI / 32; float ro = 78, ri = 62; pts.Add(new PointF(cx + (float)Math.Cos(a0 + 0.04) * ri, cy + (float)Math.Sin(a0 + 0.04) * ri)); pts.Add(new PointF(cx + (float)Math.Cos(a0 + 0.1) * ro, cy + (float)Math.Sin(a0 + 0.1) * ro)); pts.Add(new PointF(cx + (float)Math.Cos(a1 + 0.1) * ro, cy + (float)Math.Sin(a1 + 0.1) * ro)); pts.Add(new PointF(cx + (float)Math.Cos(a1 + 0.16) * ri, cy + (float)Math.Sin(a1 + 0.16) * ri)); }
                    using var gp = new GraphicsPath(); gp.AddPolygon(pts.ToArray()); Fill(gg, gp, C(255, 255, 255), C(205, 214, 230), 90f);
                    using var hole = new GraphicsPath(); hole.AddEllipse(98, 98, 60, 60); Fill(gg, hole, C(70, 84, 112), C(40, 50, 74), 90f); using var pn = new Pen(C(255, 255, 255, 140), 3); gg.DrawPath(pn, hole);
                }));
            case "browser":
                return Make(g =>
                {
                    Soft(g, new RectangleF(30, 30, 196, 196), 98, 14);
                    var r = new RectangleF(28, 28, 200, 200);
                    using (var b = new SolidBrush(C(234, 67, 53))) g.FillPie(b, r.X, r.Y, r.Width, r.Height, 180, 120);
                    using (var b = new SolidBrush(C(251, 188, 5))) g.FillPie(b, r.X, r.Y, r.Width, r.Height, 300, 120);
                    using (var b = new SolidBrush(C(52, 168, 83))) g.FillPie(b, r.X, r.Y, r.Width, r.Height, 60, 120);
                    using (var b = new SolidBrush(C(255, 255, 255))) g.FillEllipse(b, 128 - 52, 128 - 52, 104, 104);
                    using (var b = new LinearGradientBrush(new RectangleF(84, 84, 88, 88), C(92, 150, 255), C(26, 100, 232), 90f)) g.FillEllipse(b, 128 - 41, 128 - 41, 82, 82);
                });
            case "shot":
                return Make(g => Tile(g, C(176, 128, 255), C(98, 52, 214), gg =>
                {
                    using var body = RR(46, 82, 164, 112, 22); Fill(gg, body, C(255, 255, 255), C(222, 214, 250), 90f);
                    using var top = RR(92, 62, 72, 34, 10); using (var b = new SolidBrush(C(236, 230, 252))) gg.FillPath(b, top);
                    using (var b = new SolidBrush(C(60, 40, 140))) gg.FillEllipse(b, 88, 100, 80, 80);
                    using (var b = new LinearGradientBrush(new RectangleF(98, 110, 60, 60), C(120, 190, 255), C(40, 90, 220), 90f)) gg.FillEllipse(b, 98, 110, 60, 60);
                    using (var b = new SolidBrush(C(255, 255, 255, 170))) gg.FillEllipse(b, 108, 118, 16, 12); using (var b = new SolidBrush(C(255, 120, 90))) gg.FillEllipse(b, 180, 96, 12, 12);
                }));
            case "bin":
                return Make(g =>
                {
                    Soft(g, new RectangleF(54, 60, 148, 170), 20, 12);
                    using var lid = RR(46, 52, 164, 28, 12); Fill(g, lid, C(200, 210, 226), C(150, 162, 184), 90f);
                    using var h = RR(98, 34, 60, 26, 10); using (var b = new SolidBrush(C(170, 182, 204))) g.FillPath(b, h);
                    using var body = new GraphicsPath(); body.AddPolygon(new[] { new PointF(62, 84), new PointF(194, 84), new PointF(182, 226), new PointF(74, 226) }); Fill(g, body, C(236, 242, 252), C(168, 182, 208), 90f); Gloss(g, body, new RectangleF(62, 84, 132, 142));
                    using var pn = new Pen(C(120, 136, 166, 150), 8) { StartCap = LineCap.Round, EndCap = LineCap.Round }; foreach (var x in new[] { 100, 128, 156 }) g.DrawLine(pn, x, 110, x + (x - 128) / 12f, 202);
                    using var rc = new Pen(C(34, 170, 110), 9) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }; g.DrawArc(rc, 104, 130, 48, 48, 200, 240);
                });
            case "store":
                return Make(g => Tile(g, C(255, 110, 130), C(214, 40, 90), gg =>
                {
                    using var bag = RR(58, 86, 140, 122, 20); Fill(gg, bag, C(255, 255, 255), C(255, 226, 232), 90f);
                    using var hp = new Pen(C(255, 255, 255), 12) { StartCap = LineCap.Round, EndCap = LineCap.Round }; gg.DrawArc(hp, 88, 44, 80, 84, 180, 180);
                    using var m = new GraphicsPath(); m.AddEllipse(98, 118, 60, 60); Fill(gg, m, C(255, 110, 130), C(214, 40, 90), 90f); Txt(gg, "M", 44, Color.White, new RectangleF(98, 118, 60, 60));
                }));
            case "tm":
                return Make(g => Tile(g, C(34, 211, 170), C(8, 120, 110), gg =>
                {
                    using var sc = RR(52, 60, 152, 128, 18); using (var b = new SolidBrush(C(10, 36, 40))) gg.FillPath(b, sc);
                    using var gl = new Pen(C(255, 255, 255, 36), 2); for (int i = 1; i < 4; i++) gg.DrawLine(gl, 60, 60 + i * 32, 196, 60 + i * 32);
                    using var pn = new Pen(C(110, 255, 210), 9) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }; gg.DrawLines(pn, new[] { new PointF(64, 160), new PointF(96, 128), new PointF(120, 146), new PointF(150, 92), new PointF(192, 112) });
                }));
            case "ai":
                return Make(g => Tile(g, C(168, 110, 255), C(70, 60, 220), gg =>
                {
                    using var bub = RR(48, 58, 160, 112, 30); using (var b = new SolidBrush(C(255, 255, 255))) gg.FillPath(b, bub);
                    using var tail = new GraphicsPath(); tail.AddPolygon(new[] { new PointF(84, 160), new PointF(84, 204), new PointF(124, 164) }); using (var b = new SolidBrush(C(255, 255, 255))) gg.FillPath(b, tail);
                    using var st = new GraphicsPath(); st.AddPolygon(new[] { new PointF(128, 74), new PointF(140, 110), new PointF(178, 114), new PointF(148, 134), new PointF(158, 168), new PointF(128, 148), new PointF(98, 168), new PointF(108, 134), new PointF(78, 114), new PointF(116, 110) }); Fill(gg, st, C(168, 110, 255), C(70, 60, 220), 90f);
                }));
            case "vault":
                return Make(g => Tile(g, C(130, 120, 255), C(60, 50, 190), gg =>
                {
                    using var sh = new Pen(C(235, 238, 255), 18) { StartCap = LineCap.Round, EndCap = LineCap.Round }; gg.DrawArc(sh, 86, 50, 84, 96, 180, 180); gg.DrawLine(sh, 86, 98, 86, 112); gg.DrawLine(sh, 170, 98, 170, 112);
                    using var bd = RR(62, 108, 132, 98, 20); Fill(gg, bd, C(255, 214, 102), C(247, 160, 20), 90f);
                    using (var b = new SolidBrush(C(120, 70, 10))) { gg.FillEllipse(b, 116, 138, 24, 24); gg.FillRectangle(b, 124, 150, 8, 28); }
                }));
            case "vm":
                return Make(g => Tile(g, C(110, 120, 255), C(52, 56, 190), gg =>
                {
                    using var mon = RR(44, 54, 168, 118, 14); using (var b = new SolidBrush(C(236, 240, 255))) gg.FillPath(b, mon);
                    using var scr = RR(54, 64, 148, 96, 8); Fill(gg, scr, C(24, 190, 240), C(80, 60, 220), 45f);
                    using (var b = new SolidBrush(C(255, 255, 255, 235))) { gg.FillEllipse(b, 108, 86, 40, 46); gg.FillEllipse(b, 118, 78, 20, 20); }
                    using (var b = new SolidBrush(C(255, 190, 40))) { gg.FillEllipse(b, 104, 126, 24, 12); gg.FillEllipse(b, 128, 126, 24, 12); }
                    using var st = new GraphicsPath(); st.AddPolygon(new[] { new PointF(112, 172), new PointF(144, 172), new PointF(152, 198), new PointF(104, 198) }); using (var b = new SolidBrush(C(210, 216, 240))) gg.FillPath(b, st);
                    using var bs = RR(80, 196, 96, 14, 7); using (var b = new SolidBrush(C(236, 240, 255))) gg.FillPath(b, bs);
                }));
            case "photo":
                return Make(g => Tile(g, C(255, 170, 70), C(236, 72, 130), gg =>
                {
                    using var fr = RR(48, 60, 160, 128, 16); using (var b = new SolidBrush(C(255, 255, 255))) gg.FillPath(b, fr);
                    using var im = RR(58, 70, 140, 108, 8); Fill(gg, im, C(120, 200, 255), C(190, 235, 255), 90f);
                    using (var b = new SolidBrush(C(255, 224, 90))) gg.FillEllipse(b, 148, 82, 30, 30);
                    using var m1 = new GraphicsPath(); m1.AddPolygon(new[] { new PointF(58, 168), new PointF(104, 104), new PointF(142, 168) }); using (var b = new SolidBrush(C(56, 160, 110))) gg.FillPath(b, m1);
                    using var m2 = new GraphicsPath(); m2.AddPolygon(new[] { new PointF(110, 168), new PointF(150, 122), new PointF(198, 168) }); using (var b = new SolidBrush(C(34, 130, 90))) gg.FillPath(b, m2);
                }));
            case "music":
                return Make(g => Tile(g, C(180, 120, 255), C(120, 50, 220), gg =>
                {
                    using var pn = new Pen(Color.White, 12) { StartCap = LineCap.Round, EndCap = LineCap.Round }; gg.DrawLine(pn, 160, 62, 160, 156); gg.DrawLine(pn, 160, 62, 100, 78); gg.DrawLine(pn, 100, 78, 100, 168);
                    using var b = new SolidBrush(Color.White); gg.FillEllipse(b, 72, 152, 44, 36); gg.FillEllipse(b, 132, 140, 44, 36);
                }));
            case "video":
                return Make(g => Tile(g, C(255, 110, 100), C(214, 40, 60), gg =>
                {
                    using var fr = RR(48, 68, 160, 112, 18); using (var b = new SolidBrush(C(255, 255, 255, 235))) gg.FillPath(b, fr);
                    using var tr = new GraphicsPath(); tr.AddPolygon(new[] { new PointF(110, 96), new PointF(110, 152), new PointF(160, 124) }); Fill(gg, tr, C(255, 110, 100), C(214, 40, 60), 90f);
                }));
            case "zip":
                return Make(g => Tile(g, C(255, 190, 70), C(220, 120, 10), gg =>
                {
                    using var bx = RR(58, 60, 140, 140, 16); Fill(gg, bx, C(255, 240, 200), C(240, 205, 140), 90f);
                    using var zp = new Pen(C(120, 80, 20), 8) { DashPattern = new[] { 1f, 1f } }; gg.DrawLine(zp, 128, 60, 128, 150);
                    using var tab = RR(110, 142, 36, 44, 10); using (var b = new SolidBrush(C(214, 120, 12))) gg.FillPath(b, tab);
                    using var hole = new SolidBrush(C(255, 240, 200)); gg.FillEllipse(hole, 120, 158, 16, 16);
                }));
            case "code":
                return Make(g => Tile(g, C(40, 200, 180), C(10, 110, 130), gg =>
                {
                    using var pn = new Pen(Color.White, 14) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                    gg.DrawLines(pn, new[] { new PointF(100, 84), new PointF(62, 128), new PointF(100, 172) }); gg.DrawLines(pn, new[] { new PointF(156, 84), new PointF(194, 128), new PointF(156, 172) }); using var sl = new Pen(C(255, 255, 255, 190), 12) { StartCap = LineCap.Round, EndCap = LineCap.Round }; gg.DrawLine(sl, 138, 74, 118, 182);
                }));
            case "exe":
                return Make(g => Tile(g, C(120, 140, 170), C(56, 70, 100), gg =>
                {
                    using var w = RR(52, 62, 152, 130, 14); using (var b = new SolidBrush(Color.White)) gg.FillPath(b, w);
                    using var tb = new GraphicsPath(); tb.AddArc(52, 62, 28, 28, 180, 90); tb.AddLine(66, 62, 190, 62); tb.AddArc(176, 62, 28, 28, 270, 90); tb.AddLine(204, 88, 52, 88); tb.CloseFigure(); using (var b = new SolidBrush(C(70, 130, 240))) gg.FillPath(b, tb);
                    using var pn = new Pen(C(150, 165, 195), 8) { StartCap = LineCap.Round, EndCap = LineCap.Round }; gg.DrawLine(pn, 76, 114, 170, 114); gg.DrawLine(pn, 76, 140, 140, 140);
                }));
            case "web":
                return App("browser");
            case "user":
                return Make(g => Tile(g, C(120, 130, 255), C(70, 60, 200), gg =>
                {
                    using (var b = new SolidBrush(Color.White)) { gg.FillEllipse(b, 98, 56, 60, 60); using var body = new GraphicsPath(); body.AddArc(66, 126, 124, 120, 180, 180); body.CloseFigure(); gg.FillPath(b, body); }
                }));
        }
        return Make(g => Tile(g, C(120, 130, 150), C(70, 80, 100), gg => Txt(gg, "?", 110, Color.White, new RectangleF(22, 18, 212, 212))));
    }
}

// Crisp vector glyphs for buttons and toolbars.
static class Glyphs
{
    public static void Draw(Graphics g, string k, RectangleF r, Color c, float w)
    {
        var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }; using var br = new SolidBrush(c);
        float x = r.X, y = r.Y, W = r.Width, H = r.Height, cx = x + W / 2, cy = y + H / 2;
        switch (k)
        {
            case "close": g.DrawLine(pen, x, y, x + W, y + H); g.DrawLine(pen, x + W, y, x, y + H); break;
            case "min": g.DrawLine(pen, x, cy, x + W, cy); break;
            case "max": g.DrawRectangle(pen, x, y, W, H); break;
            case "restore": g.DrawRectangle(pen, x + W * 0.18f, y, W * 0.82f, H * 0.82f); g.DrawRectangle(pen, x, y + H * 0.18f, W * 0.82f, H * 0.82f); break;
            case "back": g.DrawLine(pen, x + W, cy, x, cy); g.DrawLines(pen, new[] { new PointF(x + W * 0.45f, y), new PointF(x, cy), new PointF(x + W * 0.45f, y + H) }); break;
            case "fwd": g.DrawLine(pen, x, cy, x + W, cy); g.DrawLines(pen, new[] { new PointF(x + W * 0.55f, y), new PointF(x + W, cy), new PointF(x + W * 0.55f, y + H) }); break;
            case "up": g.DrawLine(pen, cx, y + H, cx, y); g.DrawLines(pen, new[] { new PointF(x, y + H * 0.45f), new PointF(cx, y), new PointF(x + W, y + H * 0.45f) }); break;
            case "reload": g.DrawArc(pen, x, y, W, H, -40, 280); g.DrawLines(pen, new[] { new PointF(x + W * 0.95f, y - H * 0.02f), new PointF(x + W * 0.88f, y + H * 0.30f), new PointF(x + W * 0.58f, y + H * 0.22f) }); break;
            case "home": g.DrawLines(pen, new[] { new PointF(x, cy), new PointF(cx, y), new PointF(x + W, cy) }); g.DrawLines(pen, new[] { new PointF(x + W * 0.16f, cy), new PointF(x + W * 0.16f, y + H), new PointF(x + W * 0.84f, y + H), new PointF(x + W * 0.84f, cy) }); break;
            case "menu": foreach (var dy in new[] { 0.1f, 0.5f, 0.9f }) g.FillEllipse(br, cx - w * 0.9f, y + H * dy - w * 0.9f, w * 1.8f, w * 1.8f); break;
            case "plus": g.DrawLine(pen, cx, y, cx, y + H); g.DrawLine(pen, x, cy, x + W, cy); break;
            case "search": g.DrawEllipse(pen, x, y, W * 0.72f, H * 0.72f); g.DrawLine(pen, x + W * 0.64f, y + H * 0.64f, x + W, y + H); break;
            case "power": g.DrawArc(pen, x, y + H * 0.08f, W, H * 0.92f, -60, 300); g.DrawLine(pen, cx, y, cx, cy); break;
            case "start": { float s = W * 0.46f, gp = W * 0.08f; g.FillRectangle(br, x, y, s, s); g.FillRectangle(br, x + s + gp * 2, y, s, s); g.FillRectangle(br, x, y + s + gp * 2, s, s); g.FillRectangle(br, x + s + gp * 2, y + s + gp * 2, s, s); break; }
            case "star": case "starfill": { var pts = new PointF[10]; for (int i = 0; i < 10; i++) { double a = -Math.PI / 2 + i * Math.PI / 5; float rr = i % 2 == 0 ? W / 2 : W * 0.22f; pts[i] = new PointF(cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr); } if (k == "starfill") g.FillPolygon(br, pts); else g.DrawPolygon(pen, pts); break; }
            case "lock": g.DrawArc(pen, x + W * 0.22f, y, W * 0.56f, H * 0.7f, 180, 180); g.FillRectangle(br, x + W * 0.1f, y + H * 0.42f, W * 0.8f, H * 0.58f); break;
            case "camera": g.DrawRectangle(pen, x, y + H * 0.2f, W, H * 0.72f); g.DrawEllipse(pen, cx - W * 0.2f, cy - H * 0.04f + 0, W * 0.4f, H * 0.4f); g.DrawLine(pen, x + W * 0.3f, y + H * 0.2f, x + W * 0.4f, y + H * 0.04f); g.DrawLine(pen, x + W * 0.4f, y + H * 0.04f, x + W * 0.6f, y + H * 0.04f); g.DrawLine(pen, x + W * 0.6f, y + H * 0.04f, x + W * 0.7f, y + H * 0.2f); break;
            case "check": g.DrawLines(pen, new[] { new PointF(x, cy), new PointF(x + W * 0.38f, y + H * 0.85f), new PointF(x + W, y + H * 0.1f) }); break;
            case "chev": g.DrawLines(pen, new[] { new PointF(x + W * 0.25f, y), new PointF(x + W * 0.8f, cy), new PointF(x + W * 0.25f, y + H) }); break;
            case "down": g.DrawLine(pen, cx, y, cx, y + H * 0.8f); g.DrawLines(pen, new[] { new PointF(x + W * 0.2f, y + H * 0.5f), new PointF(cx, y + H * 0.8f), new PointF(x + W * 0.8f, y + H * 0.5f) }); g.DrawLine(pen, x + W * 0.1f, y + H, x + W * 0.9f, y + H); break;
            case "lockscreen": g.DrawRectangle(pen, x, y + H * 0.4f, W, H * 0.6f); g.DrawArc(pen, x + W * 0.2f, y, W * 0.6f, H * 0.8f, 180, 180); break;
            case "folderplus": g.DrawRectangle(pen, x, y + H * 0.2f, W, H * 0.8f); g.DrawLine(pen, cx, cy + H * 0.1f, cx, y + H * 0.9f); g.DrawLine(pen, x + W * 0.25f, y + H * 0.6f, x + W * 0.75f, y + H * 0.6f); break;
            case "wifi": for (int i = 1; i <= 3; i++) { float s = W * i / 3f; g.DrawArc(pen, cx - s / 2, y + H - s * 0.9f, s, s, 225, 90); } g.FillEllipse(br, cx - w * 0.8f, y + H - w * 1.6f, w * 1.6f, w * 1.6f); break;
            case "speaker": g.FillPolygon(br, new[] { new PointF(x, y + H * 0.35f), new PointF(x + W * 0.3f, y + H * 0.35f), new PointF(x + W * 0.6f, y + H * 0.08f), new PointF(x + W * 0.6f, y + H * 0.92f), new PointF(x + W * 0.3f, y + H * 0.65f), new PointF(x, y + H * 0.65f) }); g.DrawArc(pen, x + W * 0.55f, y + H * 0.25f, W * 0.45f, H * 0.5f, -50, 100); break;
            case "bell": g.DrawArc(pen, x + W * 0.15f, y, W * 0.7f, H * 0.8f, 180, 180); g.DrawLine(pen, x + W * 0.15f, y + H * 0.4f, x, y + H * 0.8f); g.DrawLine(pen, x + W * 0.85f, y + H * 0.4f, x + W, y + H * 0.8f); g.DrawLine(pen, x, y + H * 0.8f, x + W, y + H * 0.8f); break;
            case "battery": g.DrawRectangle(pen, x, y + H * 0.2f, W * 0.88f, H * 0.6f); g.FillRectangle(br, x + W * 0.9f, y + H * 0.38f, W * 0.1f, H * 0.24f); break;
            case "usb": g.DrawLine(pen, x, cy, x + W, cy); g.FillEllipse(br, x - w, cy - w, w * 2, w * 2); g.FillRectangle(br, x + W - w, cy - w * 2, w * 2, w * 4); break;
        }
        g.SmoothingMode = sm;
    }
}

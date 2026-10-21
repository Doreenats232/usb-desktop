using System.Drawing.Drawing2D;
namespace UsbDesktop;

// The walking buddy: a small animated character that wanders the desktop when the assistant window is closed.
// Click it for a small chat bar at its feet; replies appear in a white speech bubble and are spoken out loud.
class PetSprite : Control
{
    public bool Right = true, Moving, Talking; float phase; DateTime nextBlink = DateTime.Now.AddSeconds(2), blinkEnd; static readonly Random R = new();
    public PetSprite() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); BackColor = Color.Transparent; Cursor = Cursors.Hand; Visible = false; }
    public void Step(float dt)
    {
        if (Moving) phase += dt * 9f; var now = DateTime.Now; if (now >= nextBlink) { blinkEnd = now.AddMilliseconds(140); nextBlink = now.AddSeconds(2 + R.NextDouble() * 3); }
    }
    static void Limb(Graphics g, float x, float y, float len, float deg, Color c, float w, Color? tip = null, float tr = 0)
    {
        float r = deg * MathF.PI / 180f, ex = x + MathF.Sin(r) * len, ey = y + MathF.Cos(r) * len;
        using var p = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round }; g.DrawLine(p, x, y, ex, ey);
        if (tip != null) { using var b = new SolidBrush(tip.Value); g.FillEllipse(b, ex - tr, ey - tr * 0.7f, tr * 2.3f, tr * 1.4f); }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; float s = Math.Min(Width / 110f, Height / 195f); float t = Environment.TickCount / 1000f;
        g.TranslateTransform(Width / 2f, Height - 8 * s); if (!Right) g.ScaleTransform(-1, 1); g.ScaleTransform(s, s);
        float sw = Moving ? MathF.Sin(phase) : 0f, bob = Moving ? -MathF.Abs(MathF.Cos(phase)) * 4f : MathF.Sin(t * 2f) * -1.5f;
        var jeans = Color.FromArgb(50, 62, 112); var skin = Color.FromArgb(224, 172, 130); var hair = Color.FromArgb(40, 30, 32);
        using (var b = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(b, -32, -7, 64, 14);
        g.TranslateTransform(0, bob);
        Limb(g, 8, -62, 54, -sw * 30, jeans, 16, Color.FromArgb(235, 235, 240), 9);       // far leg
        Limb(g, -8, -62, 54, sw * 30, jeans, 16, Color.FromArgb(245, 245, 250), 9);       // near leg
        var body = new Rectangle(-25, -124, 50, 66);
        using (var path = U.Round(body, 16)) using (var lg = new LinearGradientBrush(body, Theme.Accent, Ui.Accent2, 80f)) g.FillPath(lg, path);
        using (var pk = new Pen(Color.FromArgb(70, 255, 255, 255), 2)) g.DrawLine(pk, -12, -80, 12, -80);
        float armSwing = Talking ? 42f + MathF.Sin(t * 7f) * 16f : -sw * 32f;
        Limb(g, 25, -112, 44, Talking ? -armSwing : sw * 32f, Theme.Accent, 12, skin, 6);   // far arm
        Limb(g, -25, -112, 44, Talking ? armSwing : armSwing, Theme.Accent, 12, skin, 6);  // near arm
        // head
        using (var b = new SolidBrush(skin)) g.FillEllipse(b, -26, -176, 52, 52);
        using (var b = new SolidBrush(hair)) g.FillPie(b, -27, -178, 54, 54, 180, 180);
        using (var pn = new Pen(Theme.Accent, 6)) g.DrawArc(pn, -31, -182, 62, 62, 180, 180);
        using (var b = new SolidBrush(Theme.Accent)) { g.FillEllipse(b, -35, -158, 11, 22); g.FillEllipse(b, 24, -158, 11, 22); }
        bool blink = DateTime.Now < blinkEnd;
        using (var b = new SolidBrush(Color.FromArgb(30, 24, 28))) using (var pn = new Pen(Color.FromArgb(30, 24, 28), 2))
        {
            if (blink) { g.DrawLine(pn, -8, -148, -1, -148); g.DrawLine(pn, 8, -148, 15, -148); }
            else { g.FillEllipse(b, -8, -153, 7, 10); g.FillEllipse(b, 8, -153, 7, 10); }
            if (Talking) { float h = 4 + MathF.Abs(MathF.Sin(t * 14f)) * 8f; g.FillEllipse(b, 0, -138, 12, h); }
            else g.DrawArc(pn, -1, -146, 16, 12, 20, 140);
        }
    }
}

class PetBubble : Control
{
    string text = ""; int tail; readonly Font f = Ui.F(10);
    public PetBubble() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); BackColor = Color.White; Visible = false; }
    public void SetText(string t)
    {
        if (t.Length > 260) t = t[..260] + "…"; text = t; int pad = Ui.D(12); tail = Ui.D(10);
        var sz = TextRenderer.MeasureText(t, f, new Size(Ui.D(240), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        Size = new Size(Math.Max(Ui.D(46), sz.Width + pad * 2 + 2), sz.Height + pad * 2 + tail);
        using var rp = U.Round(new Rectangle(0, 0, Width, Height - tail), Ui.D(14)); var rg = new Region(rp);
        using var tp = new GraphicsPath(); tp.AddPolygon(new[] { new Point(Width / 2 - Ui.D(8), Height - tail - 1), new Point(Width / 2, Height), new Point(Width / 2 + Ui.D(8), Height - tail - 1) }); rg.Union(tp);
        Region = rg; Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.White);
        TextRenderer.DrawText(e.Graphics, text, f, new Rectangle(Ui.D(12), Ui.D(12), Math.Max(10, Width - Ui.D(24)), Math.Max(10, Height - tail - Ui.D(24))), Color.FromArgb(30, 30, 44), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
    }
}

static class Pet
{
    static DesktopForm? sh; static Panel? desk; static PetSprite? sp; static PetBubble? bu; static Control? bar; static TextBox? tb;
    static readonly Random rnd = new(); static System.Windows.Forms.Timer? tm;
    static DateTime last = DateTime.Now, idleUntil = DateTime.Now.AddSeconds(2), pausedUntil, hideBubbleAt = DateTime.MaxValue, barUntil, nextFix;
    static PointF pos; static PointF? target; static bool busy, speaking, dragging, down; static int speechId; static Point dn, startLoc;

    public static void Attach(DesktopForm s, Panel d)
    {
        if (sp != null) return; sh = s; desk = d;
        sp = new PetSprite { Width = Ui.D(110), Height = Ui.D(170) }; bu = new PetBubble();
        var (box, t) = Ui.Input("Talk to " + Cfg.Get("ai_name", "Morales") + "…", false, 38); bar = box; tb = t; bar.Width = Ui.D(260); bar.Height = Ui.D(38); bar.Visible = false;
        d.Controls.Add(bar); d.Controls.Add(bu); d.Controls.Add(sp);
        pos = new PointF(Ui.D(320), Math.Max(Ui.D(60), d.Height - sp.Height - Ui.D(140))); sp.Location = Point.Round(pos);
        var menu = new ContextMenuStrip(); menu.Items.Add("Talk to me", null, (_, _) => ToggleBar()); menu.Items.Add("Hide the character", null, (_, _) => Cfg.Set("pet", "0")); sp.ContextMenuStrip = menu;
        sp.MouseDown += (_, e) => { if (e.Button != MouseButtons.Left) return; down = true; dragging = false; dn = Cursor.Position; startLoc = sp.Location; };
        sp.MouseMove += (_, e) =>
        {
            if (!down) return; var c = Cursor.Position; int dx = c.X - dn.X, dy = c.Y - dn.Y;
            if (!dragging && Math.Abs(dx) + Math.Abs(dy) > 6) { dragging = true; target = null; }
            if (dragging) pos = Clamp(new PointF(startLoc.X + dx, startLoc.Y + dy));
        };
        sp.MouseUp += (_, e) => { if (e.Button != MouseButtons.Left) return; bool was = dragging; down = false; dragging = false; pausedUntil = DateTime.Now.AddSeconds(5); if (!was) ToggleBar(); };
        tb.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Ask(tb.Text); e.SuppressKeyPress = true; } else if (e.KeyCode == Keys.Escape) { bar!.Visible = false; e.SuppressKeyPress = true; } };
        tb.TextChanged += (_, _) => barUntil = DateTime.Now.AddSeconds(15);
        Assistant.Replied += (text, fromPet) => { if (!fromPet && sp != null && sp.Visible) ShowBubble(text, Math.Max(5, text.Length * 0.06)); };
        tm = new System.Windows.Forms.Timer { Interval = 40 }; tm.Tick += (_, _) => Tick(); tm.Start();
    }

    public static void Command(string a)
    {
        a = a.Trim().ToLowerInvariant();
        if (a == "off") Cfg.Set("pet", "0");
        else { Cfg.Set("pet", "1"); if (a == "come" && desk != null && sp != null) { pausedUntil = default; target = Clamp(new PointF(desk.Width / 2f - sp.Width / 2f, desk.Height / 2f)); } }
    }

    static PointF Clamp(PointF p) => desk == null || sp == null ? p : new PointF(Math.Clamp(p.X, 0, Math.Max(0, desk.Width - sp.Width)), Math.Clamp(p.Y, Ui.D(20), Math.Max(Ui.D(20), desk.Height - sp.Height - Ui.D(54))));

    static void Tick()
    {
        if (sh == null || desk == null || sp == null || bu == null || bar == null || tb == null) return;
        var now = DateTime.Now; float dt = Math.Min(0.2f, (float)(now - last).TotalSeconds); last = now;
        bool show = Cfg.Get("pet", "1") == "1" && desk.Visible && !sh.AssistantOpen();
        if (!show) { if (sp.Visible) { sp.Visible = false; bu.Visible = false; bar.Visible = false; } return; }
        if (!sp.Visible) { sp.Visible = true; pos = Clamp(pos); }
        if (now >= nextFix) { nextFix = now.AddSeconds(1.5); Fix(); }
        bool still = dragging || speaking || busy || bar.Visible || now < pausedUntil;
        if (still) { sp.Moving = false; if (!dragging) target = null; }
        else if (target == null) { sp.Moving = false; if (now >= idleUntil) { var a = rnd.NextDouble() * Math.PI * 2; float r = Ui.D(150) + (float)rnd.NextDouble() * Ui.D(300); target = Clamp(new PointF(pos.X + MathF.Cos((float)a) * r, pos.Y + MathF.Sin((float)a) * r * 0.6f)); } }
        else
        {
            float dx = target.Value.X - pos.X, dy = target.Value.Y - pos.Y, dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < 4) { target = null; idleUntil = now.AddSeconds(2.5 + rnd.NextDouble() * 5); sp.Moving = false; }
            else { float st = Math.Min(dist, Ui.D(70) * dt); pos = new PointF(pos.X + dx / dist * st, pos.Y + dy / dist * st); sp.Moving = true; if (Math.Abs(dx) > 2) sp.Right = dx > 0; }
        }
        sp.Location = Point.Round(Clamp(pos)); sp.Step(dt); sp.Invalidate(); Place();
        if (bu.Visible && now >= hideBubbleAt && !speaking && !busy) bu.Visible = false;
        if (bar.Visible && !busy && now > barUntil && tb.Text == "") bar.Visible = false;
    }

    // keep the character above the icons but below every window
    static void Fix()
    {
        if (desk == null || sp == null) return; var c = desk.Controls;
        int maxWin = c.OfType<Win>().Select(w => c.GetChildIndex(w)).DefaultIfEmpty(-1).Max(); int minIcon = c.OfType<DeskIcon>().Select(i => c.GetChildIndex(i)).DefaultIfEmpty(int.MaxValue).Min();
        int idx = c.GetChildIndex(sp); if (idx < maxWin || idx > minIcon) c.SetChildIndex(sp, Math.Min(maxWin + 1, c.Count - 1));
    }

    static void Place()
    {
        if (desk == null || sp == null || bu == null || bar == null) return;
        if (bar.Visible) bar.Location = new Point(Math.Clamp(sp.Left + (sp.Width - bar.Width) / 2, Ui.D(4), Math.Max(Ui.D(4), desk.Width - bar.Width - Ui.D(4))), sp.Bottom + Ui.D(2));
        if (bu.Visible) bu.Location = new Point(Math.Clamp(sp.Left + sp.Width / 2 - bu.Width / 2, Ui.D(4), Math.Max(Ui.D(4), desk.Width - bu.Width - Ui.D(4))), Math.Max(Ui.D(4), sp.Top - bu.Height + Ui.D(8)));
    }

    static void ToggleBar()
    {
        if (bar == null || tb == null) return; if (bar.Visible) { bar.Visible = false; return; }
        bar.Visible = true; Place(); bar.BringToFront(); tb.Focus(); barUntil = DateTime.Now.AddSeconds(15);
    }

    static void ShowBubble(string t, double secs)
    {
        if (bu == null) return; bu.SetText(t); bu.Visible = true; Place(); bu.BringToFront(); hideBubbleAt = DateTime.Now.AddSeconds(secs);
    }

    static async void Ask(string text)
    {
        text = text.Trim(); if (text == "" || busy || sh == null || tb == null) return;
        busy = true; tb.Text = ""; ShowBubble("…", 120);
        try { var r = await Assistant.Converse(sh, text, true); Speak(r); }
        catch (Exception ex) { ShowBubble("Hmm: " + ex.Message, 7); }
        finally { busy = false; barUntil = DateTime.Now.AddSeconds(12); }
    }

    static void Speak(string text)
    {
        ShowBubble(text, Math.Max(5, text.Length * 0.06)); if (Cfg.Get("pet_voice", "1") != "1" || sp == null) return;
        int id = ++speechId; speaking = true; sp.Talking = true;
        Assistant.Say(text, () => { try { desk!.BeginInvoke(new Action(() => { if (id != speechId) return; speaking = false; if (sp != null) sp.Talking = false; hideBubbleAt = DateTime.Now.AddSeconds(2); })); } catch { } });
    }
}

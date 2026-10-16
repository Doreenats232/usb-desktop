using System.Drawing.Drawing2D;
namespace UsbDesktop;

// Small friendly prompts: pick one of a few options, or type a password.
class AskForm : Form
{
    public int Result = -1; public string Typed = "";
    public AskForm(string title, string text, string[] opts, bool password)
    {
        FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen; TopMost = true; ShowInTaskbar = false; BackColor = Ui.Surface; DoubleBuffered = true;
        int w = Ui.D(500), pad = Ui.D(28), iw = w - pad * 2;
        var th = new Label { Text = title, Font = Ui.F(15, true), ForeColor = Ui.Text, BackColor = Color.Transparent, Bounds = new Rectangle(pad, pad, iw, Ui.D(34)), AutoEllipsis = true };
        int h2 = TextRenderer.MeasureText(text, Ui.F(10.5f), new Size(iw, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + Ui.D(6);
        var tl = new Label { Text = text, Font = Ui.F(10.5f), ForeColor = Ui.Sub, BackColor = Color.Transparent, Bounds = new Rectangle(pad, th.Bottom + Ui.D(6), iw, h2) };
        Controls.Add(th); Controls.Add(tl); int y = tl.Bottom + Ui.D(18);
        if (password)
        {
            var (box, tb) = Ui.Input("Password", true, 46); box.Bounds = new Rectangle(pad, y, iw, Ui.D(46)); Controls.Add(box); y += Ui.D(46) + Ui.D(14);
            var ok = new RoundButton { Text = "Continue", Pill = true, BackColor = Theme.Accent, ForeColor = Color.White, Bounds = new Rectangle(pad, y, iw, Ui.D(46)) };
            ok.Click += (_, _) => { if (tb.Text.Length >= 1) { Typed = tb.Text; Result = 0; Close(); } }; tb.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { ok.PerformClick(); e.SuppressKeyPress = true; } };
            Controls.Add(ok); y += Ui.D(46) + Ui.D(10); Shown += (_, _) => tb.Focus();
        }
        else
            for (int i = 0; i < opts.Length; i++)
            {
                int k = i; var b = new RoundButton { Text = opts[i], Pill = true, BackColor = i == 0 ? Theme.Accent : Color.FromArgb(52, 55, 86), ForeColor = Color.White, Bounds = new Rectangle(pad, y, iw, Ui.D(46)), Font = Ui.F(10.5f) };
                b.Click += (_, _) => { Result = k; Close(); }; Controls.Add(b); y += Ui.D(46) + Ui.D(10);
            }
        var cancel = new RoundButton { Text = "Cancel", Pill = true, BackColor = Color.FromArgb(0, 0, 0, 0), ForeColor = Ui.Sub, Hover = Color.FromArgb(44, 47, 76), Bounds = new Rectangle(pad, y, iw, Ui.D(38)) };
        cancel.Click += (_, _) => { Result = -1; Close(); }; Controls.Add(cancel); y += Ui.D(38) + pad;
        ClientSize = new Size(w, y);
        Resize += (_, _) => { using var p = U.Round(new Rectangle(0, 0, Width, Height), Ui.D(20)); var old = Region; Region = new Region(p); old?.Dispose(); };
        Paint += (_, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using var lg = new LinearGradientBrush(new Rectangle(0, 0, Width, Ui.D(4)), Theme.Accent, Ui.Accent2, 0f); e.Graphics.FillRectangle(lg, 0, 0, Width, Ui.D(4)); using var pn = new Pen(Ui.Line); e.Graphics.DrawRectangle(pn, 0, 0, Width - 1, Height - 1); };
    }
}

static class Ask
{
    public static int Choose(string title, string text, params string[] opts) { using var f = new AskForm(title, text, opts, false); f.ShowDialog(); return f.Result; }
    public static string? Password(string title, string text) { using var f = new AskForm(title, text, Array.Empty<string>(), true); f.ShowDialog(); return f.Result == 0 ? f.Typed : null; }
}

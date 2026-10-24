namespace UsbDesktop;

// Two tiny built-in games: Snake and Tic-Tac-Toe against your buddy.
static class MiniGames
{
    public static string Open(string which)
    {
        which = which.Trim().ToLowerInvariant();
        if (which.Contains("snake")) { new SnakeForm().Show(); return "Opened Snake. Arrow keys or WASD, P pauses, Space restarts."; }
        if (which.Contains("tic") || which.Contains("xo") || which.Contains("noughts")) { new TicForm().Show(); return "Opened Tic-Tac-Toe. You are X."; }
        return "I have two games: snake and tictactoe.";
    }

    class SnakeForm : Form
    {
        const int N = 20, C = 22; readonly System.Windows.Forms.Timer tm = new() { Interval = 110 }; List<Point> body = new(); Point dir, food; int score, best; bool over, paused; readonly Random rnd = new();
        public SnakeForm()
        {
            Text = "Snake"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen; DoubleBuffered = true; BackColor = Ui.Surface; KeyPreview = true;
            ClientSize = new Size(N * C, N * C + 34); Reset(); tm.Tick += (_, _) => Step(); tm.Start(); KeyDown += OnKey; FormClosed += (_, _) => tm.Dispose();
        }
        void Reset() { body = new List<Point> { new(10, 10), new(9, 10), new(8, 10) }; dir = new Point(1, 0); score = 0; over = false; paused = false; Food(); Invalidate(); }
        void Food() { do food = new Point(rnd.Next(N), rnd.Next(N)); while (body.Contains(food)); }
        void Step()
        {
            if (over || paused) return;
            var h = new Point(body[0].X + dir.X, body[0].Y + dir.Y);
            if (h.X < 0 || h.Y < 0 || h.X >= N || h.Y >= N || body.Contains(h)) { over = true; best = Math.Max(best, score); Invalidate(); return; }
            body.Insert(0, h); if (h == food) { score++; Food(); if (tm.Interval > 55) tm.Interval -= 2; } else body.RemoveAt(body.Count - 1);
            Invalidate();
        }
        void OnKey(object? s, KeyEventArgs e)
        {
            Point d = e.KeyCode switch { Keys.Up or Keys.W => new Point(0, -1), Keys.Down or Keys.S => new Point(0, 1), Keys.Left or Keys.A => new Point(-1, 0), Keys.Right or Keys.D => new Point(1, 0), _ => Point.Empty };
            if (d != Point.Empty && !(d.X == -dir.X && d.Y == -dir.Y)) dir = d;
            if (e.KeyCode == Keys.Space && over) { tm.Interval = 110; Reset(); }
            if (e.KeyCode == Keys.P) paused = !paused;
            e.Handled = true;
        }
        protected override bool IsInputKey(Keys k) => k is Keys.Up or Keys.Down or Keys.Left or Keys.Right || base.IsInputKey(k);
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Color.FromArgb(18, 20, 36))) g.FillRectangle(b, 0, 0, N * C, N * C);
            using (var gp = new Pen(Color.FromArgb(26, 255, 255, 255))) for (int i = 1; i < N; i++) { g.DrawLine(gp, i * C, 0, i * C, N * C); g.DrawLine(gp, 0, i * C, N * C, i * C); }
            using (var fb = new SolidBrush(Color.FromArgb(240, 80, 100))) g.FillEllipse(fb, food.X * C + 3, food.Y * C + 3, C - 6, C - 6);
            for (int i = body.Count - 1; i >= 0; i--) using (var sb = new SolidBrush(i == 0 ? Color.FromArgb(110, 231, 160) : Color.FromArgb(40 + (i * 3) % 40, 170, 120))) g.FillRectangle(sb, body[i].X * C + 1, body[i].Y * C + 1, C - 2, C - 2);
            using var tf = new Font("Segoe UI", 10f, FontStyle.Bold); using var tb = new SolidBrush(Ui.Text);
            g.DrawString("Score " + score + "    Best " + best + (paused ? "    PAUSED" : ""), tf, tb, 8, N * C + 8);
            if (over) { using var ob = new SolidBrush(Color.FromArgb(170, 0, 0, 0)); g.FillRectangle(ob, 0, 0, N * C, N * C); using var big = new Font("Segoe UI", 20f, FontStyle.Bold); var m = "Game over\nSpace to play again"; var sz = g.MeasureString(m, big); g.DrawString(m, big, Brushes.White, (N * C - sz.Width) / 2, (N * C - sz.Height) / 2); }
        }
    }

    class TicForm : Form
    {
        readonly Button[] cell = new Button[9]; readonly char[] b = new char[9]; readonly Label info = new(); bool over;
        static readonly int[][] Lines = { new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 }, new[] { 0, 4, 8 }, new[] { 2, 4, 6 } };
        public TicForm()
        {
            Text = "Tic-Tac-Toe"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen; BackColor = Ui.Surface; ClientSize = new Size(Ui.D(330), Ui.D(400));
            for (int i = 0; i < 9; i++)
            {
                int ix = i; var c = new Button { Left = Ui.D(15) + i % 3 * Ui.D(100), Top = Ui.D(15) + i / 3 * Ui.D(100), Width = Ui.D(96), Height = Ui.D(96), Font = new Font("Segoe UI", 28f, FontStyle.Bold), BackColor = Ui.Surface2, ForeColor = Ui.Text, FlatStyle = FlatStyle.Flat };
                c.Click += (_, _) => Play(ix); cell[i] = c; Controls.Add(c);
            }
            info.SetBounds(Ui.D(15), Ui.D(325), Ui.D(300), Ui.D(26)); info.ForeColor = Ui.Text; info.Font = new Font("Segoe UI", 11f, FontStyle.Bold); info.Text = "Your move (X)"; Controls.Add(info);
            var again = new Button { Text = "New game", Left = Ui.D(15), Top = Ui.D(355), Width = Ui.D(140), Height = Ui.D(32) }; again.Click += (_, _) => Reset(); Controls.Add(again);
        }
        void Reset() { for (int i = 0; i < 9; i++) { b[i] = '\0'; cell[i].Text = ""; cell[i].Enabled = true; } over = false; info.Text = "Your move (X)"; }
        bool Won(char m) => Lines.Any(l => l.All(i => b[i] == m));
        void Put(int i, char m) { b[i] = m; cell[i].Text = m.ToString(); cell[i].ForeColor = m == 'X' ? Color.FromArgb(110, 231, 160) : Color.FromArgb(240, 120, 130); }
        bool End() { if (Won('X')) { info.Text = "You win! 🎉"; over = true; } else if (Won('O')) { info.Text = "Your buddy wins 😄"; over = true; } else if (b.All(x => x != '\0')) { info.Text = "Draw."; over = true; } return over; }
        void Play(int i)
        {
            if (over || b[i] != '\0') return; Put(i, 'X'); if (End()) return;
            int pick = -1;
            foreach (var m in new[] { 'O', 'X' }) { for (int k = 0; k < 9 && pick < 0; k++) if (b[k] == '\0') { b[k] = m; bool w = Won(m); b[k] = '\0'; if (w) pick = k; } if (pick >= 0) break; }
            if (pick < 0) foreach (var k in new[] { 4, 0, 2, 6, 8, 1, 3, 5, 7 }) if (b[k] == '\0') { pick = k; break; }
            if (pick >= 0) Put(pick, 'O'); if (!End()) info.Text = "Your move (X)";
        }
    }
}

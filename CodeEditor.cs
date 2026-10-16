using System.Text.RegularExpressions;
namespace UsbDesktop;

// Built-in code editor: folder tree, tabs, syntax colours, line numbers, find, Ctrl+S.
static class CodeEditor
{
    static readonly Regex Kw = new(@"\b(abstract|as|async|await|bool|break|case|catch|class|const|continue|def|default|del|do|elif|else|enum|export|extends|false|finally|for|foreach|from|function|if|implements|import|in|int|interface|is|lambda|let|new|none|null|override|pass|private|protected|public|raise|readonly|return|static|string|struct|super|switch|this|throw|true|try|typeof|using|var|void|while|with|yield)\b", RegexOptions.Compiled);
    static readonly Regex Str = new("\"(?:\\\\.|[^\"\\\\\\n])*\"|'(?:\\\\.|[^'\\\\\\n])*'", RegexOptions.Compiled);
    static readonly Regex Num = new(@"\b\d+(\.\d+)?\b", RegexOptions.Compiled);
    static readonly Regex Cmt = new(@"//[^\n]*|#[^\n]*|/\*[\s\S]*?\*/|<!--[\s\S]*?-->", RegexOptions.Compiled);

    class Doc { public string? Path; public RichTextBox Box = null!; public bool Dirty; public string Title => (Path == null ? "Untitled" : System.IO.Path.GetFileName(Path)) + (Dirty ? " •" : ""); }

    class Gutter : Control
    {
        public RichTextBox? Box;
        public Gutter() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true); Width = Ui.D(46); Dock = DockStyle.Left; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Ui.Surface2); if (Box == null) return;
            using var br = new SolidBrush(Ui.Sub); using var f = new Font("Consolas", Box.Font.Size);
            int first = Box.GetCharIndexFromPosition(new Point(0, 0)); int line = Box.GetLineFromCharIndex(first);
            for (int i = line; i < Box.Lines.Length + 1; i++)
            {
                int idx = Box.GetFirstCharIndexFromLine(i); if (idx < 0) break;
                var pt = Box.GetPositionFromCharIndex(idx); if (pt.Y > Height) break;
                var s = (i + 1).ToString(); var sz = e.Graphics.MeasureString(s, f);
                e.Graphics.DrawString(s, f, br, Width - sz.Width - Ui.D(6), pt.Y);
            }
        }
    }

    public static Control View(DesktopForm sh, string? startPath)
    {
        var root = new Panel(); Ui.Own(root); root.BackColor = Ui.Surface;
        var ms = new MenuStrip { Dock = DockStyle.Top, Renderer = new DarkRenderer(), BackColor = Ui.Surface2, ForeColor = Ui.Text };
        var tree = new TreeView { Dock = DockStyle.Left, Width = Ui.D(200), BackColor = Ui.Surface, ForeColor = Ui.Text, BorderStyle = BorderStyle.None, Font = Ui.F(9.5f), ShowLines = false };
        var tabs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Ui.D(34), BackColor = Ui.Surface2, WrapContents = false, AutoScroll = false };
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Surface };
        var gutter = new Gutter();
        var status = new Label { Dock = DockStyle.Bottom, Height = Ui.D(24), ForeColor = Ui.Sub, BackColor = Ui.Surface2, Font = Ui.F(9), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(Ui.D(8), 0, 0, 0) };
        var find = new Panel { Dock = DockStyle.Top, Height = Ui.D(36), Visible = false, BackColor = Ui.Surface2 };
        var fb = new TextBox { Left = Ui.D(8), Top = Ui.D(6), Width = Ui.D(260), BackColor = Ui.Surface, ForeColor = Ui.Text, BorderStyle = BorderStyle.FixedSingle };
        var fn = new Button { Text = "Next", Left = Ui.D(276), Top = Ui.D(5), Width = Ui.D(64), Height = Ui.D(26), FlatStyle = FlatStyle.Flat, ForeColor = Ui.Text, BackColor = Ui.Surface };
        var fx = new Button { Text = "✕", Left = Ui.D(346), Top = Ui.D(5), Width = Ui.D(32), Height = Ui.D(26), FlatStyle = FlatStyle.Flat, ForeColor = Ui.Text, BackColor = Ui.Surface };
        find.Controls.AddRange(new Control[] { fb, fn, fx });

        var docs = new List<Doc>(); Doc? cur = null; bool busy = false;

        void Colour(Doc d)
        {
            if (busy) return; busy = true; var b = d.Box;
            try
            {
                b.SuspendLayout(); int ss = b.SelectionStart, sl = b.SelectionLength; var txt = b.Text;
                b.SelectAll(); b.SelectionColor = Ui.Text;
                if (txt.Length < 200000)
                {
                    void Paint(Regex r, Color c) { foreach (Match m in r.Matches(txt)) { b.Select(m.Index, m.Length); b.SelectionColor = c; } }
                    Paint(Num, Color.FromArgb(247, 140, 108)); Paint(Kw, Color.FromArgb(199, 146, 234)); Paint(Str, Color.FromArgb(195, 232, 141)); Paint(Cmt, Color.FromArgb(120, 130, 160));
                }
                b.Select(ss, sl); b.SelectionColor = Ui.Text;
            }
            catch { }
            finally { b.ResumeLayout(); busy = false; }
        }

        void Retab()
        {
            tabs.SuspendLayout(); tabs.Controls.Clear();
            foreach (var d in docs)
            {
                var dd = d; var on = dd == cur;
                var b = new Button { Text = dd.Title + "  ", AutoSize = true, Height = Ui.D(34), FlatStyle = FlatStyle.Flat, ForeColor = Ui.Text, BackColor = on ? Theme.Accent : Ui.Surface2, Font = Ui.F(9.5f), Margin = new Padding(0, 0, Ui.D(2), 0) };
                b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => Switch(dd);
                b.MouseUp += (_, e) => { if (e.Button == MouseButtons.Middle) Close(dd); };
                var x = new Button { Text = "✕", Width = Ui.D(26), Height = Ui.D(34), FlatStyle = FlatStyle.Flat, ForeColor = Ui.Sub, BackColor = b.BackColor, Margin = new Padding(0, 0, Ui.D(6), 0) };
                x.FlatAppearance.BorderSize = 0; x.Click += (_, _) => Close(dd);
                tabs.Controls.Add(b); tabs.Controls.Add(x);
            }
            tabs.ResumeLayout();
        }

        void Switch(Doc d)
        {
            cur = d; host.Controls.Clear(); host.Controls.Add(d.Box); host.Controls.Add(gutter); gutter.Box = d.Box; d.Box.BringToFront(); gutter.Invalidate(); Retab();
            status.Text = (d.Path ?? "Untitled") + "    Ln 1, Col 1";
        }

        Doc NewDoc(string? path)
        {
            var d = new Doc { Path = path };
            d.Box = new RichTextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Ui.Surface, ForeColor = Ui.Text, Font = new Font("Consolas", 11), AcceptsTab = true, WordWrap = false, DetectUrls = false, ScrollBars = RichTextBoxScrollBars.ForcedBoth };
            if (path != null && File.Exists(path)) { try { d.Box.Text = File.ReadAllText(path); } catch (Exception ex) { d.Box.Text = "Couldn't open: " + ex.Message; } }
            var t = new System.Windows.Forms.Timer { Interval = 450 };
            t.Tick += (_, _) => { t.Stop(); Colour(d); };
            d.Box.TextChanged += (_, _) => { if (busy) return; if (!d.Dirty) { d.Dirty = true; Retab(); } t.Stop(); t.Start(); gutter.Invalidate(); };
            d.Box.VScroll += (_, _) => gutter.Invalidate(); d.Box.Resize += (_, _) => gutter.Invalidate();
            d.Box.SelectionChanged += (_, _) => { if (busy || d != cur) return; int i = d.Box.SelectionStart; int ln = d.Box.GetLineFromCharIndex(i); int col = i - d.Box.GetFirstCharIndexFromLine(ln); status.Text = (d.Path ?? "Untitled") + $"    Ln {ln + 1}, Col {col + 1}"; };
            d.Box.KeyDown += (_, e) =>
            {
                if (e.Control && e.KeyCode == Keys.S) { Save(d, false); e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.F) { find.Visible = true; fb.Focus(); e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.N) { Open(null); e.SuppressKeyPress = true; }
            };
            d.Box.DragEnter += (_, _) => { };
            docs.Add(d); Colour(d); d.Dirty = false; return d;
        }

        void Open(string? path)
        {
            if (path != null) { var ex = docs.FirstOrDefault(x => x.Path != null && string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)); if (ex != null) { Switch(ex); return; } }
            Switch(NewDoc(path));
        }

        void Save(Doc d, bool asNew)
        {
            try
            {
                if (asNew || d.Path == null) { using var dlg = new SaveFileDialog { InitialDirectory = Paths.Desktop, FileName = d.Path == null ? "untitled.txt" : System.IO.Path.GetFileName(d.Path) }; if (dlg.ShowDialog() != DialogResult.OK) return; d.Path = dlg.FileName; }
                File.WriteAllText(d.Path!, d.Box.Text); d.Dirty = false; Retab(); sh.Toast("Saved " + System.IO.Path.GetFileName(d.Path)); sh.Reload();
            }
            catch (Exception ex) { sh.Toast("Couldn't save: " + ex.Message); }
        }

        void Close(Doc d)
        {
            if (d.Dirty && MessageBox.Show("Close " + System.IO.Path.GetFileName(d.Path ?? "Untitled") + " without saving?", "Code Editor", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            int i = docs.IndexOf(d); docs.Remove(d); d.Box.Dispose();
            if (docs.Count == 0) Switch(NewDoc(null)); else Switch(docs[Math.Min(i, docs.Count - 1)]);
        }

        void FindNext()
        {
            if (cur == null || fb.Text == "") return;
            var b = cur.Box; int from = Math.Min(b.TextLength, b.SelectionStart + b.SelectionLength);
            int i = b.Text.IndexOf(fb.Text, from, StringComparison.OrdinalIgnoreCase); if (i < 0) i = b.Text.IndexOf(fb.Text, 0, StringComparison.OrdinalIgnoreCase);
            if (i >= 0) { b.Select(i, fb.TextLength); b.ScrollToCaret(); b.Focus(); } else sh.Toast("Not found");
        }
        fn.Click += (_, _) => FindNext(); fx.Click += (_, _) => find.Visible = false;
        fb.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { FindNext(); e.SuppressKeyPress = true; } else if (e.KeyCode == Keys.Escape) find.Visible = false; };

        void Fill(string dir)
        {
            tree.BeginUpdate(); tree.Nodes.Clear();
            void Add(TreeNodeCollection into, string d, int depth)
            {
                try
                {
                    foreach (var x in Directory.GetDirectories(d).OrderBy(z => z, StringComparer.OrdinalIgnoreCase))
                    {
                        var n = into.Add(System.IO.Path.GetFileName(x)); n.Tag = x; if (depth < 3) Add(n.Nodes, x, depth + 1);
                    }
                    foreach (var f in Directory.GetFiles(d).OrderBy(z => z, StringComparer.OrdinalIgnoreCase)) { var n = into.Add(System.IO.Path.GetFileName(f)); n.Tag = f; }
                }
                catch { }
            }
            var top = tree.Nodes.Add(System.IO.Path.GetFileName(dir.TrimEnd('\\', '/')) is { Length: > 0 } nm ? nm : dir); top.Tag = dir; Add(top.Nodes, dir, 0); top.Expand(); tree.EndUpdate();
        }
        tree.NodeMouseDoubleClick += (_, e) => { if (e.Node.Tag is string p && File.Exists(p)) Open(p); };

        string folder = Paths.Desktop; if (startPath != null && File.Exists(startPath)) folder = System.IO.Path.GetDirectoryName(startPath) ?? folder;
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("New  (Ctrl+N)", null, (_, _) => Open(null));
        file.DropDownItems.Add("Open file…", null, (_, _) => { using var d = new OpenFileDialog { InitialDirectory = Paths.Desktop }; if (d.ShowDialog() == DialogResult.OK) Open(d.FileName); });
        file.DropDownItems.Add("Open folder…", null, (_, _) => { using var d = new FolderBrowserDialog { SelectedPath = folder }; if (d.ShowDialog() == DialogResult.OK) { folder = d.SelectedPath; Fill(folder); } });
        file.DropDownItems.Add("Save  (Ctrl+S)", null, (_, _) => { if (cur != null) Save(cur, false); });
        file.DropDownItems.Add("Save as…", null, (_, _) => { if (cur != null) Save(cur, true); });
        var edit = new ToolStripMenuItem("Edit"); edit.DropDownItems.Add("Find  (Ctrl+F)", null, (_, _) => { find.Visible = true; fb.Focus(); });
        edit.DropDownItems.Add("Bigger text", null, (_, _) => { if (cur != null) cur.Box.Font = new Font("Consolas", Math.Min(28, cur.Box.Font.Size + 1)); gutter.Invalidate(); });
        edit.DropDownItems.Add("Smaller text", null, (_, _) => { if (cur != null) cur.Box.Font = new Font("Consolas", Math.Max(7, cur.Box.Font.Size - 1)); gutter.Invalidate(); });
        var run = new ToolStripMenuItem("Run"); run.DropDownItems.Add("Run this file", null, (_, _) =>
        {
            if (cur == null) return; if (cur.Path == null || cur.Dirty) Save(cur, false); if (cur.Path == null) return;
            var ext = System.IO.Path.GetExtension(cur.Path).ToLowerInvariant();
            try
            {
                if (ext == ".html" || ext == ".htm") sh.Launch("web", new Uri(cur.Path).AbsoluteUri);
                else if (ext == ".bat" || ext == ".cmd") System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/k \"" + cur.Path + "\"") { UseShellExecute = true });
                else if (ext == ".py") System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/k python \"" + cur.Path + "\"") { UseShellExecute = true });
                else if (ext == ".js") System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/k node \"" + cur.Path + "\"") { UseShellExecute = true });
                else sh.Toast("Run works for .html, .bat, .py and .js files");
            }
            catch (Exception ex) { sh.Toast("Couldn't run: " + ex.Message); }
        });
        ms.Items.AddRange(new ToolStripItem[] { file, edit, run });

        host.Controls.Add(gutter);
        root.Controls.Add(host); root.Controls.Add(find); root.Controls.Add(tabs); root.Controls.Add(tree); root.Controls.Add(ms); root.Controls.Add(status);
        bool started = false;
        root.HandleCreated += (_, _) => { if (started) return; started = true; Fill(folder); if (startPath != null && File.Exists(startPath)) Open(startPath); else Open(null); };
        root.Tag = new StateApi { Get = () => cur?.Path, Set = p => { if (!string.IsNullOrEmpty(p) && File.Exists(p)) Open(p); } };
        return root;
    }
}

using System.Diagnostics;

namespace UsbDesktop;

class DesktopForm : Form
{
    public static DesktopForm? I;
    static readonly string[] TextExt = { ".txt", ".md", ".js", ".json", ".log", ".cs", ".html", ".css", ".py", ".ini", ".xml" };
    static readonly Font BigClock = new("Segoe UI Light", 34), Small = new("Segoe UI", 11), Tiny = new("Segoe UI", 9);

    readonly ListView lv = new();
    readonly ImageList icons = new() { ColorDepth = ColorDepth.Depth32Bit };
    readonly Panel bar = new(), widget = new();
    readonly FlowLayoutPanel tasks = new();
    readonly Button startBtn = new(), shotBtn = new();
    readonly Label sys = new(), toast = new();
    readonly ContextMenuStrip menu = new(), start = new();
    readonly System.Windows.Forms.Timer clockT = new() { Interval = 1000 }, toastT = new() { Interval = 2600 };
    SettingsForm? settings;

    public DesktopForm()
    {
        I = this;
        FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Maximized; Text = "USB OS"; KeyPreview = true;

        bar.Dock = DockStyle.Bottom; bar.Height = 48; bar.BackColor = Color.FromArgb(20, 20, 34);
        tasks.Dock = DockStyle.Fill; tasks.WrapContents = false; tasks.BackColor = bar.BackColor;
        startBtn.Text = "⊞  Start"; startBtn.Dock = DockStyle.Left; startBtn.Width = 110; startBtn.FlatStyle = FlatStyle.Flat; startBtn.ForeColor = Color.White; startBtn.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        startBtn.FlatAppearance.BorderSize = 0; startBtn.Click += (_, _) => start.Show(startBtn, new Point(0, 0), ToolStripDropDownDirection.AboveRight);
        shotBtn.Text = "📷"; shotBtn.Dock = DockStyle.Right; shotBtn.Width = 46; shotBtn.FlatStyle = FlatStyle.Flat; shotBtn.ForeColor = Color.White; shotBtn.Font = new Font("Segoe UI Emoji", 13);
        shotBtn.FlatAppearance.BorderSize = 0; shotBtn.Click += (_, _) => Shot();
        sys.Dock = DockStyle.Right; sys.Width = 130; sys.ForeColor = Color.White; sys.Font = Tiny; sys.TextAlign = ContentAlignment.MiddleCenter;
        bar.Controls.AddRange(new Control[] { tasks, sys, shotBtn, startBtn });

        lv.Dock = DockStyle.Fill; lv.View = View.LargeIcon; lv.LargeImageList = icons; lv.LabelEdit = true; lv.BorderStyle = BorderStyle.None;
        lv.ForeColor = Color.White; lv.BackColor = Color.FromArgb(38, 33, 92); lv.Font = new Font("Segoe UI", 10); lv.BackgroundImageTiled = false;
        lv.ItemActivate += (_, _) => { if (lv.SelectedItems.Count > 0) Activate(lv.SelectedItems[0]); };
        lv.BeforeLabelEdit += (_, e) => { if (lv.Items[e.Item].Tag != null) e.CancelEdit = true; };
        lv.AfterLabelEdit += Renamed;
        lv.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) DeleteSelected(); if (e.KeyCode == Keys.F2 && lv.SelectedItems.Count > 0) lv.SelectedItems[0].BeginEdit(); if (e.KeyCode == Keys.F5) Reload(); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.F12) Shot(); };
        KeyUp += (_, e) => { if (e.KeyCode == Keys.PrintScreen) Shot(); };

        widget.Size = new Size(260, 160); widget.BackColor = Color.FromArgb(28, 28, 48);
        widget.Paint += PaintWidget;
        toast.AutoSize = false; toast.Size = new Size(440, 42); toast.TextAlign = ContentAlignment.MiddleCenter; toast.BackColor = Color.FromArgb(28, 28, 48); toast.ForeColor = Color.White; toast.Font = Small; toast.Visible = false;
        toastT.Tick += (_, _) => { toastT.Stop(); toast.Visible = false; };

        Controls.Add(lv); Controls.Add(bar); Controls.Add(widget); Controls.Add(toast);
        widget.BringToFront(); toast.BringToFront();
        Resize += (_, _) => widget.Location = new Point(ClientSize.Width - widget.Width - 24, 24);

        var nw = new ToolStripMenuItem("New");
        nw.DropDownItems.Add("Folder", null, (_, _) => Create("New folder", true));
        nw.DropDownItems.Add("Text document", null, (_, _) => Create("New document.txt", false));
        nw.DropDownItems.Add("Code file", null, (_, _) => Create("new.js", false));
        var vw = new ToolStripMenuItem("View");
        vw.DropDownItems.Add("Small icons", null, (_, _) => { Cfg.Set("icon", "32"); Reload(); });
        vw.DropDownItems.Add("Medium icons", null, (_, _) => { Cfg.Set("icon", "48"); Reload(); });
        vw.DropDownItems.Add("Large icons", null, (_, _) => { Cfg.Set("icon", "64"); Reload(); });
        var ren = new ToolStripMenuItem("Rename (F2)", null, (_, _) => { if (lv.SelectedItems.Count > 0) lv.SelectedItems[0].BeginEdit(); });
        var del = new ToolStripMenuItem("Delete (to Recycle Bin)", null, (_, _) => DeleteSelected());
        menu.Items.AddRange(new ToolStripItem[] { vw, new ToolStripMenuItem("Refresh", null, (_, _) => Reload()), nw, new ToolStripSeparator(),
            new ToolStripMenuItem("Take screenshot", null, (_, _) => Shot()), new ToolStripMenuItem("Empty Recycle Bin", null, (_, _) => EmptyBin()),
            new ToolStripMenuItem("Personalize", null, (_, _) => OpenSettings()), new ToolStripSeparator(), ren, del });
        menu.Opening += (_, _) => { bool f = lv.SelectedItems.Count > 0 && lv.SelectedItems[0].Tag == null; ren.Enabled = del.Enabled = f; };
        lv.ContextMenuStrip = menu;

        clockT.Tick += (_, _) => Tick(); clockT.Start(); Tick();
        Load += (_, _) => { ApplyTheme(); widget.Location = new Point(ClientSize.Width - widget.Width - 24, 24); };
    }

    public void ApplyTheme()
    {
        lv.BackgroundImage?.Dispose();
        lv.BackgroundImage = Theme.Wall(Screen.FromControl(this).Bounds.Size);
        startBtn.BackColor = Theme.Accent; widget.Visible = Cfg.Get("widget", "1") == "1";
        BuildStart(); Reload();
    }

    void Tick() { sys.Text = DateTime.Now.ToString("HH:mm") + "\n" + DateTime.Now.ToString("ddd, MMM d"); widget.Invalidate(); }

    void PaintWidget(object? s, PaintEventArgs e)
    {
        var g = e.Graphics;
        TextRenderer.DrawText(g, DateTime.Now.ToString("HH:mm"), BigClock, new Point(12, 6), Color.White);
        TextRenderer.DrawText(g, DateTime.Now.ToString("dddd, MMM d"), Small, new Point(18, 66), Color.FromArgb(205, 205, 235));
        try
        {
            var d = new DriveInfo(Path.GetPathRoot(Paths.Root)!); double used = 1.0 - (double)d.AvailableFreeSpace / d.TotalSize;
            TextRenderer.DrawText(g, $"USB {d.Name.TrimEnd('\\')}   {d.AvailableFreeSpace / 1073741824} GB free", Tiny, new Point(18, 108), Color.FromArgb(175, 175, 210));
            using var tr = new SolidBrush(Color.FromArgb(60, 60, 90)); g.FillRectangle(tr, 18, 132, 224, 8);
            using var fl = new SolidBrush(Theme.Accent); g.FillRectangle(fl, 18, 132, (int)(224 * used), 8);
        }
        catch { }
    }

    void Reload()
    {
        int sz = int.TryParse(Cfg.Get("icon", "48"), out var n) ? n : 48;
        icons.ImageSize = new Size(sz, sz); lv.Items.Clear(); icons.Images.Clear();
        icons.Images.Add("dir", Theme.Tile(Color.FromArgb(239, 159, 39), "📁", sz));
        var v = new (string t, string n, Color c, string g)[]
        {
            ("pc", "This PC", Color.FromArgb(55, 138, 221), "💻"), ("bin", "Recycle Bin", Color.FromArgb(95, 94, 90), "🗑"),
            ("browser", "Browser", Color.FromArgb(29, 158, 117), "🌐"), ("notepad", "Notepad", Color.FromArgb(186, 117, 23), "📝"),
            ("term", "Terminal", Color.FromArgb(44, 44, 42), "⌨"), ("shot", "Screenshot", Color.FromArgb(212, 83, 126), "📷"),
            ("settings", "Settings", Color.FromArgb(83, 74, 183), "⚙"),
        };
        foreach (var x in v) { icons.Images.Add(x.t, Theme.Tile(x.c, x.g, sz)); lv.Items.Add(new ListViewItem(x.n, x.t) { Tag = x.t }); }
        foreach (var p in Directory.GetFileSystemEntries(Paths.Desktop).OrderByDescending(Directory.Exists).ThenBy(p => p))
        {
            var key = "dir";
            if (File.Exists(p))
            {
                var ext = Path.GetExtension(p).ToLower();
                key = ext is ".exe" or ".lnk" or "" ? p : ext;
                if (!icons.Images.ContainsKey(key))
                {
                    try { icons.Images.Add(key, Icon.ExtractAssociatedIcon(p)!.ToBitmap()); }
                    catch { icons.Images.Add(key, Theme.Tile(Color.FromArgb(120, 120, 140), "📄", sz)); }
                }
            }
            lv.Items.Add(new ListViewItem(Path.GetFileName(p), key));
        }
    }

    void Activate(ListViewItem it)
    {
        switch (it.Tag as string)
        {
            case "pc": Open(Paths.Root); return;
            case "bin": Open(Paths.Bin); return;
            case "browser": Open("https://www.google.com"); return;
            case "notepad": Launch(new NotepadForm()); return;
            case "term": Process.Start(new ProcessStartInfo("cmd.exe") { WorkingDirectory = Paths.Root, UseShellExecute = true }); return;
            case "shot": Shot(); return;
            case "settings": OpenSettings(); return;
        }
        var p = Path.Combine(Paths.Desktop, it.Text);
        if (File.Exists(p) && TextExt.Contains(Path.GetExtension(p).ToLower())) Launch(new NotepadForm(p)); else Open(p);
    }

    void OpenSettings()
    {
        if (settings == null || settings.IsDisposed) { settings = new SettingsForm(); Launch(settings); }
        else { settings.Show(); settings.WindowState = FormWindowState.Normal; settings.Activate(); }
    }

    void Launch(Form f)
    {
        var b = new Button { Width = 150, Height = 36, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(52, 52, 80), Margin = new Padding(4, 6, 0, 0), TextAlign = ContentAlignment.MiddleLeft };
        b.FlatAppearance.BorderSize = 0;
        void Name() => b.Text = f.Text.Length > 18 ? f.Text[..18] + "…" : f.Text;
        Name(); f.TextChanged += (_, _) => Name();
        b.Click += (_, _) => { if (!f.Visible) { f.Show(); f.Activate(); } else if (f.ContainsFocus) f.Hide(); else f.Activate(); };
        f.Resize += (_, _) => { if (f.WindowState == FormWindowState.Minimized) { f.WindowState = FormWindowState.Normal; f.Hide(); } };
        f.FormClosed += (_, _) => { tasks.Controls.Remove(b); b.Dispose(); };
        tasks.Controls.Add(b); f.Show(this);
    }

    void BuildStart()
    {
        start.Items.Clear(); start.Font = new Font("Segoe UI", 11); start.ImageScalingSize = new Size(32, 32);
        var head = new ToolStripMenuItem(Theme.Name, Theme.AvatarBmp(32)) { Font = new Font("Segoe UI", 12, FontStyle.Bold), Enabled = false };
        start.Items.Add(head); start.Items.Add(new ToolStripSeparator());
        foreach (var a in Directory.GetFiles(Paths.Apps).Where(f => f.EndsWith(".exe") || f.EndsWith(".lnk") || f.EndsWith(".bat")))
        { var p = a; start.Items.Add(Path.GetFileNameWithoutExtension(p), null, (_, _) => Open(p)); }
        start.Items.Add("Notepad", null, (_, _) => Launch(new NotepadForm()));
        start.Items.Add("Settings", null, (_, _) => OpenSettings());
        start.Items.Add("Screenshot", null, (_, _) => Shot());
        start.Items.Add("Full OS (Linux VM)", null, (_, _) =>
        {
            var bat = Path.Combine(Paths.Vm, "start.bat");
            if (File.Exists(bat)) Open(bat); else MessageBox.Show("VM isn't set up yet. Add QEMU and a disk to the vm folder.");
        });
        start.Items.Add(new ToolStripSeparator());
        start.Items.Add("Lock", null, (_, _) => { using var l = new LoginForm(); l.ShowDialog(); });
        start.Items.Add("Shut down", null, (_, _) => Application.Exit());
    }

    async void Shot()
    {
        try
        {
            await Task.Delay(350);
            var r = Screen.FromControl(this).Bounds;
            using var bmp = new Bitmap(r.Width, r.Height);
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
            Directory.CreateDirectory(Paths.Shots);
            bmp.Save(Path.Combine(Paths.Shots, $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"), System.Drawing.Imaging.ImageFormat.Png);
            try { Clipboard.SetImage(bmp); } catch { }
            Toast("Screenshot saved to Desktop\\Screenshots and copied"); Reload();
        }
        catch (Exception ex) { Toast("Screenshot failed: " + ex.Message); }
    }

    void Toast(string s)
    {
        toast.Text = s; toast.Location = new Point((ClientSize.Width - toast.Width) / 2, ClientSize.Height - bar.Height - 70);
        toast.Visible = true; toast.BringToFront(); toastT.Stop(); toastT.Start();
    }

    void Create(string baseName, bool dir)
    {
        var n = baseName; int i = 2;
        while (File.Exists(Path.Combine(Paths.Desktop, n)) || Directory.Exists(Path.Combine(Paths.Desktop, n)))
            n = Path.GetFileNameWithoutExtension(baseName) + $" ({i++})" + Path.GetExtension(baseName);
        var full = Path.Combine(Paths.Desktop, n);
        if (dir) Directory.CreateDirectory(full); else File.WriteAllText(full, "");
        Reload();
        var item = lv.Items.Cast<ListViewItem>().FirstOrDefault(x => x.Text == n);
        if (item != null) { item.Selected = true; item.BeginEdit(); }
    }

    void Renamed(object? s, LabelEditEventArgs e)
    {
        var old = lv.Items[e.Item].Text;
        if (string.IsNullOrWhiteSpace(e.Label) || e.Label == old) { e.CancelEdit = true; return; }
        try
        {
            var a = Path.Combine(Paths.Desktop, old); var b = Path.Combine(Paths.Desktop, e.Label);
            if (Directory.Exists(a)) Directory.Move(a, b); else File.Move(a, b);
        }
        catch { e.CancelEdit = true; MessageBox.Show("Couldn't rename that. Try another name."); }
    }

    void DeleteSelected()
    {
        foreach (ListViewItem it in lv.SelectedItems.Cast<ListViewItem>().ToList())
        {
            if (it.Tag != null) continue;
            var a = Path.Combine(Paths.Desktop, it.Text); var b = Path.Combine(Paths.Bin, DateTime.Now.ToString("HHmmss_") + it.Text);
            try { if (Directory.Exists(a)) Directory.Move(a, b); else File.Move(a, b); } catch (Exception ex) { Toast(ex.Message); }
        }
        Reload(); Toast("Moved to Recycle Bin");
    }

    void EmptyBin()
    {
        if (MessageBox.Show("Permanently delete everything in the Recycle Bin?", "Recycle Bin", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        foreach (var p in Directory.GetFileSystemEntries(Paths.Bin)) { try { if (Directory.Exists(p)) Directory.Delete(p, true); else File.Delete(p); } catch { } }
        Toast("Recycle Bin emptied");
    }

    static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path) ?? "" }); }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
}

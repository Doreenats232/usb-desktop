using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace UsbDesktop;

static class Paths
{
    public static readonly string Root = Path.GetDirectoryName(Environment.ProcessPath)!;
    public static readonly string Desktop = Path.Combine(Root, "Desktop");
    public static readonly string Apps = Path.Combine(Root, "apps");
    public static readonly string Data = Path.Combine(Root, "data");
    public static readonly string Vm = Path.Combine(Root, "vm");
}

static class Theme
{
    public static Bitmap Wallpaper(Size s, Color bg, Color c1, Color c2)
    {
        var b = new Bitmap(Math.Max(s.Width, 1), Math.Max(s.Height, 1));
        using var g = Graphics.FromImage(b);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(bg);
        using (var br = new SolidBrush(c1)) g.FillEllipse(br, s.Width - 520, -280, 760, 760);
        using (var br = new SolidBrush(Color.FromArgb(120, c2))) g.FillEllipse(br, -200, s.Height - 380, 640, 640);
        using (var br = new SolidBrush(Color.FromArgb(70, c2))) g.FillEllipse(br, s.Width - 300, s.Height - 260, 420, 420);
        return b;
    }
}

static class Program
{
    [STAThread]
    static void Main()
    {
        foreach (var d in new[] { Paths.Desktop, Paths.Apps, Paths.Data, Paths.Vm }) Directory.CreateDirectory(d);
        ApplicationConfiguration.Initialize();
        using (var login = new LoginForm())
            if (login.ShowDialog() != DialogResult.OK) return;
        Application.Run(new DesktopForm());
    }
}

class LoginForm : Form
{
    readonly string userFile = Path.Combine(Paths.Data, "user.txt");
    readonly bool firstRun;
    readonly Label clock = new(), date = new(), name = new(), hint = new(), err = new();
    readonly TextBox pass = new();
    readonly Panel card = new(), avatar = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };

    public LoginForm()
    {
        firstRun = !File.Exists(userFile);
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        DoubleBuffered = true;
        KeyPreview = true;

        clock.Font = new Font("Segoe UI Light", 54); clock.ForeColor = Color.FromArgb(238, 237, 254); clock.AutoSize = true; clock.BackColor = Color.Transparent; clock.Location = new Point(40, 30);
        date.Font = new Font("Segoe UI", 14); date.ForeColor = Color.FromArgb(206, 203, 246); date.AutoSize = true; date.BackColor = Color.Transparent; date.Location = new Point(46, 135);
        Controls.AddRange(new Control[] { clock, date, card });

        card.Size = new Size(340, 360); card.BackColor = Color.FromArgb(48, 42, 112);
        avatar.Size = new Size(96, 96); avatar.Location = new Point(122, 28); avatar.BackColor = card.BackColor;
        avatar.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var br = new SolidBrush(Color.FromArgb(83, 74, 183));
            using var pen = new Pen(Color.FromArgb(175, 169, 236), 3);
            e.Graphics.FillEllipse(br, 3, 3, 88, 88); e.Graphics.DrawEllipse(pen, 3, 3, 88, 88);
            var t = Environment.UserName.Substring(0, 1).ToUpper();
            TextRenderer.DrawText(e.Graphics, t, new Font("Segoe UI", 32), new Rectangle(0, 0, 96, 96), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        name.Text = Environment.UserName; name.Font = new Font("Segoe UI", 16); name.ForeColor = Color.White; name.AutoSize = false; name.TextAlign = ContentAlignment.MiddleCenter; name.SetBounds(0, 134, 340, 36);
        hint.Text = firstRun ? "Create a password for this USB" : "Enter your password"; hint.Font = new Font("Segoe UI", 10); hint.ForeColor = Color.FromArgb(206, 203, 246); hint.AutoSize = false; hint.TextAlign = ContentAlignment.MiddleCenter; hint.SetBounds(0, 172, 340, 24);
        pass.UseSystemPasswordChar = true; pass.Font = new Font("Segoe UI", 13); pass.SetBounds(30, 212, 240, 34); pass.BorderStyle = BorderStyle.FixedSingle; pass.BackColor = Color.FromArgb(60, 52, 137); pass.ForeColor = Color.White;
        var go = new Button { Text = "→", Font = new Font("Segoe UI", 13), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(127, 119, 221), ForeColor = Color.White, Bounds = new Rectangle(276, 212, 34, 34) };
        go.FlatAppearance.BorderSize = 0; go.Click += (_, _) => Submit();
        err.ForeColor = Color.FromArgb(240, 149, 149); err.Font = new Font("Segoe UI", 10); err.AutoSize = false; err.TextAlign = ContentAlignment.MiddleCenter; err.SetBounds(0, 254, 340, 24);
        var usb = new Label { Text = "Running from " + Path.GetPathRoot(Paths.Root), ForeColor = Color.FromArgb(175, 169, 236), Font = new Font("Segoe UI", 10), AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(0, 300, 340, 24) };
        card.Controls.AddRange(new Control[] { avatar, name, hint, pass, go, err, usb });

        var off = new Button { Text = "⏻", Font = new Font("Segoe UI", 14), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(83, 74, 183), Size = new Size(44, 44) };
        off.FlatAppearance.BorderSize = 0; off.Click += (_, _) => { DialogResult = DialogResult.Cancel; };
        Controls.Add(off);
        Resize += (_, _) => { card.Location = new Point((ClientSize.Width - card.Width) / 2, (ClientSize.Height - card.Height) / 2); off.Location = new Point(ClientSize.Width - 64, ClientSize.Height - 64); Invalidate(); };

        AcceptButton = go;
        timer.Tick += (_, _) => Tick(); timer.Start(); Tick();
        Shown += (_, _) => pass.Focus();
    }

    void Tick() { clock.Text = DateTime.Now.ToString("HH:mm"); date.Text = DateTime.Now.ToString("dddd, MMMM d"); }
    static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("usbdesk" + s)));

    void Submit()
    {
        if (pass.Text.Length < 1) { err.Text = "Enter a password"; return; }
        if (firstRun) { File.WriteAllText(userFile, Hash(pass.Text)); DialogResult = DialogResult.OK; return; }
        if (File.ReadAllText(userFile).Trim() == Hash(pass.Text)) DialogResult = DialogResult.OK;
        else { err.Text = "Wrong password. Try again."; pass.SelectAll(); }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var w = Theme.Wallpaper(ClientSize, Color.FromArgb(38, 33, 92), Color.FromArgb(60, 52, 137), Color.FromArgb(83, 74, 183));
        e.Graphics.DrawImage(w, 0, 0);
    }
}

class DesktopForm : Form
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int ExtractIconEx(string file, int idx, IntPtr[] large, IntPtr[]? small, int n);

    readonly ListView lv = new();
    readonly ImageList icons = new() { ImageSize = new Size(48, 48), ColorDepth = ColorDepth.Depth32Bit };
    readonly Label clock = new(), space = new();
    readonly Panel bar = new();
    readonly ContextMenuStrip menu = new(), start = new();

    public DesktopForm()
    {
        FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Maximized; Text = "USB Desktop";

        bar.Dock = DockStyle.Bottom; bar.Height = 42; bar.BackColor = Color.FromArgb(4, 44, 83);
        var btn = new Button { Text = "⊞  Start", Dock = DockStyle.Left, Width = 100, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, Font = new Font("Segoe UI", 11) };
        btn.FlatAppearance.BorderSize = 0; btn.Click += (_, _) => start.Show(btn, new Point(0, 0), ToolStripDropDownDirection.AboveRight);
        clock.Dock = DockStyle.Right; clock.Width = 80; clock.ForeColor = Color.White; clock.Font = new Font("Segoe UI", 11); clock.TextAlign = ContentAlignment.MiddleCenter;
        space.Dock = DockStyle.Right; space.Width = 190; space.ForeColor = Color.FromArgb(181, 212, 244); space.Font = new Font("Segoe UI", 10); space.TextAlign = ContentAlignment.MiddleCenter;
        bar.Controls.AddRange(new Control[] { clock, space, btn });

        lv.Dock = DockStyle.Fill; lv.View = View.LargeIcon; lv.LargeImageList = icons; lv.LabelEdit = true; lv.BorderStyle = BorderStyle.None;
        lv.ForeColor = Color.White; lv.BackColor = Color.FromArgb(12, 68, 124); lv.Font = new Font("Segoe UI", 10); lv.MultiSelect = true;
        lv.BackgroundImageTiled = false;
        lv.ItemActivate += (_, _) => { if (lv.SelectedItems.Count > 0) Open(Path.Combine(Paths.Desktop, lv.SelectedItems[0].Text)); };
        lv.AfterLabelEdit += Renamed;
        lv.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) DeleteSelected(); if (e.KeyCode == Keys.F2 && lv.SelectedItems.Count > 0) lv.SelectedItems[0].BeginEdit(); if (e.KeyCode == Keys.F5) Reload(); };
        Controls.Add(lv); Controls.Add(bar);

        var nw = new ToolStripMenuItem("New");
        nw.DropDownItems.Add("Folder", null, (_, _) => Create("New folder", true));
        nw.DropDownItems.Add("Text document", null, (_, _) => Create("New document.txt", false));
        nw.DropDownItems.Add("Code file", null, (_, _) => Create("new.js", false));
        menu.Items.Add("Refresh", null, (_, _) => Reload());
        menu.Items.Add(nw);
        menu.Items.Add("Open Desktop folder", null, (_, _) => Open(Paths.Desktop));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Rename (F2)", null, (_, _) => { if (lv.SelectedItems.Count > 0) lv.SelectedItems[0].BeginEdit(); });
        menu.Items.Add("Delete", null, (_, _) => DeleteSelected());
        lv.ContextMenuStrip = menu;

        BuildStart();
        var t = new System.Windows.Forms.Timer { Interval = 1000 }; t.Tick += (_, _) => Tick(); t.Start(); Tick();
        Load += (_, _) => { lv.BackgroundImage = Theme.Wallpaper(Screen.PrimaryScreen!.Bounds.Size, Color.FromArgb(12, 68, 124), Color.FromArgb(24, 95, 165), Color.FromArgb(55, 138, 221)); Reload(); };
    }

    void Tick()
    {
        clock.Text = DateTime.Now.ToString("HH:mm");
        try { var d = new DriveInfo(Path.GetPathRoot(Paths.Root)!); space.Text = $"USB {d.Name.TrimEnd('\\')}  {d.AvailableFreeSpace / 1073741824} GB free"; } catch { }
    }

    static Icon? Shell(int idx)
    {
        var l = new IntPtr[1];
        ExtractIconEx(Path.Combine(Environment.SystemDirectory, "shell32.dll"), idx, l, null, 1);
        return l[0] == IntPtr.Zero ? null : Icon.FromHandle(l[0]);
    }

    void Reload()
    {
        lv.Items.Clear(); icons.Images.Clear();
        icons.Images.Add("dir", (Shell(3) ?? SystemIcons.Application));
        foreach (var p in Directory.GetFileSystemEntries(Paths.Desktop).OrderByDescending(Directory.Exists).ThenBy(p => p))
        {
            var key = "dir";
            if (File.Exists(p))
            {
                key = Path.GetExtension(p).ToLower();
                if (!icons.Images.ContainsKey(key))
                    try { icons.Images.Add(key, Icon.ExtractAssociatedIcon(p)!); } catch { key = "dir"; }
            }
            lv.Items.Add(new ListViewItem(Path.GetFileName(p), key));
        }
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
        if (lv.SelectedItems.Count == 0) return;
        if (MessageBox.Show($"Delete {lv.SelectedItems.Count} item(s)?", "Delete", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        foreach (ListViewItem it in lv.SelectedItems)
        {
            var p = Path.Combine(Paths.Desktop, it.Text);
            if (Directory.Exists(p)) Directory.Delete(p, true); else File.Delete(p);
        }
        Reload();
    }

    static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path) }); }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    void BuildStart()
    {
        start.Font = new Font("Segoe UI", 11);
        var apps = Directory.GetFiles(Paths.Apps).Where(f => f.EndsWith(".exe") || f.EndsWith(".lnk") || f.EndsWith(".bat")).ToList();
        foreach (var a in apps) start.Items.Add(Path.GetFileNameWithoutExtension(a), null, (_, _) => Open(a));
        if (apps.Count == 0) start.Items.Add("Put apps in the apps folder").Enabled = false;
        start.Items.Add(new ToolStripSeparator());
        start.Items.Add("Full OS (Linux VM)", null, (_, _) =>
        {
            var bat = Path.Combine(Paths.Vm, "start.bat");
            if (File.Exists(bat)) Open(bat); else MessageBox.Show("VM isn't set up yet. Add QEMU and a disk to the vm folder.");
        });
        start.Items.Add("Lock", null, (_, _) => { using var l = new LoginForm(); l.ShowDialog(); });
        start.Items.Add("Shut down", null, (_, _) => Application.Exit());
    }
}

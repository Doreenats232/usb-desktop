using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace UsbDesktop;

// Portable apps: one-click downloads of free portable programs onto the USB (latest version, straight from their GitHub releases).
static class GetApps
{
    record Item(string Name, string Repo, string Pattern, string Exe, string Note);
    static readonly Item[] Items =
    {
        new("Notepad++", "notepad-plus-plus/notepad-plus-plus", @"^npp\.[\d.]+\.portable\.x64\.zip$", "notepad++.exe", "Code and text editor"),
        new("ShareX", "ShareX/ShareX", @"^ShareX-[\d.]+-portable-x64\.zip$", "ShareX.exe", "Screenshots and screen recording"),
        new("KeePassXC", "keepassxreboot/keepassxc", @"^KeePassXC-[\d.]+-Win64\.zip$", "KeePassXC.exe", "Offline password manager"),
        new("Rufus", "pbatard/rufus", @"^rufus-[\d.]+p\.exe$", "", "Make bootable USB drives"),
        new("Ventoy", "ventoy/Ventoy", @"^ventoy-[\d.]+-windows\.zip$", "", "Boot many ISOs from one USB"),
        new("yt-dlp", "yt-dlp/yt-dlp", @"^yt-dlp\.exe$", "", "Video downloader (command line, opens its folder)"),
    };

    static string DirOf(Item it) => Path.Combine(Paths.Apps, it.Name);
    static bool Have(Item it) { try { return Directory.Exists(DirOf(it)) && Directory.GetFiles(DirOf(it), "*", SearchOption.AllDirectories).Length > 0; } catch { return false; } }

    public static Control View(DesktopForm sh)
    {
        var root = new Panel { BackColor = Ui.Surface }; Ui.Own(root);
        var head = new Label { Dock = DockStyle.Top, Height = Ui.D(54), Text = "Portable apps: one click puts the latest version on your USB", Font = Ui.F(11, true), ForeColor = Ui.Text, BackColor = Ui.Surface2, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(Ui.D(16), 0, 0, 0) };
        var list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Ui.Surface, Padding = new Padding(Ui.D(16), Ui.D(12), 0, 0) };
        root.Controls.Add(list); root.Controls.Add(head);
        foreach (var it in Items)
        {
            var item = it;
            var row = new Panel { Width = Ui.D(640), Height = Ui.D(66), BackColor = Ui.Surface2, Margin = new Padding(0, 0, 0, Ui.D(8)) };
            row.Controls.Add(new Label { Text = item.Name, Font = Ui.F(11, true), ForeColor = Ui.Text, BackColor = Color.Transparent, Left = Ui.D(14), Top = Ui.D(10), AutoSize = true });
            row.Controls.Add(new Label { Text = item.Note, Font = Ui.F(9), ForeColor = Ui.Sub, BackColor = Color.Transparent, Left = Ui.D(14), Top = Ui.D(36), AutoSize = true });
            var b = new RoundButton { Pill = true, BackColor = Theme.Accent, ForeColor = Color.White, Width = Ui.D(110), Height = Ui.D(34), Left = Ui.D(640 - 124), Top = Ui.D(16), Font = Ui.F(10) };
            b.Text = Have(item) ? "Open" : "Get";
            b.Click += async (_, _) =>
            {
                if (Have(item)) { Open(item); return; }
                b.Enabled = false; b.Text = "…";
                try { await Install(item, b); sh.ReloadApps(); sh.Toast(item.Name + " is ready"); }
                catch (Exception ex) { sh.Toast("Couldn't get " + item.Name + ": " + ex.Message); try { Directory.Delete(DirOf(item), true); } catch { } }
                finally { if (!b.IsDisposed) { b.Enabled = true; b.Text = Have(item) ? "Open" : "Get"; } }
            };
            row.Controls.Add(b); list.Controls.Add(row);
        }
        return root;
    }

    static async Task Install(Item it, RoundButton b)
    {
        var j = JsonDocument.Parse(await Net.Text($"https://api.github.com/repos/{it.Repo}/releases/latest")).RootElement; string? url = null;
        foreach (var a in j.GetProperty("assets").EnumerateArray()) { var n = a.GetProperty("name").GetString() ?? ""; if (Regex.IsMatch(n, it.Pattern)) { url = a.GetProperty("browser_download_url").GetString(); break; } }
        if (url == null) throw new Exception("no download found");
        var dir = DirOf(it); Directory.CreateDirectory(dir); var file = Path.Combine(dir, Path.GetFileName(new Uri(url).AbsolutePath));
        await Net.Download(url, file, (g, t, s) => { try { if (!b.IsDisposed) b.BeginInvoke(new Action(() => b.Text = t > 0 ? (int)(100.0 * g / t) + "%" : Ui.Bytes(g))); } catch { } }, CancellationToken.None);
        if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) { ZipFile.ExtractToDirectory(file, dir, true); File.Delete(file); }
    }

    static void Open(Item it)
    {
        try
        {
            var dir = DirOf(it); string? exe = null;
            if (it.Exe != "") exe = Directory.GetFiles(dir, it.Exe, SearchOption.AllDirectories).FirstOrDefault();
            else if (it.Name is "Rufus") exe = Directory.GetFiles(dir, "*.exe").FirstOrDefault();
            else if (it.Name is "Ventoy") exe = Directory.GetFiles(dir, "Ventoy2Disk.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (exe != null) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! });
            else Process.Start(new ProcessStartInfo("explorer.exe", "\"" + dir + "\"") { UseShellExecute = true });
        }
        catch { }
    }
}

// Games: runs games stored on the USB (Games folder) and opens cloud gaming sites.
static class Games
{
    static string Dir => Path.Combine(Paths.Root, "Games");
    public static Control View(DesktopForm sh)
    {
        Directory.CreateDirectory(Dir);
        var root = new Panel { BackColor = Ui.Surface }; Ui.Own(root);
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Ui.D(60), BackColor = Ui.Surface2, Padding = new Padding(Ui.D(12), Ui.D(12), 0, 0) };
        RoundButton TB(string t, int w, Action a) { var b = new RoundButton { Text = t, Pill = true, BackColor = Ui.Line, ForeColor = Ui.Text, Width = Ui.D(w), Height = Ui.D(34), Font = Ui.F(10), Margin = new Padding(0, 0, Ui.D(8), 0) }; b.Click += (_, _) => a(); top.Controls.Add(b); return b; }
        var list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Ui.Surface, Padding = new Padding(Ui.D(16), Ui.D(12), 0, 0) };
        void Fill()
        {
            list.Controls.Clear(); var found = new List<string>();
            try
            {
                foreach (var pat in new[] { "*.exe", "*.lnk", "*.bat" })
                {
                    found.AddRange(Directory.GetFiles(Dir, pat));
                    foreach (var d in Directory.GetDirectories(Dir)) found.AddRange(Directory.GetFiles(d, pat));
                }
            }
            catch { }
            found = found.Where(f => !Regex.IsMatch(Path.GetFileName(f), @"unins|setup|redist|crash|update|vc_|dxsetup", RegexOptions.IgnoreCase)).Distinct().OrderBy(f => f).ToList();
            if (found.Count == 0) list.Controls.Add(new Label { Text = "No games yet. Put game folders inside the Games folder on your USB, then press Refresh.", ForeColor = Ui.Sub, BackColor = Color.Transparent, Font = Ui.F(10.5f), AutoSize = true });
            foreach (var f in found)
            {
                var path = f; var row = new Panel { Width = Ui.D(600), Height = Ui.D(56), BackColor = Ui.Surface2, Margin = new Padding(0, 0, 0, Ui.D(8)) };
                row.Controls.Add(new Label { Text = Path.GetFileNameWithoutExtension(path), Font = Ui.F(11, true), ForeColor = Ui.Text, BackColor = Color.Transparent, Left = Ui.D(14), Top = Ui.D(16), AutoSize = true });
                var b = new RoundButton { Text = "Play", Pill = true, BackColor = Theme.Accent, ForeColor = Color.White, Width = Ui.D(90), Height = Ui.D(32), Left = Ui.D(600 - 104), Top = Ui.D(12), Font = Ui.F(10) };
                b.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path)! }); } catch (Exception ex) { sh.Toast("Couldn't start: " + ex.Message); } };
                row.Controls.Add(b); list.Controls.Add(row);
            }
        }
        TB("GeForce NOW", 120, () => sh.Launch("web", "https://play.geforcenow.com"));
        TB("Xbox Cloud", 110, () => sh.Launch("web", "https://www.xbox.com/play"));
        TB("Open folder", 110, () => { try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Dir + "\"") { UseShellExecute = true }); } catch { } });
        TB("Refresh", 90, Fill);
        root.Controls.Add(list); root.Controls.Add(top); Fill();
        return root;
    }
}

// What's new: shows the change notes once after the app updates.
static class Changelog
{
    public static async Task Show()
    {
        int cur = Updater.Cur, seen = Cfg.Int("seen_ver", 0);
        if (seen == 0) { Cfg.Set("seen_ver", cur.ToString()); return; }
        if (cur <= seen) return;
        var lines = new List<string>();
        for (int v = Math.Max(seen + 1, cur - 5); v <= cur; v++)
        {
            try { var j = JsonDocument.Parse(await Net.Text($"https://api.github.com/repos/Doreenats232/usb-desktop/commits/v1.0.{v}")).RootElement; var m = j.GetProperty("commit").GetProperty("message").GetString() ?? ""; lines.Add("1.0." + v + "   " + m.Split('\n')[0]); }
            catch { }
        }
        Cfg.Set("seen_ver", cur.ToString());
        if (lines.Count > 0) MessageBox.Show(string.Join("\n\n", lines), "What's new in USB Desktop", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

// Wipe traces: clears what this session left on the host PC (clipboard, our temp files, recent-file shortcuts made during the session).
static class Traces
{
    static readonly DateTime Started = Process.GetCurrentProcess().StartTime;
    public static int Wipe()
    {
        int n = 0; School.Cleanup(true);
        try { Clipboard.Clear(); } catch { }
        try
        {
            var rec = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            foreach (var f in Directory.GetFiles(rec, "*.lnk")) { try { if (File.GetLastWriteTime(f) >= Started.AddMinutes(-1)) { File.Delete(f); n++; } } catch { } }
        }
        catch { }
        return n;
    }
}

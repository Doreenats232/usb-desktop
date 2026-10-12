using System.Diagnostics;
using System.IO.Compression;
using System.Drawing.Drawing2D;
namespace UsbDesktop;

static class Installer
{
    public static string IconKey(string name)
    {
        return "";
    }

    static bool LooksLikeWebPage(string f)
    {
        try
        {
            using var s = File.OpenRead(f); var buf = new byte[256]; int n = s.Read(buf, 0, buf.Length);
            var t = System.Text.Encoding.UTF8.GetString(buf, 0, n).TrimStart().ToLowerInvariant();
            return t.StartsWith("<!doctype html") || t.StartsWith("<html") || t.StartsWith("<?xml") && t.Contains("<error");
        }
        catch { return false; }
    }

    // Safe, forgiving zip extraction with live percentage. Returns the folder it created.
    public static string ExtractZip(string zip, string parent, Action<double> progress)
    {
        using var za = ZipFile.OpenRead(zip);
        var entries = za.Entries.Where(x => x.FullName.Length > 0).ToList();
        string Norm(string n) => n.Replace('\\', '/').TrimStart('/');
        var firsts = entries.Select(x => Norm(x.FullName)).Where(x => x != "").Select(x => x.Split('/')[0]).Distinct().ToList();
        bool single = firsts.Count == 1 && entries.Any(x => Norm(x.FullName).Contains('/'));
        string folder = U.Unique(parent, single ? firsts[0] : Path.GetFileNameWithoutExtension(zip));
        Directory.CreateDirectory(folder); var root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        long total = Math.Max(1, entries.Sum(x => x.Length)), done = 0; int skipped = 0;
        foreach (var en in entries)
        {
            var rel = Norm(en.FullName); if (single) rel = rel.Contains('/') ? rel[(rel.IndexOf('/') + 1)..] : "";
            if (rel == "") continue;
            var target = Path.GetFullPath(Path.Combine(folder, rel.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                if (en.FullName.EndsWith('/') || en.FullName.EndsWith('\\')) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); en.ExtractToFile(target, true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is PathTooLongException || ex is NotSupportedException) { skipped++; }
            done += en.Length; progress(Math.Min(100, done * 100.0 / total));
        }
        if (skipped > 0) File.WriteAllText(Path.Combine(folder, "_skipped_files.txt"), skipped + " file(s) could not be extracted.");
        return folder;
    }

    // Called after any download finishes. Validates the file, extracts zips that are apps, updates the UI.
    public static async Task Finish(DesktopForm sh, DlCard card, string file, bool asApp)
    {
        try
        {
            if (!File.Exists(file)) { card.Fail("The file didn't arrive. Try again."); return; }
            var len = new FileInfo(file).Length; var ext = Path.GetExtension(file).ToLowerInvariant();
            if (len == 0) { card.Fail("The server sent an empty file."); try { File.Delete(file); } catch { } return; }
            if (ext != ".html" && ext != ".htm" && ext != ".txt" && ext != ".json" && ext != ".xml" && ext != ".csv" && ext != ".md" && LooksLikeWebPage(file))
            { card.Fail("The server sent a web page instead of the file. Sign in or check the link."); return; }
            if (asApp && ext == ".zip")
            {
                card.Stage("Installing… 0%", 0);
                string folder = await Task.Run(() => ExtractZip(file, Paths.Apps, p => card.Stage("Installing…  " + (int)p + "%", p)));
                try { File.Delete(file); } catch { }
                sh.ReloadApps(); sh.Reload(); card.Done("Installed · find it in the Start menu"); sh.Toast("Installed " + Path.GetFileName(folder));
            }
            else
            {
                if (asApp) { sh.ReloadApps(); }
                sh.Reload(); card.Done("Saved · " + Ui.Bytes(len)); sh.Toast("Downloaded " + Path.GetFileName(file));
            }
        }
        catch (InvalidDataException) { card.Fail("This isn't a valid zip. The download may be incomplete."); }
        catch (Exception ex) { card.Fail("Couldn't finish: " + ex.Message); }
    }

    public static async Task<bool> Get(DesktopForm sh, string url, string dest, string title, bool asApp = false, string? icon = null)
    {
        var cts = new CancellationTokenSource(); var card = sh.NewDl(title, cts, icon ?? IconKey(dest));
        try { await Net.Download(url, dest, card.Report, cts.Token); await Finish(sh, card, dest, asApp); return true; }
        catch (OperationCanceledException) { card.Fail("Cancelled"); return false; }
        catch (Exception ex) { card.Fail(ex.Message); return false; }
    }
}

class UpdateDialog : Form
{
    readonly Label title = new(), ver = new(), pct = new(), detail = new(); readonly LineProgress bar = new();
    readonly RoundButton go = new(), later = new();
    public bool Accepted; TaskCompletionSource<bool> choice = new();
    public UpdateDialog(int from, int to, bool ask)
    {
        FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = false; TopMost = true; BackColor = Ui.Surface; Size = Ui.D(500, 290); DoubleBuffered = true;
        title.Text = ask ? "Update available" : "Updating USB Desktop"; title.Font = Ui.F(16, true); title.ForeColor = Ui.Text; title.BackColor = Color.Transparent; title.SetBounds(Ui.D(28), Ui.D(26), Ui.D(440), Ui.D(34));
        ver.Text = $"Version 1.0.{from}  →  1.0.{to}"; ver.Font = Ui.F(10); ver.ForeColor = Ui.Sub; ver.BackColor = Color.Transparent; ver.SetBounds(Ui.D(30), Ui.D(62), Ui.D(440), Ui.D(26));
        pct.Font = Ui.F(30, true); pct.ForeColor = Ui.Text; pct.BackColor = Color.Transparent; pct.TextAlign = ContentAlignment.MiddleLeft; pct.SetBounds(Ui.D(28), Ui.D(112), Ui.D(200), Ui.D(56));
        detail.Font = Ui.F(9.5f); detail.ForeColor = Ui.Sub; detail.BackColor = Color.Transparent; detail.TextAlign = ContentAlignment.MiddleRight; detail.AutoEllipsis = true; detail.SetBounds(Ui.D(200), Ui.D(122), Ui.D(272), Ui.D(40));
        bar.SetBounds(Ui.D(28), Ui.D(176), Ui.D(444), Ui.D(10)); bar.Height = Ui.D(10);
        go.Text = "Update now"; go.Pill = true; go.BackColor = Ui.Accent; go.ForeColor = Color.White; go.SetBounds(Ui.D(250), Ui.D(216), Ui.D(222), Ui.D(44));
        later.Text = "Later"; later.Pill = true; later.SetBounds(Ui.D(28), Ui.D(216), Ui.D(200), Ui.D(44));
        go.Click += (_, _) => { Accepted = true; choice.TrySetResult(true); }; later.Click += (_, _) => { choice.TrySetResult(false); Close(); };
        if (ask) { pct.Text = ""; detail.Text = "A newer version is ready. It only takes a minute and your files stay on the USB."; detail.TextAlign = ContentAlignment.TopLeft; detail.SetBounds(Ui.D(28), Ui.D(112), Ui.D(444), Ui.D(56)); bar.Visible = false; }
        else { go.Visible = later.Visible = false; bar.Indeterminate = true; pct.Text = "0%"; detail.Text = "Connecting…"; }
        Controls.AddRange(new Control[] { title, ver, pct, detail, bar, go, later });
        Resize += (_, _) => { using var p = U.Round(new Rectangle(0, 0, Width, Height), Ui.D(20)); var old = Region; Region = new Region(p); old?.Dispose(); };
        Paint += (_, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using var lg = new LinearGradientBrush(new Rectangle(0, 0, Width, Ui.D(4)), Ui.Accent, Ui.Accent2, 0f); e.Graphics.FillRectangle(lg, 0, 0, Width, Ui.D(4)); using var pn = new Pen(Ui.Line); e.Graphics.DrawRectangle(pn, 0, 0, Width - 1, Height - 1); };
    }
    public Task<bool> Ask() => choice.Task;
    public void StartProgress()
    {
        title.Text = "Updating USB Desktop"; go.Visible = later.Visible = false; bar.Visible = true; bar.Indeterminate = true;
        detail.TextAlign = ContentAlignment.MiddleRight; detail.SetBounds(Ui.D(200), Ui.D(122), Ui.D(272), Ui.D(40)); pct.Text = "0%"; detail.Text = "Connecting…";
    }
    void UI(Action a) { if (IsDisposed) return; if (InvokeRequired) BeginInvoke(a); else a(); }
    public void Report(long got, long total, double speed) => UI(() =>
    {
        if (speed < 0) { detail.Text = "Connection dropped. Reconnecting…"; bar.Indeterminate = true; return; }
        if (total > 0) { double p = Math.Min(100, got * 100.0 / total); bar.Indeterminate = false; bar.Value = p; pct.Text = (int)p + "%"; detail.Text = $"{Ui.Bytes(got)} of {Ui.Bytes(total)}\n{Ui.Bytes((long)speed)}/s" + (speed > 1 && total > got ? "  ·  " + Ui.Eta(TimeSpan.FromSeconds((total - got) / speed)) : ""); }
        else { bar.Indeterminate = true; detail.Text = $"{Ui.Bytes(got)} downloaded\n{Ui.Bytes((long)speed)}/s"; }
    });
    public void Stage(string text, int percent = -1) => UI(() => { detail.Text = text; if (percent >= 0) { bar.Indeterminate = false; bar.Value = percent; pct.Text = percent + "%"; } });
    public void Fail(string text) => UI(() => { title.Text = "Update didn't finish"; bar.Indeterminate = false; bar.Fill1 = Color.FromArgb(239, 68, 68); bar.Fill2 = Color.FromArgb(251, 146, 120); pct.Text = "!"; detail.Text = text; later.Text = "Close"; later.Visible = true; later.SetBounds(Ui.D(28), Ui.D(216), Ui.D(444), Ui.D(44)); });
}

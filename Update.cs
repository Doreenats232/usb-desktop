using System.Diagnostics;
using System.Text.Json;
namespace UsbDesktop;

static class Updater
{
    const string Repo = "Doreenats232/usb-desktop";
    public static int Cur => typeof(Updater).Assembly.GetName().Version?.Build ?? 0;
    public static async Task<(int latest, string url)> Check()
    {
        var j = JsonDocument.Parse(await Net.Text($"https://api.github.com/repos/{Repo}/releases/latest")).RootElement;
        var tag = j.GetProperty("tag_name").GetString() ?? "v0"; int n = int.Parse(tag[(tag.LastIndexOf('.') + 1)..]);
        foreach (var a in j.GetProperty("assets").EnumerateArray())
            if (a.GetProperty("name").GetString() == "Launcher.exe") return (n, a.GetProperty("browser_download_url").GetString()!);
        return (0, "");
    }

    // Shows the premium update dialog (ask first when `ask`), downloads with live percentage, then swaps the exe and restarts.
    public static async Task Run(int latest, string url, bool ask)
    {
        using var dlg = new UpdateDialog(Cur, latest, ask); dlg.Show();
        if (ask) { if (!await dlg.Ask()) return; dlg.StartProgress(); }
        var tmp = Path.Combine(Paths.Root, "Launcher.new");
        try
        {
            await Net.Download(url, tmp, dlg.Report, CancellationToken.None);
            dlg.Stage("Checking the download…", 100);
            var fi = new FileInfo(tmp); bool ok = fi.Length > 5_000_000;
            if (ok) { using var s = File.OpenRead(tmp); ok = s.ReadByte() == 'M' && s.ReadByte() == 'Z'; }
            if (!ok) { try { File.Delete(tmp); } catch { } dlg.Fail("The downloaded file was damaged. Please try again."); await Task.Delay(1000); dlg.Visible = true; await WaitClosed(dlg); return; }
            dlg.Stage("Installing… restarting in a moment", 100); await Task.Delay(900);
            var exe = Path.GetFileName(Environment.ProcessPath)!;
            File.WriteAllText(Path.Combine(Paths.Root, "update.bat"),
                "@echo off\r\ncd /d \"%~dp0\"\r\n:retry\r\nset /a n+=1\r\nping -n 2 127.0.0.1 >nul\r\nmove /y \"Launcher.new\" \"" + exe + "\" >nul 2>&1 && goto done\r\nif %n% lss 40 goto retry\r\n:done\r\nstart \"\" \"" + exe + "\"\r\n(goto) 2>nul & del \"%~f0\"\r\n");
            Process.Start(new ProcessStartInfo("cmd.exe", "/c update.bat") { WorkingDirectory = Paths.Root, CreateNoWindow = true, UseShellExecute = false });
            Application.Exit(); await Task.Delay(1500); Environment.Exit(0);
        }
        catch (Exception ex) { dlg.Fail(ex.Message); await WaitClosed(dlg); }
    }
    static async Task WaitClosed(Form f) { while (!f.IsDisposed && f.Visible) await Task.Delay(200); }
}

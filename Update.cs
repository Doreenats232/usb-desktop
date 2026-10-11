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
    public static async Task Apply(string url, Action<long, long, double> progress)
    {
        var exe = Path.GetFileName(Environment.ProcessPath)!; var tmp = Path.Combine(Paths.Root, "Launcher.new");
        await Net.Download(url, tmp, progress, CancellationToken.None);
        File.WriteAllText(Path.Combine(Paths.Root, "update.bat"), "@echo off\r\ntimeout /t 2 /nobreak >nul\r\nmove /y \"Launcher.new\" \"" + exe + "\" >nul\r\nstart \"\" \"" + exe + "\"\r\ndel \"%~f0\"\r\n");
        Process.Start(new ProcessStartInfo("cmd.exe", "/c update.bat") { WorkingDirectory = Paths.Root, CreateNoWindow = true, UseShellExecute = false });
        Application.Exit();
    }
}

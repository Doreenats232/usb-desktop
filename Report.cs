using System.Text;
namespace UsbDesktop;

// One block of text you can paste to get a problem fixed: version, Windows, a few safe settings and the end of the error log. Secrets are never included.
static class Report
{
    static readonly string[] Show = { "wp", "light", "accent", "idle", "pet", "pet_outfit", "vm_distro", "usbwatch", "ai_speak", "ai_voice", "ai_rate", "ai_ask" };
    public static string Make()
    {
        var sb = new StringBuilder();
        sb.AppendLine("USB Desktop problem report"); sb.AppendLine("Version 1.0." + Updater.Cur + "   " + DateTime.Now);
        sb.AppendLine("Windows: " + Environment.OSVersion.VersionString + (Environment.Is64BitOperatingSystem ? " 64-bit" : " 32-bit") + "   CPUs: " + Environment.ProcessorCount);
        var scr = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 0, 0); sb.AppendLine("Screen: " + scr.Width + "x" + scr.Height);
        sb.AppendLine("Running from: " + Paths.Root + "   Previous version kept: " + Updater.HasPrevious);
        sb.AppendLine("Accounts connected: " + string.Join(", ", new[] { "spotify", "google" }.Where(Accounts.Connected)));
        sb.AppendLine("\nSettings:"); foreach (var k in Show) { var v = Cfg.Get(k); if (v != "") sb.AppendLine("  " + k + " = " + v); }
        sb.AppendLine("\nEnd of error log:");
        try { var f = Path.Combine(Paths.Data, "error.log"); var t = File.Exists(f) ? File.ReadAllText(f) : "(no errors logged)"; sb.AppendLine(t.Length > 6000 ? "…" + t[^6000..] : t); } catch (Exception ex) { sb.AppendLine("(couldn't read it: " + ex.Message + ")"); }
        return sb.ToString();
    }
}

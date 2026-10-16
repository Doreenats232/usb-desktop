namespace UsbDesktop;

// School mode: leaves as little as possible on the PC. Everything stays on the USB.
static class School
{
    public static bool On => Cfg.Get("school", "0") == "1";
    static string PcDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UsbDesktop");

    // Folders the app may write to. In school mode the PC's own profile folder is never used.
    public static string[] Dirs(string usb, string sub) => On ? new[] { usb } : new[] { usb, Path.Combine(PcDir, sub) };

    public static void Cleanup(bool force = false)
    {
        if (!On && !force) return;
        try { Clipboard.Clear(); } catch { }
        try { if (Directory.Exists(PcDir)) Directory.Delete(PcDir, true); } catch { }
        try
        {
            var t = Path.GetTempPath();
            foreach (var f in Directory.GetFiles(t, "UsbDesktop*")) { try { File.Delete(f); } catch { } }
            foreach (var d in Directory.GetDirectories(t, "UsbDesktop*")) { try { Directory.Delete(d, true); } catch { } }
        }
        catch { }
    }
}

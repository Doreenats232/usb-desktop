using System.Runtime.InteropServices;
namespace UsbDesktop;

// The browser engine's small native loader is stored inside Launcher.exe and unpacked on first run,
// so the exe never needs the "self-extract" step that fails on some PCs.
static class Native
{
    static IntPtr h; public static string? Error;
    public static void Prepare()
    {
        try
        {
            using var s = typeof(Program).Assembly.GetManifestResourceStream("WebView2Loader.x86.dll");
            if (s == null) return;
            var dirs = new[] { Path.Combine(Paths.Data, "runtime"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UsbDesktop", "runtime") };
            foreach (var d in dirs)
            {
                try
                {
                    Directory.CreateDirectory(d); var f = Path.Combine(d, "WebView2Loader.dll");
                    if (!File.Exists(f) || new FileInfo(f).Length != s.Length) { s.Position = 0; using var o = File.Create(f); s.CopyTo(o); }
                    h = NativeLibrary.Load(f); Error = null; break;
                }
                catch (Exception ex) { Error = ex.Message; }
            }
            if (h != IntPtr.Zero)
                NativeLibrary.SetDllImportResolver(typeof(Microsoft.Web.WebView2.Core.CoreWebView2Environment).Assembly, (name, a, p) => name.StartsWith("WebView2Loader", StringComparison.OrdinalIgnoreCase) ? h : IntPtr.Zero);
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
namespace UsbDesktop;

// "Seeing" the screen for the assistant: which windows are open on this PC (titles and app names), what is in front, and what is on the USB Desktop.
static class ScreenSee
{
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    struct RECT { public int L, T, R, B; }

    public static string Look(DesktopForm sh)
    {
        var fg = GetForegroundWindow(); var lines = new List<string>();
        EnumWindows((h, _) =>
        {
            if (lines.Count >= 25 || !IsWindowVisible(h)) return true;
            var sb = new StringBuilder(256); GetWindowText(h, sb, 256); var t = sb.ToString().Trim(); if (t == "" || t == "Program Manager") return true;
            GetWindowRect(h, out var r); bool min = IsIconic(h); if (!min && (r.R - r.L < 60 || r.B - r.T < 40)) return true;
            GetWindowThreadProcessId(h, out var pid); string app = ""; try { app = Process.GetProcessById((int)pid).ProcessName; } catch { }
            lines.Add((h == fg ? "IN FRONT: " : min ? "(minimized) " : "") + t + (app != "" ? "  [" + app + "]" : "") + (min ? "" : "  " + (r.R - r.L) + "x" + (r.B - r.T)));
            return true;
        }, IntPtr.Zero);
        var scr = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 0, 0);
        return "Screen " + scr.Width + "x" + scr.Height + ". Windows open on this PC, front to back:\n" + (lines.Count == 0 ? "(none visible)" : string.Join("\n", lines)) + "\n\nOn the USB Desktop:\n" + sh.DesktopSnapshot() + "\n\n(I can see window titles and app names, not the pixels. Use the page tag to read the Browser page.)";
    }
}

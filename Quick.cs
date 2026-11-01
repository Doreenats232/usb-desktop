using System.Runtime.InteropServices;
namespace UsbDesktop;

// Quick actions from the phone app's grid (lock, volume, status, screen...). They run directly, no AI in the middle, so they are fast and reliable.
static class Quick
{
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    static void Key(byte vk) { keybd_event(vk, 0, 0x1, UIntPtr.Zero); keybd_event(vk, 0, 0x1 | 0x2, UIntPtr.Zero); }
    static readonly HashSet<string> Apps = new() { "files", "notepad", "calc", "paint", "term", "web", "store", "ai", "settings", "games", "tm", "vault" };

    // Runs on the UI thread. Returns the reply text; a screenshot is attached through ctx.
    public static string Run(DesktopForm sh, string cmd, PhoneLink.Ctx ctx)
    {
        var parts = cmd.Trim().TrimStart('/').Split(' ', 2); var k = parts[0].ToLowerInvariant(); var a = parts.Length > 1 ? parts[1].Trim() : "";
        try
        {
            switch (k)
            {
                case "lock": return Act(sh, "lock", "");
                case "sleep": return Act(sh, "sleep", "");
                case "eject": return Act(sh, "eject", "");
                case "undo": return Act(sh, "undo", "");
                case "tidy": return Act(sh, "arrange", "");
                case "sort": return Act(sh, "sort", a);
                case "vol": return SysVolume.Do(a);
                case "open":
                    {
                        var app = a.ToLowerInvariant();
                        if (!Apps.Contains(app)) return "I can't open \"" + a + "\" from the phone.";
                        sh.Launch(app); return "Opened " + app + " on the USB Desktop.";
                    }
                case "media":
                    switch (a.ToLowerInvariant())
                    {
                        case "play": case "pause": case "toggle": Key(0xB3); return "Play / pause sent.";
                        case "next": Key(0xB0); return "Next track.";
                        case "prev": case "previous": Key(0xB1); return "Previous track.";
                        case "stop": Key(0xB2); return "Stopped.";
                        default: return "Media actions are play, next, prev, stop.";
                    }
                case "clipget":
                    {
                        if (!Clipboard.ContainsText()) return "📋 (the PC clipboard has no text)";
                        var t = Clipboard.GetText(); if (t.Length > 1800) t = t[..1800] + "…"; return "📋 " + t;
                    }
                case "clipset":
                    {
                        if (a == "") return "Nothing to copy.";
                        Clipboard.SetText(a); return "Copied to the PC clipboard (" + a.Length + " characters).";
                    }
                case "study": _ = PhoneLink.PostStudy(); return "Study list refreshed.";
                case "hw": { var r = Study.PhoneAction("hw", a); _ = PhoneLink.PostStudy(); _ = PhoneLink.PostEvents(); return r; }
                case "lesson": { var r = Study.PhoneAction("lesson", a); _ = PhoneLink.PostStudy(); _ = PhoneLink.PostEvents(); return r; }
                case "clips": return ClipHist.Run("list");
                case "clipuse": return ClipHist.Run("use " + a);
                case "status": return Status(sh);
                case "screen":
                    {
                        var jpg = sh.ShotJpeg(); if (jpg == null) return "I couldn't capture the screen.";
                        ctx.AttachName = "screen.jpg"; ctx.AttachBytes = jpg; return "Here is your PC screen right now.";
                    }
                default: return "I don't know the quick action \"" + k + "\".";
            }
        }
        catch (Exception ex) { return "That didn't work: " + ex.Message; }
    }

    static string Act(DesktopForm sh, string k, string a)
    {
        var r = AiActions.RunAll(sh, new List<(string k, string a)> { (k, a) });
        return r.Count > 0 ? r[0] : "Done.";
    }

    static string Status(DesktopForm sh)
    {
        var sb = new System.Text.StringBuilder("📊 PC status\n");
        foreach (var line in AiActions.SysInfo().Split('\n'))
        {
            var l = line.Trim(); if (l == "" || l.StartsWith("OS:") || l.StartsWith("CPU")) continue; sb.AppendLine(l);
        }
        var v = SysVolume.Get(); if (v >= 0) sb.AppendLine("Volume: " + v + "%");
        var ow = sh.DesktopSnapshot().Split('\n').FirstOrDefault(x => x.StartsWith("Open windows:")); if (ow != null) sb.AppendLine(ow);
        sb.AppendLine("Time: " + DateTime.Now.ToString("ddd HH:mm"));
        return sb.ToString().Trim();
    }
}

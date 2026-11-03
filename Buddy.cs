using System.Runtime.InteropServices;
namespace UsbDesktop;

// Makes the walking character react to things: greets you, naps when you're away, welcomes you back, warns on low battery.
static class Buddy
{
    [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO p);

    static bool greeted, asleep, lastFull; static DateTime nextBattery = DateTime.MinValue, started = DateTime.Now;

    static int IdleSeconds()
    {
        var li = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref li)) return 0;
        return (int)((uint)Environment.TickCount - li.dwTime) / 1000;
    }

    public static void Start(DesktopForm sh)
    {
        var t = new System.Windows.Forms.Timer { Interval = 5000 };
        t.Tick += (_, _) =>
        {
            try
            {
                bool full = PhoneLink.FullAccess; if (lastFull && !full) { Pet.React("🔒 Full access ended", 6); _ = PhoneLink.PostState(); } lastFull = full;
                if (Cfg.Get("buddy_react", "1") != "1") return;
                var now = DateTime.Now;
                if (!greeted && (now - started).TotalSeconds > 6)
                {
                    greeted = true; var h = now.Hour;
                    Pet.React((h < 12 ? "Good morning" : h < 17 ? "Good afternoon" : "Good evening") + ", Boss! 👋", 6, true);
                }
                int idle = IdleSeconds();
                if (!asleep && idle > 600) { asleep = true; Pet.React("💤 Zzz… I'll nap until you're back.", 8); }
                else if (asleep && idle < 5) { asleep = false; Pet.React("Welcome back, Boss! ☀️", 5, true); }
                var ps = SystemInformation.PowerStatus;
                if (!ps.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) && ps.PowerLineStatus == PowerLineStatus.Offline && ps.BatteryLifePercent <= 0.15f && now >= nextBattery)
                {
                    nextBattery = now.AddMinutes(30); Pet.React("🔋 Battery is at " + (int)(ps.BatteryLifePercent * 100) + "%. Plug in soon!", 9);
                }
            }
            catch { }
        };
        t.Start();
    }
}

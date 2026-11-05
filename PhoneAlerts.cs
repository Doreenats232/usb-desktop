namespace UsbDesktop;

// Watches the PC for things worth telling your phone about. (Downloads finishing and backups failing send their own alerts.)
static class PhoneAlerts
{
    static bool warned;
    public static void Start(DesktopForm sh)
    {
        var t = new System.Windows.Forms.Timer { Interval = 60000 };
        t.Tick += (_, _) =>
        {
            try
            {
                var ps = SystemInformation.PowerStatus;
                if ((ps.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) != 0) return;   // a desktop PC
                float pct = ps.BatteryLifePercent; if (pct < 0 || pct > 1) return;                   // unknown
                if (ps.PowerLineStatus == PowerLineStatus.Online || pct > 0.3f) { warned = false; return; }
                if (!warned && pct <= 0.2f) { warned = true; PhoneLink.Alert("🔋 PC battery low", Math.Round(pct * 100) + "% left and it isn't charging"); }
            }
            catch { }
        };
        t.Start();
    }
}

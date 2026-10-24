using System.Runtime.InteropServices;
namespace UsbDesktop;

// Reads how loud the PC's speakers are right now (the same meter the Windows volume mixer shows) so the buddy can dance when music plays.
static class AudioMeter
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCom { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator { int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices); int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device); }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice { int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface); }
    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioMeterInformation { int GetPeakValue(out float peak); }
    static IAudioMeterInformation? meter; static int fails; static DateTime refreshAt = DateTime.MinValue;
    // 0..1, or -1 when the meter can't be read on this PC
    public static float Peak()
    {
        if (fails > 5) return -1f;
        try
        {
            if (meter == null || DateTime.Now > refreshAt)
            {
                var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom(); en.GetDefaultAudioEndpoint(0, 1, out var dev);
                var iid = typeof(IAudioMeterInformation).GUID; dev.Activate(ref iid, 23, IntPtr.Zero, out var o); meter = (IAudioMeterInformation)o; refreshAt = DateTime.Now.AddSeconds(60);
            }
            meter.GetPeakValue(out var p); return p;
        }
        catch { meter = null; fails++; return -1f; }
    }
}

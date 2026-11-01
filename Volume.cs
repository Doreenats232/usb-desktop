using System.Runtime.InteropServices;
namespace UsbDesktop;

// Sets the PC's master volume (what the Windows volume slider controls) so the assistant can turn it up, down, mute or set a number.
static class SysVolume
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCom { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator { int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices); int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device); }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice { int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface); }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify); int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint count);
        int SetMasterVolumeLevel(float db, ref Guid ctx); int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        int GetMasterVolumeLevel(out float db); int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint ch, float db, ref Guid ctx); int SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
        int GetChannelVolumeLevel(uint ch, out float db); int GetChannelVolumeLevelScalar(uint ch, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx); int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    static IAudioEndpointVolume Endpoint()
    {
        var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom(); en.GetDefaultAudioEndpoint(0, 1, out var dev);
        var iid = typeof(IAudioEndpointVolume).GUID; dev.Activate(ref iid, 23, IntPtr.Zero, out var o); return (IAudioEndpointVolume)o;
    }

    // arg: a number 0-100, "up", "down", "mute" or "unmute"
    public static string Do(string arg)
    {
        try
        {
            var v = Endpoint(); var ctx = Guid.Empty; arg = arg.Trim().ToLowerInvariant();
            v.GetMasterVolumeLevelScalar(out var cur);
            if (arg == "mute") { v.SetMute(true, ref ctx); return "PC volume muted."; }
            if (arg == "unmute") { v.SetMute(false, ref ctx); return "PC volume unmuted."; }
            float target;
            if (arg == "up") target = cur + 0.1f;
            else if (arg == "down") target = cur - 0.1f;
            else if (int.TryParse(arg.TrimEnd('%', ' '), out var n)) target = n / 100f;
            else return "ERR: use a number from 0 to 100, or up, down, mute, unmute.";
            target = Math.Clamp(target, 0f, 1f);
            if (target > 0f) v.SetMute(false, ref ctx);
            v.SetMasterVolumeLevelScalar(target, ref ctx);
            return "PC volume is now " + (int)Math.Round(target * 100) + "%.";
        }
        catch (Exception ex) { return "ERR: I couldn't change the PC volume (" + ex.Message + ")."; }
    }
}

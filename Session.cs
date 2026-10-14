using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace UsbDesktop;

class WinState
{
    public string Kind { get; set; } = ""; public string? Arg { get; set; } public List<string>? Tabs { get; set; }
    public int X { get; set; } public int Y { get; set; } public int W { get; set; } public int H { get; set; } public bool Max { get; set; } public bool Hidden { get; set; } public string? Data { get; set; }
}

class StateApi { public Func<string?> Get = () => null; public Action<string> Set = _ => { }; }

class BrowserApi { public Func<List<string>> Urls = () => new(); public Action<string> Open = _ => { }; }

// "Sleep": the open windows are saved (encrypted with a key made from your login password) and restored at the next login, on any PC.
static class Session
{
    public static byte[]? Key; static bool saved;
    static string F => Path.Combine(Paths.Data, "sleep.dat"); static string SaltFile => Path.Combine(Paths.Data, "sleep.salt");
    public static bool Exists => File.Exists(F);
    public static void SetKey(string pw)
    {
        try
        {
            byte[] s; if (File.Exists(SaltFile)) s = File.ReadAllBytes(SaltFile); else { s = RandomNumberGenerator.GetBytes(16); File.WriteAllBytes(SaltFile, s); }
            Key = U.Pbkdf2(Encoding.UTF8.GetBytes(pw), s, 100000);
        }
        catch { }
    }
    public static bool Save(List<WinState> l)
    {
        if (Key == null) return false;
        try
        {
            var plain = JsonSerializer.SerializeToUtf8Bytes(l); var n = RandomNumberGenerator.GetBytes(12); var ct = new byte[plain.Length]; var tag = new byte[16];
            using (var a = new AesGcm(Key, 16)) a.Encrypt(n, plain, ct, tag);
            File.WriteAllBytes(F, n.Concat(tag).Concat(ct).ToArray()); saved = true; return true;
        }
        catch { return false; }
    }
    public static bool AlreadySaved => saved;
    public static List<WinState>? Load()
    {
        try
        {
            if (Key == null || !Exists) return null; var d = File.ReadAllBytes(F); var pt = new byte[d.Length - 28];
            using var a = new AesGcm(Key, 16); a.Decrypt(d.AsSpan(0, 12), d.AsSpan(28), d.AsSpan(12, 16), pt);
            return JsonSerializer.Deserialize<List<WinState>>(pt);
        }
        catch { return null; }
    }
    public static void Clear() { try { File.Delete(F); } catch { } saved = false; }
}

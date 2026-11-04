using System.Text;
namespace UsbDesktop;

// One button that tests the main features and says what is broken. It only reads and pings: nothing is changed or uploaded.
static class HealthCheck
{
    static async Task<(int lvl, string msg)> Timed(Func<CancellationToken, Task<(int, string)>> f, int sec)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(sec));
        try
        {
            var t = f(cts.Token); var done = await Task.WhenAny(t, Task.Delay(TimeSpan.FromSeconds(sec + 2)));
            if (done != t) return (2, "took too long (over " + sec + " seconds)");
            return await t;
        }
        catch (OperationCanceledException) { return (2, "took too long (over " + sec + " seconds)"); }
        catch (Exception ex) { return (2, ex.Message.Length > 120 ? ex.Message[..120] + "…" : ex.Message); }
    }

    static async Task<(int, string)> Reach(string url, CancellationToken ct, string okText)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await Net.Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        int code = (int)resp.StatusCode;
        return code < 500 ? (0, okText) : (2, "the server answered with an error (HTTP " + code + ")");
    }

    static string Ago(string ticksKey)
    {
        if (!long.TryParse(Cfg.Get(ticksKey), out var t) || t <= 0) return "never yet";
        var m = (int)(DateTime.UtcNow - new DateTime(t, DateTimeKind.Utc)).TotalMinutes;
        return m < 120 ? Math.Max(0, m) + " min ago" : m < 2880 ? m / 60 + " h ago" : m / 1440 + " days ago";
    }

    public static async Task<string> Run(DesktopForm sh, Action<string> say)
    {
        var lines = new List<string>(); int bad = 0, warn = 0;
        async Task Step(string name, Func<CancellationToken, Task<(int, string)>> f, int sec = 20)
        {
            say("Checking " + name + "…"); var (lvl, msg) = await Timed(f, sec);
            lines.Add((lvl == 0 ? "✅ " : lvl == 1 ? "⚠️ " : "❌ ") + name + ": " + msg);
            if (lvl == 2) bad++; else if (lvl == 1) warn++;
        }
        void Add(int lvl, string name, string msg)
        {
            lines.Add((lvl == 0 ? "✅ " : lvl == 1 ? "⚠️ " : "❌ ") + name + ": " + msg);
            if (lvl == 2) bad++; else if (lvl == 1) warn++;
        }

        // the USB itself
        say("Checking the USB…");
        try
        {
            var di = new DriveInfo(Path.GetPathRoot(Paths.Root)!); double gb = di.AvailableFreeSpace / 1073741824.0;
            Add(gb < 0.2 ? 2 : gb < 1 ? 1 : 0, "USB space", gb.ToString("0.0") + " GB free");
        }
        catch (Exception ex) { Add(1, "USB space", "couldn't read it (" + ex.Message + ")"); }
        try
        {
            Directory.CreateDirectory(Paths.Data); var tmp = Path.Combine(Paths.Data, "healthcheck.tmp");
            File.WriteAllText(tmp, "ok"); File.Delete(tmp); Add(0, "Saving to the USB", "works");
        }
        catch (Exception ex) { Add(2, "Saving to the USB", "can't write to it (is it locked or full?) " + ex.Message); }
        try
        {
            var el = Path.Combine(Paths.Data, "error.log");
            if (File.Exists(el) && new FileInfo(el).Length > 0 && (DateTime.Now - File.GetLastWriteTime(el)).TotalHours < 24) Add(1, "Error log", "new errors in the last day (Settings → About → Copy problem report)");
        }
        catch { }

        await Step("Internet", ct => Reach("https://www.gstatic.com/generate_204", ct, "connected"), 12);

        await Step("Phone link", ct =>
        {
            if (Cfg.Get("phone_on", "0") != "1") return Task.FromResult((1, "is off (turn it on in Assistant → Phone)"));
            var st = PhoneLink.Status;
            return Task.FromResult(st == "Connected" ? (0, "connected to your relay") : (2, st));
        });
        lines.Add("ℹ️ Remote touchpad: " + (RemoteInput.Enabled ? "allowed" : "off (tick it in Assistant → Phone to use it)") + "   Full access: " + (PhoneLink.FullAccess ? "ON" : "off"));

        await Step("AI (RelayAPI)", async ct =>
        {
            if (Cfg.Get("ai_key") == "") return (2, "no API key yet (Assistant → Settings)");
            var url = Cfg.Get("ai_url", "https://relayapi-c5ah.onrender.com").Trim().TrimEnd('/'); if (url == "") return (2, "no server address");
            var r = await Reach(url, ct, "key is set and the server answers"); return r;
        }, 45);

        await Step("TeraBox vault", async ct =>
        {
            if (!VaultBackup.Ready) return (1, "not set up (Vault → Connection)");
            var l = await VaultBackup.Listing(ct); return (0, "your key works · " + l.Count + " file(s) in your project");
        }, 45);
        lines.Add("ℹ️ Last data backup: " + Ago("data_auto_last") + (Cfg.Get("data_auto", "1") == "1" ? " (automatic is on)" : " (automatic is off)"));

        await Step("Voice and microphone", ct => Task.Run<(int, string)>(() =>
        {
            int voices = 0, recs = 0; string mic = "";
            try { using var s = new System.Speech.Synthesis.SpeechSynthesizer(); voices = s.GetInstalledVoices().Count(v => v.Enabled); } catch { }
            try
            {
                var all = System.Speech.Recognition.SpeechRecognitionEngine.InstalledRecognizers(); recs = all.Count;
                if (recs > 0) { try { using var probe = new System.Speech.Recognition.SpeechRecognitionEngine(all[0]); probe.SetInputToDefaultAudioDevice(); } catch (Exception ex) { mic = ex.Message; } }
            }
            catch { }
            if (voices == 0) return (2, "no speaking voice installed (Windows Settings → Time & language → Speech)");
            if (recs == 0) return (1, "can speak, but there is no speech recognizer for listening");
            if (mic != "") return (1, "can speak, but the microphone isn't available (" + (mic.Length > 70 ? mic[..70] : mic) + ")");
            return (0, "can speak and listen");
        }), 15);

        await Step("Screen text reading (OCR)", async ct =>
        {
            var t = await Ocr.Run(sh, "screen");
            return t.StartsWith("ERR:") ? (2, t[4..].Trim()) : (0, "works");
        }, 50);

        await Step("Updates", async ct =>
        {
            var u = await Updater.Check();
            return u.latest > Updater.Cur ? (1, "version 1.0." + u.latest + " is available (you have 1.0." + Updater.Cur + ")") : (0, "up to date (version 1.0." + Updater.Cur + ")");
        }, 25);

        var head = bad == 0 && warn == 0 ? "🟢 Everything looks good" : bad > 0 ? "🔴 " + bad + " problem" + (bad == 1 ? "" : "s") + (warn > 0 ? " and " + warn + " warning" + (warn == 1 ? "" : "s") : "") : "🟡 " + warn + " warning" + (warn == 1 ? "" : "s");
        return head + "\n\n" + string.Join("\n", lines);
    }
}

using System.Media;
using System.Speech.Recognition;
namespace UsbDesktop;

// Hands-free wake word: say "Hey Morales" (any time USB Desktop is open) and then your request. Off by default, because it keeps the microphone open.
static class WakeWord
{
    static SpeechRecognitionEngine? rec;
    static DesktopForm? shell;
    static bool awake; static DateTime awakeUntil;
    static System.Windows.Forms.Timer? clock;

    public static void Start(DesktopForm sh) { shell = sh; if (Cfg.Get("wake_on", "0") == "1") Begin(); }

    public static string Set(string a)
    {
        a = a.Trim().ToLowerInvariant();
        if (a == "toggle") a = Cfg.Get("wake_on", "0") == "1" ? "off" : "on";
        if (a == "off") { Cfg.Set("wake_on", "0"); Stop(); return "Wake word is off. The microphone is closed."; }
        if (a == "on")
        {
            Cfg.Set("wake_on", "1");
            var err = Begin(); return err ?? "Wake word is on. Say \"Hey " + Cfg.Get("ai_name", "Morales") + "\" and then what you want. (The microphone stays open while this is on.)";
        }
        return "Wake word is " + (Cfg.Get("wake_on", "0") == "1" ? "on" : "off") + ".";
    }

    static void Stop()
    {
        try { clock?.Stop(); } catch { }
        try { if (rec != null) { rec.RecognizeAsyncCancel(); rec.Dispose(); } } catch { }
        rec = null; awake = false;
    }

    static Grammar WakeGrammar()
    {
        var n = Cfg.Get("ai_name", "Morales");
        var g = new Grammar(new GrammarBuilder(new Choices("hey " + n, "ok " + n, "okay " + n, "hi " + n, n))) { Name = "wake" };
        return g;
    }

    static string? Begin()
    {
        Stop();
        try
        {
            var infos = SpeechRecognitionEngine.InstalledRecognizers();
            if (infos.Count == 0) { Cfg.Set("wake_on", "0"); return "Windows has no speech recognizer installed (Settings > Time & language > Speech)."; }
            var info = infos.FirstOrDefault(i => i.Culture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase)) ?? infos[0];
            rec = new SpeechRecognitionEngine(info);
            rec.SetInputToDefaultAudioDevice();
            rec.LoadGrammar(WakeGrammar());
            rec.SpeechRecognized += (_, e) => Heard(e);
            rec.RecognizeAsync(RecognizeMode.Multiple);
            clock = new System.Windows.Forms.Timer { Interval = 1000 };
            clock.Tick += (_, _) => { if (awake && DateTime.Now > awakeUntil) Sleep(); };
            clock.Start();
            return null;
        }
        catch (Exception ex) { Cfg.Set("wake_on", "0"); Stop(); return "I couldn't start listening: " + ex.Message; }
    }

    static void Sleep()
    {
        try { if (rec == null) return; awake = false; rec.UnloadAllGrammars(); rec.LoadGrammar(WakeGrammar()); } catch { }
    }

    static void Heard(SpeechRecognizedEventArgs e)
    {
        var sh = shell; if (sh == null || rec == null) return;
        try
        {
            if (!awake)
            {
                if (e.Result.Confidence < 0.7f) return;
                awake = true; awakeUntil = DateTime.Now.AddSeconds(9);
                rec.UnloadAllGrammars(); rec.LoadGrammar(new DictationGrammar());
                SystemSounds.Asterisk.Play();
                sh.BeginInvoke(new Action(() => Pet.React("🎙 Yes, Boss?", 6)));
            }
            else
            {
                var t = e.Result.Text.Trim();
                if (e.Result.Confidence < 0.3f || t == "") return;
                Sleep();
                sh.BeginInvoke(new Action(async () =>
                {
                    try { if (!sh.DesktopSnapshot().Contains("AI Assistant")) sh.Launch("ai"); await Assistant.Converse(sh, t, true, "🎙 "); } catch { }
                }));
            }
        }
        catch { }
    }
}

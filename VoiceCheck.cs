using System.Speech.Recognition;
using System.Speech.Synthesis;
namespace UsbDesktop;

// A small window that tests the speaker and microphone, says exactly what is missing, and lets you pick the voice and speed.
static class VoiceCheck
{
    public static void Show()
    {
        using var f = new Form { Text = "Voice check", StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ClientSize = new Size(Ui.D(540), Ui.D(560)), BackColor = Ui.Surface, ForeColor = Ui.Text, Font = Ui.F(10) };
        int W = Ui.D(512), X = Ui.D(14);
        f.Controls.Add(new Label { Text = "Voice check", Left = X, Top = Ui.D(10), AutoSize = true, Font = Ui.F(13, true) });
        f.Controls.Add(new Label { Text = "Voice: On  =  it speaks every reply out loud.\nTalk  =  press once, speak, and wait; it keeps listening until you press Stop.", Left = X, Top = Ui.D(40), Width = W, Height = Ui.D(40), ForeColor = Ui.Sub });
        var report = new Label { Left = X, Top = Ui.D(86), Width = W, Height = Ui.D(150), BackColor = Ui.Surface2, Padding = new Padding(Ui.D(8)) }; f.Controls.Add(report);

        using var syn = new SpeechSynthesizer(); var voices = new List<string>(); var recs = new List<RecognizerInfo>(); string micMsg = "";
        try { voices = syn.GetInstalledVoices().Where(v => v.Enabled).Select(v => v.VoiceInfo.Name).ToList(); } catch { }
        try { recs = SpeechRecognitionEngine.InstalledRecognizers().ToList(); } catch { }
        if (recs.Count > 0)
        {
            try { using var probe = new SpeechRecognitionEngine(recs[0]); probe.SetInputToDefaultAudioDevice(); } catch (Exception ex) { micMsg = ex.Message; }
        }
        var sb = new System.Text.StringBuilder();
        sb.Append(voices.Count > 0 ? "✅  Speaker voice: " + string.Join(", ", voices) : "❌  No voice installed. Windows Settings > Time & language > Speech > Add voices.").Append("\n\n");
        sb.Append(recs.Count > 0 ? "✅  Speech recognizer: " + string.Join(", ", recs.Select(r => r.Name)) : "❌  No speech recognizer. Windows Settings > Time & language > Language & region > add English (US) with Speech.").Append("\n\n");
        sb.Append(recs.Count == 0 ? "❌  Microphone: can't be tested until a recognizer is installed." : micMsg == "" ? "✅  Microphone: found." : "❌  Microphone: " + micMsg + "  Check Windows Settings > Privacy & security > Microphone, and let desktop apps use it.");
        report.Text = sb.ToString();

        f.Controls.Add(new Label { Text = "Voice", Left = X, Top = Ui.D(246), AutoSize = true });
        var pick = new ComboBox { Left = X, Top = Ui.D(268), Width = W, DropDownStyle = ComboBoxStyle.DropDownList }; foreach (var v in voices) pick.Items.Add(v);
        var cur = Cfg.Get("ai_voice"); pick.SelectedIndex = voices.Count == 0 ? -1 : Math.Max(0, voices.IndexOf(cur)); f.Controls.Add(pick);
        f.Controls.Add(new Label { Text = "Speed  (left = slower, right = faster)", Left = X, Top = Ui.D(304), AutoSize = true });
        var rate = new TrackBar { Left = X, Top = Ui.D(326), Width = W, Minimum = -5, Maximum = 5, Value = Math.Clamp(Cfg.Int("ai_rate", 0), -5, 5), TickFrequency = 1 }; f.Controls.Add(rate);
        var testV = new Button { Text = "Test the voice", Left = X, Top = Ui.D(378), Width = Ui.D(160), Height = Ui.D(32) }; f.Controls.Add(testV);
        testV.Click += (_, _) =>
        {
            try { if (pick.SelectedIndex >= 0) syn.SelectVoice(voices[pick.SelectedIndex]); syn.Rate = rate.Value; syn.Volume = 100; syn.SetOutputToDefaultAudioDevice(); syn.SpeakAsyncCancelAll(); syn.SpeakAsync("Hi, I am your assistant. Can you hear me clearly?"); Cfg.Set("ai_voice", pick.SelectedIndex >= 0 ? voices[pick.SelectedIndex] : ""); Cfg.Set("ai_rate", rate.Value.ToString()); }
            catch (Exception ex) { MessageBox.Show("❌ " + ex.Message, "Voice check"); }
        };
        var lvl = new ProgressBar { Left = X, Top = Ui.D(430), Width = W, Height = Ui.D(16), Minimum = 0, Maximum = 100 }; f.Controls.Add(lvl);
        var heard = new Label { Left = X, Top = Ui.D(454), Width = W, Height = Ui.D(50), ForeColor = Ui.Sub, Text = "Press Test microphone, then say a sentence. The bar above should move when you talk." }; f.Controls.Add(heard);
        var testM = new Button { Text = "Test microphone", Left = X, Top = Ui.D(512), Width = Ui.D(160), Height = Ui.D(32), Enabled = recs.Count > 0 && micMsg == "" }; f.Controls.Add(testM);
        SpeechRecognitionEngine? eng = null;
        void StopMic() { try { eng?.RecognizeAsyncCancel(); eng?.Dispose(); } catch { } eng = null; testM.Text = "Test microphone"; lvl.Value = 0; }
        testM.Click += (_, _) =>
        {
            if (eng != null) { StopMic(); return; }
            try
            {
                eng = new SpeechRecognitionEngine(recs[0]); eng.LoadGrammar(new DictationGrammar()); eng.SetInputToDefaultAudioDevice();
                eng.AudioLevelUpdated += (_, e) => { try { f.BeginInvoke(new Action(() => lvl.Value = Math.Clamp(e.AudioLevel, 0, 100))); } catch { } };
                eng.SpeechRecognized += (_, e) => { try { f.BeginInvoke(new Action(() => heard.Text = "✅  I heard: " + e.Result.Text)); } catch { } };
                eng.SpeechRecognitionRejected += (_, _) => { try { f.BeginInvoke(new Action(() => heard.Text = "I heard sound but couldn't make out words. Speak clearly, closer to the microphone.")); } catch { } };
                eng.RecognizeAsync(RecognizeMode.Multiple); testM.Text = "Stop microphone test"; heard.Text = "Listening… say a sentence.";
            }
            catch (Exception ex) { heard.Text = "❌  " + ex.Message; StopMic(); }
        };
        f.FormClosed += (_, _) => StopMic();
        f.ShowDialog();
    }
}

using System.Text;
using System.Text.Json;
namespace UsbDesktop;

// The AI's hands on the PHONE: sends one step to the phone through the relay and returns what the phone says.
// It only works while the user has switched "AI phone control" on inside the phone app (the phone checks that itself).
static class PhoneCtl
{
    static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(60) };
    static readonly HashSet<string> Steps = new() { "open", "screen", "tap", "type", "swipe", "back", "home", "wait" };

    public static async Task<string> Run(string a)
    {
        var sp = a.Trim().Split(' ', 2); var cmd = sp[0].ToLowerInvariant(); var arg = sp.Length > 1 ? sp[1].Trim() : "";
        if (!Steps.Contains(cmd)) return "Unknown phone command \"" + cmd + "\". Use open, screen, tap, type, swipe, back, home or wait.";
        if (PhoneLink.BaseUrl == "") return "The phone link is off on this PC, so I can't reach the phone.";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, PhoneLink.BaseUrl + "/api/ctl") { Content = new StringContent(JsonSerializer.Serialize(new { cmd, arg }), Encoding.UTF8, "application/json") };
            req.Headers.Add("x-pair", PhoneLink.Pair);
            using var resp = await http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return "The phone didn't answer (error " + (int)resp.StatusCode + ").";
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("text", out var t) ? (t.GetString() ?? "") : "The phone sent no answer.";
        }
        catch (Exception ex) { return "I couldn't reach the phone: " + ex.Message; }
    }
}

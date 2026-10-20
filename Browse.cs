using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
namespace UsbDesktop;

// Lets the assistant drive the built-in Browser: scroll, navigate, play songs/videos, media keys, click, type, read the page.
// Safe by design: clicking and typing ask first (unless you turned on "trust"), buy/delete/sign-out style buttons ALWAYS ask,
// it never types into password fields, and reading the page text asks first.
static class BrowserCtl
{
    static readonly Regex Danger = new(@"\b(buy|pay|purchase|checkout|delete|transfer|send money|donate|place order|withdraw)\b", RegexOptions.IgnoreCase);

    static async Task<string> Js(CoreWebView2 c, string js)
    {
        var r = await c.ExecuteScriptAsync(js); if (r == "null" || r == "undefined") return "";
        if (r.StartsWith("\"")) { try { return JsonSerializer.Deserialize<string>(r) ?? ""; } catch { } }
        return r;
    }
    static async Task<bool> NavigateAndWait(CoreWebView2 c, string url, int ms = 15000)
    {
        var tcs = new TaskCompletionSource<bool>(); void H(object? s, CoreWebView2NavigationCompletedEventArgs e) => tcs.TrySetResult(e.IsSuccess);
        c.NavigationCompleted += H; try { c.Navigate(url); await Task.WhenAny(tcs.Task, Task.Delay(ms)); } finally { c.NavigationCompleted -= H; }
        return tcs.Task.IsCompletedSuccessfully && tcs.Task.Result;
    }
    static string ToUrl(string a)
    {
        a = a.Trim(); if (a.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return a;
        if (!a.Contains(' ') && a.Contains('.')) return "https://" + a;
        return "https://www.google.com/search?q=" + Uri.EscapeDataString(a);
    }

    public static async Task<string> Do(DesktopForm sh, string kind, string arg, bool trust, Func<string, bool> confirm)
    {
        try
        {
            var c = sh.ActiveBrowser()?.Core();
            if (kind == "goto") { if (c == null) { sh.Launch("web", ToUrl(arg)); return ""; } c.Navigate(ToUrl(arg)); return ""; }
            if (kind == "play")
            {
                var url = "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(arg);
                if (c == null) { sh.Launch("web", url); await Task.Delay(5000); c = sh.ActiveBrowser()?.Core(); if (c == null) return "ERR: the browser didn't open."; }
                else await NavigateAndWait(c, url);
                string r = ""; for (int i = 0; i < 8 && r != "clicked"; i++) { await Task.Delay(1000); r = await Js(c, "(function(){var a=document.querySelector('ytd-video-renderer a#video-title, a#video-title');if(a){a.click();return 'clicked';}return '';})()"); }
                if (r != "clicked") return "ERR: I couldn't find a video to play on YouTube.";
                await Task.Delay(3500); await Js(c, "(function(){var v=document.querySelector('video');if(v){v.play();}})()"); return "";
            }
            if (c == null) return "ERR: no Browser window is open. Open one with the web action first.";
            switch (kind)
            {
                case "scroll":
                    {
                        var d = arg.Trim().ToLowerInvariant(); string js = d switch
                        {
                            "up" => "window.scrollBy({top:-Math.round(window.innerHeight*0.8),behavior:'smooth'})",
                            "top" => "window.scrollTo({top:0,behavior:'smooth'})",
                            "bottom" => "window.scrollTo({top:document.documentElement.scrollHeight,behavior:'smooth'})",
                            _ => "window.scrollBy({top:Math.round(window.innerHeight*0.8),behavior:'smooth'})"
                        };
                        await Js(c, js); return "";
                    }
                case "nav":
                    switch (arg.Trim().ToLowerInvariant()) { case "back": if (c.CanGoBack) c.GoBack(); break; case "forward": if (c.CanGoForward) c.GoForward(); break; default: c.Reload(); break; }
                    return "";
                case "media":
                    {
                        var act = arg.Trim().ToLowerInvariant();
                        string body = act switch
                        {
                            "pause" => "x.pause()", "toggle" => "(x.paused?x.play():x.pause())", "mute" => "x.muted=true", "unmute" => "x.muted=false",
                            "next" => "0", "fullscreen" => "0", _ => "x.play()"
                        };
                        string js = act == "next" ? "(function(){var b=document.querySelector('.ytp-next-button');if(b){b.click();return 'ok';}return '';})()"
                            : act == "fullscreen" ? "(function(){var v=document.querySelector('video');if(v&&v.requestFullscreen){v.requestFullscreen();return 'ok';}return '';})()"
                            : "(function(){var v=[...document.querySelectorAll('video,audio')];if(!v.length)return '';v.forEach(function(x){try{" + body + ";}catch(e){}});return 'ok';})()";
                        var r = await Js(c, js); return r == "ok" ? "" : "ERR: nothing to control on this page (" + act + ").";
                    }
                case "page":
                    {
                        if (!confirm("The assistant wants to read the text of the page you're viewing in the Browser. Allow?")) return "The user did not allow reading the page.";
                        var raw = await Js(c, "JSON.stringify({url:location.href,title:document.title,text:(document.body?document.body.innerText:'').slice(0,2500),items:[...document.querySelectorAll('a,button,[role=button],input[type=submit]')].filter(function(e){return e.offsetParent!==null;}).map(function(e){return (e.innerText||e.getAttribute('aria-label')||e.title||e.value||'').trim().replace(/\\s+/g,' ').slice(0,60);}).filter(function(t){return t;}).slice(0,30)})");
                        var j = JsonDocument.Parse(raw).RootElement; var items = string.Join(" | ", j.GetProperty("items").EnumerateArray().Select(x => x.GetString()));
                        return ("Page: " + j.GetProperty("title").GetString() + "\nURL: " + j.GetProperty("url").GetString() + "\nText:\n" + j.GetProperty("text").GetString() + "\nClickable: " + items).Replace("[[", "[ [");
                    }
                case "click":
                    {
                        var found = await Js(c, FindClick.Replace("__ARG__", JsonSerializer.Serialize(arg.Trim()))); if (found == "") return "ERR: I couldn't find anything to click that matches \"" + arg.Trim() + "\".";
                        if (Danger.IsMatch(found)) { if (!confirm("The assistant wants to click this button, which looks like it spends money or deletes something:\n\n" + found + "\n\nAllow?")) return "The user did not allow that click."; }
                        else if (!trust && !confirm("The assistant wants to click in the Browser:\n\n" + found + "\n\nAllow?")) return "The user did not allow that click.";
                        var r = await Js(c, "(function(){var e=document.querySelector('[data-ucl]');if(e){e.click();return 'ok';}return '';})()"); return r == "ok" ? "" : "ERR: the click didn't work.";
                    }
                case "type":
                    {
                        var i = arg.IndexOf('|'); var field = i >= 0 ? arg[..i].Trim() : ""; var val = i >= 0 ? arg[(i + 1)..] : arg;
                        var found = await Js(c, FindType.Replace("__ARG__", JsonSerializer.Serialize(field))); if (found == "") return "ERR: I couldn't find a box to type into.";
                        if (found == "PASSWORD") return "ERR: I won't type into password fields. Type it yourself.";
                        if (!trust && !confirm("The assistant wants to type into \"" + found + "\":\n\n" + (val.Length > 120 ? val[..120] + "…" : val) + "\n\nAllow?")) return "The user did not allow typing.";
                        var r = await Js(c, "(function(v){var e=document.querySelector('[data-utp]');if(!e)return '';e.focus();if(e.isContentEditable){e.innerText=v;}else{var p=e.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:HTMLInputElement.prototype;Object.getOwnPropertyDescriptor(p,'value').set.call(e,v);}e.dispatchEvent(new Event('input',{bubbles:true}));e.dispatchEvent(new Event('change',{bubbles:true}));return 'ok';})(" + JsonSerializer.Serialize(val) + ")");
                        return r == "ok" ? "" : "ERR: typing didn't work.";
                    }
                case "key":
                    {
                        if (!trust && !confirm("The assistant wants to press Enter in the Browser (this can submit a form). Allow?")) return "The user did not allow that.";
                        var r = await Js(c, "(function(){var e=document.activeElement;if(!e)return '';var f=e.form;if(f&&f.requestSubmit){try{f.requestSubmit();return 'ok';}catch(x){}}['keydown','keypress','keyup'].forEach(function(t){e.dispatchEvent(new KeyboardEvent(t,{key:'Enter',code:'Enter',keyCode:13,which:13,bubbles:true}));});return 'ok';})()");
                        return r == "ok" ? "" : "ERR: couldn't press Enter.";
                    }
            }
        }
        catch (Exception ex) { return "ERR: browser action failed: " + ex.Message; }
        return "";
    }

    const string FindClick = """
(function(t){t=t.toLowerCase();var els=[...document.querySelectorAll('a,button,[role=button],[role=link],input[type=submit],input[type=button],summary,[onclick]')].filter(function(e){return e.offsetParent!==null||e.getClientRects().length;});var lab=function(e){return ((e.innerText||'')+' '+(e.getAttribute('aria-label')||'')+' '+(e.title||'')+' '+(e.value||'')+' '+(e.alt||'')).trim().replace(/\s+/g,' ').toLowerCase();};var best=els.find(function(e){return lab(e)===t;})||els.find(function(e){return lab(e).includes(t);});if(!best)return '';document.querySelectorAll('[data-ucl]').forEach(function(e){e.removeAttribute('data-ucl');});best.setAttribute('data-ucl','1');best.scrollIntoView({block:'center'});return lab(best).slice(0,80);})(__ARG__)
""";
    const string FindType = """
(function(f){f=f.toLowerCase();var els=[...document.querySelectorAll('input,textarea,[contenteditable=true]')].filter(function(e){return e.offsetParent!==null&&e.type!=='hidden';});var lab=function(e){return ((e.placeholder||'')+' '+(e.getAttribute('aria-label')||'')+' '+(e.name||'')+' '+(e.id||'')+' '+(e.title||'')).toLowerCase();};var best=f?els.find(function(e){return lab(e).includes(f);}):(els.find(function(e){return e===document.activeElement;})||els[0]);if(!best)return '';if(best.type==='password')return 'PASSWORD';document.querySelectorAll('[data-utp]').forEach(function(e){e.removeAttribute('data-utp');});best.setAttribute('data-utp','1');best.scrollIntoView({block:'center'});best.focus();return (best.placeholder||best.name||best.id||best.tagName).slice(0,60);})(__ARG__)
""";
}

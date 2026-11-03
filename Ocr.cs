using System.Diagnostics;
using System.Text;
namespace UsbDesktop;

// Screenshot to text: reads the words in a picture (or the whole PC screen) using Windows' own text recognition. Works offline.
static class Ocr
{
    static string ScriptFile => Path.Combine(Paths.Data, "ocr.ps1");

    const string Script = @"param([string]$p)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics, ContentType = WindowsRuntime]
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($op, $type) { $t = $asTask.MakeGenericMethod($type).Invoke($null, @($op)); $t.Wait(-1) | Out-Null; $t.Result }
$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync($p)) ([Windows.Storage.StorageFile])
$stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
$decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
$bmp = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
if ($engine -eq $null) { throw 'Windows has no text recognition language installed (Settings > Time & language > Language).' }
$result = Await ($engine.RecognizeAsync($bmp)) ([Windows.Media.Ocr.OcrResult])
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$result.Text
";

    // arg: "screen" (or empty) for the whole PC screen, or a picture path relative to the USB root
    public static async Task<string> Run(DesktopForm sh, string arg)
    {
        string path; bool temp = false; arg = arg.Trim();
        try
        {
            if (arg == "" || arg.Equals("screen", StringComparison.OrdinalIgnoreCase))
            {
                var jpg = sh.ShotJpeg(4000); if (jpg == null) return "ERR: I couldn't capture the screen.";
                path = Path.Combine(Path.GetTempPath(), "usbdesktop-ocr.jpg"); File.WriteAllBytes(path, jpg); temp = true;
            }
            else
            {
                path = Path.GetFullPath(Path.IsPathRooted(arg) ? arg : Path.Combine(Paths.Root, arg));
                if (!path.StartsWith(Paths.Root, StringComparison.OrdinalIgnoreCase)) return "ERR: I can only read pictures that are on the USB.";
                if (!File.Exists(path)) return "ERR: I couldn't find that picture: " + arg;
            }
            Directory.CreateDirectory(Paths.Data); File.WriteAllText(ScriptFile, Script, new UTF8Encoding(false));
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + ScriptFile + "\" \"" + path + "\"")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            using var p = Process.Start(psi)!;
            var outT = p.StandardOutput.ReadToEndAsync(); var errT = p.StandardError.ReadToEndAsync();
            bool exited = await Task.Run(() => p.WaitForExit(60000));
            if (!exited) { try { p.Kill(); } catch { } return "ERR: reading the picture took too long."; }
            var text = (await outT).Trim(); var err = (await errT).Trim();
            if (text == "") return err != "" ? "ERR: " + err.Split('\n')[0].Trim() : "I couldn't find any text in that picture.";
            return text.Length > 3000 ? text[..3000] + "…" : text;
        }
        catch (Exception ex) { return "ERR: " + ex.Message; }
        finally { if (temp) { try { File.Delete(Path.Combine(Path.GetTempPath(), "usbdesktop-ocr.jpg")); } catch { } } }
    }
}

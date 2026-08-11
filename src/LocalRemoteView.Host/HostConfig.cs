using System.Text.Json;

namespace LocalRemoteView.Host;

public sealed class HostConfig
{
    public int Port { get; set; } = 45821;
    public string PairingKey { get; set; } = "";
    public int FramesPerSecond { get; set; } = 25;
    public int MaxWidth { get; set; } = 1920;
    public int JpegQuality { get; set; } = 70;
    public string AllowedClientIp { get; set; } = "";
    public bool IsValid => Port is > 0 and <= 65535 && FramesPerSecond is >= 5 and <= 60 && MaxWidth >= 640 && JpegQuality is >= 25 and <= 95 && TryKey();
    public static string PathName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalRemoteView", "host.json");
    public static HostConfig Load()
    {
        try { return File.Exists(PathName) ? JsonSerializer.Deserialize<HostConfig>(File.ReadAllText(PathName)) ?? new() : new(); }
        catch { return new(); }
    }
    public void Save() { Directory.CreateDirectory(Path.GetDirectoryName(PathName)!); File.WriteAllText(PathName, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
    private bool TryKey() { try { return Convert.FromBase64String(PairingKey).Length == 32; } catch { return false; } }
}

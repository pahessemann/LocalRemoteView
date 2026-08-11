using System.Text.Json;

namespace LocalRemoteView.Client;

internal sealed class ClientSettings
{
    public string Host { get; set; } = "192.168.1.10";
    public int Port { get; set; } = 45821;
    public string PairingKey { get; set; } = "";
    private static string FileName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalRemoteView", "client.json");
    public static ClientSettings Load() { try { return File.Exists(FileName) ? JsonSerializer.Deserialize<ClientSettings>(File.ReadAllText(FileName)) ?? new() : new(); } catch { return new(); } }
    public void Save() { Directory.CreateDirectory(Path.GetDirectoryName(FileName)!); File.WriteAllText(FileName, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
}

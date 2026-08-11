using System.Net;

namespace LocalRemoteView.Host;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly HostConfig _config;
    private readonly NotifyIcon _icon;
    private readonly HostServer _server;
    private readonly CancellationTokenSource _cts = new();
    public TrayApplicationContext(HostConfig config)
    {
        _config = config;
        var menu = new ContextMenuStrip();
        menu.Items.Add("État", null, (_, _) => ShowStatus());
        menu.Items.Add("Copier la clé d’appairage", null, (_, _) => Clipboard.SetText(_config.PairingKey));
        menu.Items.Add("Quitter", null, (_, _) => Exit());
        _icon = new NotifyIcon { Icon = SystemIcons.Shield, Text = "LocalRemoteView — démarrage", ContextMenuStrip = menu, Visible = true };
        _server = new HostServer(config);
        _server.StatusChanged += status => { if (_icon.Visible) _icon.Text = status.Length > 63 ? status[..63] : status; };
        _ = RunAsync();
    }
    private async Task RunAsync() { try { await _server.RunAsync(_cts.Token); } catch (Exception ex) { _icon.Text = "LocalRemoteView — erreur"; File.AppendAllText(LogPath(), $"{DateTimeOffset.Now:u} {ex}\n"); } }
    private void ShowStatus()
    {
        var addresses = Dns.GetHostAddresses(Dns.GetHostName()).Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(a => a.ToString());
        MessageBox.Show($"LocalRemoteView est actif.\n\nAdresse(s) : {string.Join(", ", addresses)}\nPort : {_config.Port}\nÉtat : {_server.Status}", "LocalRemoteView", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    private void Exit() { _cts.Cancel(); _icon.Visible = false; _icon.Dispose(); ExitThread(); }
    protected override void Dispose(bool disposing) { if (disposing) { _cts.Cancel(); _cts.Dispose(); _icon.Dispose(); } base.Dispose(disposing); }
    private static string LogPath() { var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalRemoteView"); Directory.CreateDirectory(dir); return Path.Combine(dir, "host.log"); }
}

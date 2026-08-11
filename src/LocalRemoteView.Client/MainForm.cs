using LocalRemoteView.Shared;
using System.Net.Sockets;

namespace LocalRemoteView.Client;

public sealed class MainForm : Form
{
    private readonly TextBox _host = new() { Width = 140 };
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535, Width = 75 };
    private readonly TextBox _key = new() { Width = 280, UseSystemPasswordChar = true };
    private readonly Button _connect = new() { Text = "Connexion", AutoSize = true };
    private readonly Button _fullscreen = new() { Text = "Plein écran (F11)", AutoSize = true };
    private readonly Label _status = new() { Text = "Déconnecté", AutoSize = true, ForeColor = Color.DimGray, Anchor = AnchorStyles.Left };
    private readonly PictureBox _screen = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 22, 26), SizeMode = PictureBoxSizeMode.Zoom, TabStop = true };
    private readonly Panel _top = new() { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
    private SecureChannel? _channel;
    private TcpClient? _tcp;
    private CancellationTokenSource? _session;
    private bool _fullScreen;
    private FormBorderStyle _savedBorder;
    private Rectangle _savedBounds;

    public MainForm()
    {
        Text = "LocalRemoteView"; Width = 1280; Height = 800; MinimumSize = new Size(720, 480); KeyPreview = true;
        var settings = ClientSettings.Load(); _host.Text = settings.Host; _port.Value = Math.Clamp(settings.Port, 1, 65535); _key.Text = settings.PairingKey;
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        row.Controls.AddRange([new Label { Text = "PC :", AutoSize = true, Anchor = AnchorStyles.Left }, _host, new Label { Text = "Port :", AutoSize = true, Anchor = AnchorStyles.Left }, _port, new Label { Text = "Clé :", AutoSize = true, Anchor = AnchorStyles.Left }, _key, _connect, _fullscreen, _status]);
        _top.Controls.Add(row); Controls.Add(_screen); Controls.Add(_top);
        _connect.Click += async (_, _) => { if (_channel is null) await ConnectAsync(); else Disconnect(); };
        _fullscreen.Click += (_, _) => ToggleFullscreen();
        KeyDown += OnRemoteKeyDown; KeyUp += OnRemoteKeyUp;
        _screen.MouseMove += (_, e) => SendPoint(e.Location);
        _screen.MouseDown += (_, e) => { _screen.Focus(); Send(MessageType.MouseButton, Wire.Ints(ButtonId(e.Button), 1)); };
        _screen.MouseUp += (_, e) => Send(MessageType.MouseButton, Wire.Ints(ButtonId(e.Button), 0));
        _screen.MouseWheel += (_, e) => Send(MessageType.MouseWheel, Wire.Ints(e.Delta));
        FormClosing += (_, _) => Disconnect();
    }

    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(_host.Text) || string.IsNullOrWhiteSpace(_key.Text)) { MessageBox.Show("Saisissez l’adresse du PC et la clé d’appairage.", "Connexion", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        SetConnecting(true, "Connexion…");
        try
        {
            _session = new CancellationTokenSource();
            _tcp = new TcpClient { NoDelay = true };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_session.Token); timeout.CancelAfter(TimeSpan.FromSeconds(8));
            await _tcp.ConnectAsync(_host.Text.Trim(), (int)_port.Value, timeout.Token);
            _channel = await SecureChannel.ConnectAsync(_tcp, _key.Text, timeout.Token);
            new ClientSettings { Host = _host.Text.Trim(), Port = (int)_port.Value, PairingKey = _key.Text }.Save();
            _connect.Text = "Déconnexion"; _connect.Enabled = true; _status.Text = "Connecté — contrôle actif"; _status.ForeColor = Color.SeaGreen; _host.Enabled = _port.Enabled = _key.Enabled = false;
            _ = ReceiveFramesAsync(_session.Token);
        }
        catch (Exception ex) { Disconnect(); MessageBox.Show($"Connexion impossible :\n{ex.Message}", "LocalRemoteView", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { if (_channel is null) SetConnecting(false, "Déconnecté"); }
    }

    private async Task ReceiveFramesAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var packet = await _channel!.ReceiveAsync(ct);
                if (packet.Type == MessageType.InputStatus)
                {
                    var ok = packet.Data.Length > 0 && packet.Data[0] == 1;
                    BeginInvoke(() => { _status.Text = ok ? "Connecté — clavier/souris actifs" : "Connecté — injection refusée par Windows"; _status.ForeColor = ok ? Color.SeaGreen : Color.Firebrick; });
                    continue;
                }
                if (packet.Type != MessageType.Frame) continue;
                var (_, _, jpeg) = Wire.ReadFrame(packet.Data);
                using var ms = new MemoryStream(jpeg);
                using var decoded = new Bitmap(ms);
                var bitmap = new Bitmap(decoded);
                BeginInvoke(() => { var old = _screen.Image; _screen.Image = bitmap; old?.Dispose(); });
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested) { BeginInvoke(() => { Disconnect(); _status.Text = "Connexion perdue"; _status.ForeColor = Color.Firebrick; }); }
    }

    private void SendPoint(Point point)
    {
        if (_screen.Image is null) return;
        var box = ImageRectangle();
        if (!box.Contains(point)) return;
        Send(MessageType.MouseMove, Wire.Point((point.X - box.X) / (float)box.Width, (point.Y - box.Y) / (float)box.Height));
    }
    private Rectangle ImageRectangle()
    {
        if (_screen.Image is null) return Rectangle.Empty;
        var imageRatio = _screen.Image.Width / (double)_screen.Image.Height; var boxRatio = _screen.ClientSize.Width / (double)_screen.ClientSize.Height;
        if (imageRatio > boxRatio) { var h = (int)(_screen.ClientSize.Width / imageRatio); return new Rectangle(0, (_screen.ClientSize.Height - h) / 2, _screen.ClientSize.Width, h); }
        var w = (int)(_screen.ClientSize.Height * imageRatio); return new Rectangle((_screen.ClientSize.Width - w) / 2, 0, w, _screen.ClientSize.Height);
    }
    private void OnRemoteKeyDown(object? sender, KeyEventArgs e) { if (e.KeyCode == Keys.F11) { ToggleFullscreen(); e.Handled = true; return; } if (_screen.Focused) { Send(MessageType.Key, Wire.Ints((int)e.KeyCode, 1)); e.Handled = true; e.SuppressKeyPress = true; } }
    private void OnRemoteKeyUp(object? sender, KeyEventArgs e) { if (_screen.Focused && e.KeyCode != Keys.F11) { Send(MessageType.Key, Wire.Ints((int)e.KeyCode, 0)); e.Handled = true; e.SuppressKeyPress = true; } }
    private async void Send(MessageType type, byte[] data) { var channel = _channel; var ct = _session?.Token ?? CancellationToken.None; if (channel is null) return; try { await channel.SendAsync(type, data, ct); } catch { } }
    private void Disconnect()
    {
        _session?.Cancel(); _channel?.DisposeAsync().AsTask().GetAwaiter().GetResult(); _tcp?.Dispose(); _session?.Dispose();
        _session = null; _channel = null; _tcp = null; _connect.Text = "Connexion"; _host.Enabled = _port.Enabled = _key.Enabled = true;
        if (!_status.Text.Contains("perdue")) { _status.Text = "Déconnecté"; _status.ForeColor = Color.DimGray; }
    }
    private void ToggleFullscreen()
    {
        if (!_fullScreen) { _savedBorder = FormBorderStyle; _savedBounds = Bounds; FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Normal; Bounds = Screen.FromControl(this).Bounds; _top.Visible = false; _fullScreen = true; }
        else { FormBorderStyle = _savedBorder; Bounds = _savedBounds; _top.Visible = true; _fullScreen = false; }
    }
    private void SetConnecting(bool busy, string text) { _connect.Enabled = !busy; _status.Text = text; _status.ForeColor = Color.DimGray; }
    private static int ButtonId(MouseButtons b) => b switch { MouseButtons.Left => 1, MouseButtons.Right => 2, MouseButtons.Middle => 3, _ => 0 };
}

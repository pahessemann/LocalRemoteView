using LocalRemoteView.Shared;

namespace LocalRemoteView.Host;

public sealed class SetupForm : Form
{
    private readonly NumericUpDown _port = new() { Minimum = 1024, Maximum = 65535, Value = 45821, Width = 120 };
    private readonly TextBox _key = new() { ReadOnly = true, Width = 390 };
    private readonly TextBox _allowedIp = new() { Width = 180, PlaceholderText = "Vide = tout le LAN" };
    public HostConfig Config { get; private set; }

    public SetupForm(HostConfig config)
    {
        Config = config;
        Text = "Configuration initiale — LocalRemoteView"; Width = 560; Height = 290; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        _port.Value = Math.Clamp(config.Port, 1024, 65535);
        _key.Text = string.IsNullOrWhiteSpace(config.PairingKey) ? SecureChannel.NewPairingKey() : config.PairingKey;
        _allowedIp.Text = config.AllowedClientIp;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 6, AutoSize = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.Controls.Add(new Label { Text = "Port :", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); table.Controls.Add(_port, 1, 0);
        table.Controls.Add(new Label { Text = "Clé d’appairage :", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); table.Controls.Add(_key, 1, 1);
        table.Controls.Add(new Label { Text = "IP cliente autorisée :", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2); table.Controls.Add(_allowedIp, 1, 2);
        table.Controls.Add(new Label { Text = "Conservez cette clé et saisissez-la sur le PC client.\nPendant les sessions, aucun bandeau ne sera affiché sur le bureau.", AutoSize = true, ForeColor = Color.DimGray }, 0, 3); table.SetColumnSpan(table.GetControlFromPosition(0, 3)!, 2);
        var copy = new Button { Text = "Copier la clé", AutoSize = true }; copy.Click += (_, _) => Clipboard.SetText(_key.Text);
        var ok = new Button { Text = "Enregistrer", AutoSize = true }; ok.Click += (_, _) => SaveAndClose();
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight }; buttons.Controls.Add(copy); buttons.Controls.Add(ok);
        table.Controls.Add(buttons, 1, 4); Controls.Add(table); AcceptButton = ok;
    }
    private void SaveAndClose() { Config.Port = (int)_port.Value; Config.PairingKey = _key.Text; Config.AllowedClientIp = _allowedIp.Text.Trim(); DialogResult = DialogResult.OK; Close(); }
}

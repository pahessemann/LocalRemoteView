using LocalRemoteView.Shared;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace LocalRemoteView.Host;

public sealed class HostServer
{
    private readonly HostConfig _config;
    public string Status { get; private set; } = "Initialisation";
    public event Action<string>? StatusChanged;
    public HostServer(HostConfig config) => _config = config;

    public async Task RunAsync(CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Any, _config.Port); listener.Start(); SetStatus($"LocalRemoteView — écoute sur {_config.Port}");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                if (!Allowed((IPEndPoint)client.Client.RemoteEndPoint!)) { client.Dispose(); continue; }
                try { await HandleClientAsync(client, ct); }
                catch (Exception ex) when (ex is IOException or SocketException or CryptographicException or OperationCanceledException) { }
                finally { client.Dispose(); if (!ct.IsCancellationRequested) SetStatus($"LocalRemoteView — écoute sur {_config.Port}"); }
            }
        }
        finally { listener.Stop(); }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken outerCt)
    {
        client.NoDelay = true;
        var endpoint = (IPEndPoint)client.Client.RemoteEndPoint!;
        using var authCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt); authCts.CancelAfter(TimeSpan.FromSeconds(8));
        await using var channel = await SecureChannel.AcceptAsync(client, _config.PairingKey, authCts.Token);
        SetStatus($"LocalRemoteView — connecté : {endpoint.Address}");
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        var input = ReceiveInputAsync(channel, sessionCts.Token);
        try
        {
            var delay = TimeSpan.FromMilliseconds(1000d / _config.FramesPerSecond);
            while (!sessionCts.IsCancellationRequested)
            {
                var started = DateTime.UtcNow;
                var (jpeg, width, height) = CaptureFrame();
                await channel.SendAsync(MessageType.Frame, Wire.Frame(width, height, jpeg), sessionCts.Token);
                var remaining = delay - (DateTime.UtcNow - started);
                if (remaining > TimeSpan.Zero) await Task.Delay(remaining, sessionCts.Token);
            }
        }
        finally { sessionCts.Cancel(); try { await input; } catch { } }
    }

    private async Task ReceiveInputAsync(SecureChannel channel, CancellationToken ct)
    {
        var controlConfirmed = false;
        while (!ct.IsCancellationRequested)
        {
            var packet = await channel.ReceiveAsync(ct);
            var handled = false;
            switch (packet.Type)
            {
                case MessageType.MouseMove: var p = Wire.ReadPoint(packet.Data); handled = NativeInput.Move(p.X, p.Y); break;
                case MessageType.MouseButton: var mb = Wire.ReadInts(packet.Data); handled = NativeInput.Button(mb.A, mb.B != 0); break;
                case MessageType.MouseWheel: var wh = Wire.ReadInts(packet.Data); handled = NativeInput.Wheel(wh.A); break;
                case MessageType.Key: var key = Wire.ReadInts(packet.Data); handled = NativeInput.Key((ushort)key.A, key.B != 0); break;
            }
            if (!controlConfirmed && packet.Type is MessageType.MouseMove or MessageType.MouseButton or MessageType.MouseWheel or MessageType.Key)
            {
                await channel.SendAsync(MessageType.InputStatus, new byte[] { handled ? (byte)1 : (byte)0 }, ct);
                controlConfirmed = handled;
            }
        }
    }

    private (byte[] Jpeg, int Width, int Height) CaptureFrame()
    {
        var bounds = SystemInformation.VirtualScreen;
        var width = Math.Min(bounds.Width, _config.MaxWidth);
        var height = Math.Max(1, (int)Math.Round(bounds.Height * (width / (double)bounds.Width)));
        using var source = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(source))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            NativeCursor.Draw(g, bounds.Location);
        }
        using var output = width == bounds.Width ? new Bitmap(source) : new Bitmap(source, width, height);
        using var ms = new MemoryStream();
        var codec = ImageCodecInfo.GetImageEncoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
        using var ep = new EncoderParameters(1); ep.Param[0] = new EncoderParameter(Encoder.Quality, _config.JpegQuality);
        output.Save(ms, codec, ep); return (ms.ToArray(), width, height);
    }

    private bool Allowed(IPEndPoint ep)
    {
        if (!string.IsNullOrWhiteSpace(_config.AllowedClientIp)) return ep.Address.ToString() == _config.AllowedClientIp;
        if (IPAddress.IsLoopback(ep.Address)) return true;
        var b = ep.Address.GetAddressBytes(); return b.Length == 4 && (b[0] == 10 || b[0] == 127 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] is >= 16 and <= 31));
    }
    private void SetStatus(string value) { Status = value; StatusChanged?.Invoke(value); }
}

using LocalRemoteView.Shared;
using System.Net;
using System.Net.Sockets;

var key = SecureChannel.NewPairingKey();
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port;
var server = Task.Run(async () =>
{
    using var tcp = await listener.AcceptTcpClientAsync();
    await using var secure = await SecureChannel.AcceptAsync(tcp, key, CancellationToken.None);
    var packet = await secure.ReceiveAsync(CancellationToken.None);
    if (packet.Type != MessageType.Ping || System.Text.Encoding.UTF8.GetString(packet.Data) != "bonjour") throw new Exception("Paquet client invalide");
    await secure.SendAsync(MessageType.Ping, "réponse"u8.ToArray(), CancellationToken.None);
});
using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, port);
await using var channel = await SecureChannel.ConnectAsync(client, key, CancellationToken.None);
await channel.SendAsync(MessageType.Ping, "bonjour"u8.ToArray(), CancellationToken.None);
var reply = await channel.ReceiveAsync(CancellationToken.None);
if (reply.Type != MessageType.Ping || System.Text.Encoding.UTF8.GetString(reply.Data) != "réponse") throw new Exception("Réponse serveur invalide");
await server; listener.Stop();
var point = Wire.ReadPoint(Wire.Point(.25f, .75f));
if (point != (.25f, .75f)) throw new Exception("Sérialisation invalide");
Console.WriteLine("Tous les autotests ont réussi.");

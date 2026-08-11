using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace LocalRemoteView.Shared;

public enum MessageType : byte { Frame = 1, MouseMove = 2, MouseButton = 3, MouseWheel = 4, Key = 5, Ping = 6, InputStatus = 7 }
public readonly record struct Packet(MessageType Type, byte[] Data);

public sealed class SecureChannel : IAsyncDisposable
{
    private const int MaxPacket = 24 * 1024 * 1024;
    private static readonly byte[] Magic = "LRV1"u8.ToArray();
    private readonly NetworkStream _stream;
    private readonly byte[] _key;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private SecureChannel(NetworkStream stream, byte[] key) { _stream = stream; _key = key; }

    public static async Task<SecureChannel> ConnectAsync(TcpClient client, string pairingKey, CancellationToken ct)
    {
        var stream = client.GetStream();
        var baseKey = ParseKey(pairingKey);
        var clientNonce = RandomNumberGenerator.GetBytes(32);
        var proof = HMACSHA256.HashData(baseKey, Combine(clientNonce, "client"u8.ToArray()));
        await stream.WriteAsync(Combine(Magic, clientNonce, proof), ct);
        var response = new byte[64];
        await ReadExactlyAsync(stream, response, ct);
        var serverNonce = response[..32];
        var expected = HMACSHA256.HashData(baseKey, Combine(clientNonce, serverNonce, "server"u8.ToArray()));
        if (!CryptographicOperations.FixedTimeEquals(expected, response[32..])) throw new CryptographicException("Authentification de l’hôte refusée.");
        return new SecureChannel(stream, Derive(baseKey, clientNonce, serverNonce));
    }

    public static async Task<SecureChannel> AcceptAsync(TcpClient client, string pairingKey, CancellationToken ct)
    {
        var stream = client.GetStream();
        var request = new byte[68];
        await ReadExactlyAsync(stream, request, ct);
        if (!request.AsSpan(0, 4).SequenceEqual(Magic)) throw new CryptographicException("Protocole invalide.");
        var baseKey = ParseKey(pairingKey);
        var clientNonce = request[4..36];
        var expected = HMACSHA256.HashData(baseKey, Combine(clientNonce, "client"u8.ToArray()));
        if (!CryptographicOperations.FixedTimeEquals(expected, request[36..])) throw new CryptographicException("Clé d’appairage incorrecte.");
        var serverNonce = RandomNumberGenerator.GetBytes(32);
        var proof = HMACSHA256.HashData(baseKey, Combine(clientNonce, serverNonce, "server"u8.ToArray()));
        await stream.WriteAsync(Combine(serverNonce, proof), ct);
        return new SecureChannel(stream, Derive(baseKey, clientNonce, serverNonce));
    }

    public async Task SendAsync(MessageType type, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var plain = new byte[data.Length + 1];
        plain[0] = (byte)type;
        data.CopyTo(plain.AsMemory(1));
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(_key, 16)) aes.Encrypt(nonce, plain, cipher, tag);
        var length = nonce.Length + cipher.Length + tag.Length;
        var packet = new byte[length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(packet, length);
        nonce.CopyTo(packet, 4); cipher.CopyTo(packet, 16); tag.CopyTo(packet, 16 + cipher.Length);
        await _writeLock.WaitAsync(ct);
        try { await _stream.WriteAsync(packet, ct); }
        finally { _writeLock.Release(); }
    }

    public async Task<Packet> ReceiveAsync(CancellationToken ct)
    {
        var header = new byte[4];
        await ReadExactlyAsync(_stream, header, ct);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length < 29 || length > MaxPacket) throw new IOException("Taille de paquet invalide.");
        var packet = new byte[length];
        await ReadExactlyAsync(_stream, packet, ct);
        var cipherLength = length - 28;
        var plain = new byte[cipherLength];
        using (var aes = new AesGcm(_key, 16)) aes.Decrypt(packet.AsSpan(0, 12), packet.AsSpan(12, cipherLength), packet.AsSpan(12 + cipherLength, 16), plain);
        return new Packet((MessageType)plain[0], plain[1..]);
    }

    public ValueTask DisposeAsync() { _stream.Dispose(); _writeLock.Dispose(); CryptographicOperations.ZeroMemory(_key); return ValueTask.CompletedTask; }
    public static string NewPairingKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static byte[] ParseKey(string key) { var bytes = Convert.FromBase64String(key.Trim()); if (bytes.Length != 32) throw new FormatException("La clé doit contenir 32 octets."); return bytes; }
    private static byte[] Derive(byte[] key, byte[] a, byte[] b) => HMACSHA256.HashData(key, Combine("LocalRemoteView/session"u8.ToArray(), a, b));
    private static byte[] Combine(params byte[][] arrays) { var result = new byte[arrays.Sum(x => x.Length)]; var offset = 0; foreach (var a in arrays) { a.CopyTo(result, offset); offset += a.Length; } return result; }
    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken ct) { var read = 0; while (read < buffer.Length) { var n = await stream.ReadAsync(buffer[read..], ct); if (n == 0) throw new EndOfStreamException(); read += n; } }
}

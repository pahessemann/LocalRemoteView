using System.Buffers.Binary;

namespace LocalRemoteView.Shared;

public static class Wire
{
    public static byte[] Frame(int width, int height, byte[] jpeg)
    {
        var data = new byte[jpeg.Length + 8];
        BinaryPrimitives.WriteInt32LittleEndian(data, width);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), height);
        jpeg.CopyTo(data, 8); return data;
    }
    public static (int Width, int Height, byte[] Jpeg) ReadFrame(byte[] data) =>
        (BinaryPrimitives.ReadInt32LittleEndian(data), BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4)), data[8..]);
    public static byte[] Point(float x, float y) { var b = new byte[8]; BinaryPrimitives.WriteSingleLittleEndian(b, x); BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(4), y); return b; }
    public static (float X, float Y) ReadPoint(byte[] b) => (BinaryPrimitives.ReadSingleLittleEndian(b), BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(4)));
    public static byte[] Ints(int a, int b = 0) { var x = new byte[8]; BinaryPrimitives.WriteInt32LittleEndian(x, a); BinaryPrimitives.WriteInt32LittleEndian(x.AsSpan(4), b); return x; }
    public static (int A, int B) ReadInts(byte[] b) => (BinaryPrimitives.ReadInt32LittleEndian(b), BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(4)));
}

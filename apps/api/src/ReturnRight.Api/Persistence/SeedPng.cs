using System.Buffers.Binary;
using System.IO.Compression;

namespace ReturnRight.Api.Persistence;

/// <summary>Minimal RGB PNG writer used only by the dev seeder.</summary>
public static class SeedPng
{
    public static byte[] SolidColor(int width, int height, byte r, byte g, byte b)
    {
        // Uncompressed pixel data: each row starts with filter byte 0.
        var stride = width * 3;
        var raw = new byte[(stride + 1) * height];
        for (var y = 0; y < height; y++)
        {
            var row = raw.AsSpan(y * (stride + 1), stride + 1);
            row[0] = 0;
            for (var x = 0; x < width; x++)
            {
                row[1 + (x * 3)] = r;
                row[2 + (x * 3)] = g;
                row[3 + (x * 3)] = b;
            }
        }

        var idat = ZlibCompress(raw);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 2;  // colour type: truecolour RGB
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace

        using var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(stream, "IHDR"u8, ihdr);
        WriteChunk(stream, "IDAT"u8, idat);
        WriteChunk(stream, "IEND"u8, []);
        return stream.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32BigEndian(header, data.Length);
        type.CopyTo(header[4..]);
        stream.Write(header);
        stream.Write(data);

        var crc = Crc32(type, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(0x78); // zlib header: 32K window
        stream.WriteByte(0x9C); // default compression
        using (var deflate = new DeflateStream(stream, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(data);
        }
        var adler = Adler32(data);
        Span<byte> tail = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(tail, adler);
        stream.Write(tail);
        return stream.ToArray();
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        const uint mod = 65521;
        uint a = 1, b = 0;
        foreach (var value in data)
        {
            a = (a + value) % mod;
            b = (b + a) % mod;
        }
        return (b << 16) | a;
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        crc = Update(crc, type);
        crc = Update(crc, data);
        return ~crc;
    }

    private static uint Update(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc ^= value;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        return crc;
    }
}

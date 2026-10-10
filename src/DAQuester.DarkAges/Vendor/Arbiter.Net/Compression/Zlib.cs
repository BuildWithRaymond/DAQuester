using System.IO.Compression;

namespace Arbiter.Net.Compression;

public static class Zlib
{
    public static MemoryStream Decompress(Stream stream)
    {
        using var zlibStream = new ZLibStream(stream, CompressionMode.Decompress);
        var outputStream = new MemoryStream();

        zlibStream.CopyTo(outputStream);
        outputStream.Position = 0;

        return outputStream;
    }

    public static byte[] Decompress(ReadOnlySpan<byte> span)
    {
        using var inputStream = new MemoryStream(new byte[span.Length], true);
        inputStream.Write(span);
        inputStream.Position = 0;

        using var zlibStream = new ZLibStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        zlibStream.CopyTo(outputStream);
        outputStream.Position = 0;

        return outputStream.ToArray();
    }

    public static async Task<MemoryStream> DecompressAsync(Stream stream)
    {
        await using var zlibStream = new ZLibStream(stream, CompressionMode.Decompress);
        var outputStream = new MemoryStream();

        await zlibStream.CopyToAsync(outputStream);
        outputStream.Position = 0;

        return outputStream;
    }
    
    public static MemoryStream Compress(Stream stream)
    {
        using var zlibStream = new ZLibStream(stream, CompressionMode.Compress, leaveOpen: true);
        var outputStream = new MemoryStream();

        zlibStream.CopyTo(outputStream); 
        zlibStream.Flush();
        zlibStream.Close();
        
        outputStream.Position = 0;
        return outputStream;
    }

    public static byte[] Compress(ReadOnlySpan<byte> span)
    {
        using var inputStream = new MemoryStream();
        inputStream.Write(span);
        inputStream.Position = 0;

        using var outputStream = new MemoryStream();
        using var zlibStream = new ZLibStream(outputStream, CompressionMode.Compress, leaveOpen: true);
        inputStream.CopyTo(zlibStream);
        zlibStream.Flush();
        zlibStream.Close();
        
        outputStream.Position = 0;
        
        return outputStream.ToArray();
    }

    public static async Task<MemoryStream> CompressAsync(Stream stream)
    {
        await using var zlibStream = new ZLibStream(stream, CompressionMode.Compress, leaveOpen: true);
        var outputStream = new MemoryStream();

        await zlibStream.CopyToAsync(outputStream);
        await zlibStream.FlushAsync();
        zlibStream.Close();
        
        outputStream.Position = 0;
        return outputStream;
    }
}
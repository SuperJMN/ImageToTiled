namespace ImageToTiled;

using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public sealed class Tile : IEquatable<Tile>
{
    private readonly byte[] pixelBytes;
    private readonly int hashCode;

    public int Width { get; }
    public int Height { get; }
    public ReadOnlyMemory<byte> PixelBytes => pixelBytes;

    public Tile(int width, int height, byte[] pixelBytes)
    {
        Width = width;
        Height = height;
        this.pixelBytes = pixelBytes;
        hashCode = ComputeHash(pixelBytes);
    }

    public static Tile FromImage(Image<Rgba32> image, int startX, int startY, int width, int height)
    {
        var bytes = new byte[width * height * 4];
        var offset = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(startY + y);
                for (var x = 0; x < width; x++)
                {
                    var pixel = row[startX + x];
                    bytes[offset++] = pixel.R;
                    bytes[offset++] = pixel.G;
                    bytes[offset++] = pixel.B;
                    bytes[offset++] = pixel.A;
                }
            }
        });

        return new Tile(width, height, bytes);
    }

    public static Tile CreateEmpty(int width, int height)
    {
        var bytes = new byte[width * height * 4];
        return new Tile(width, height, bytes);
    }

    public bool IsTransparent(Rgba32? colorKey = null)
    {
        for (var i = 0; i < pixelBytes.Length; i += 4)
        {
            var a = pixelBytes[i + 3];
            if (a > 0)
            {
                if (colorKey.HasValue)
                {
                    var r = pixelBytes[i];
                    var g = pixelBytes[i + 1];
                    var b = pixelBytes[i + 2];
                    if (r == colorKey.Value.R && g == colorKey.Value.G && b == colorKey.Value.B)
                    {
                        continue;
                    }
                }
                return false;
            }
        }
        return true;
    }

    public void CopyTo(Image<Rgba32> targetImage, int destX, int destY)
    {
        targetImage.ProcessPixelRows(accessor =>
        {
            var offset = 0;
            for (var y = 0; y < Height; y++)
            {
                var row = accessor.GetRowSpan(destY + y);
                for (var x = 0; x < Width; x++)
                {
                    row[destX + x] = new Rgba32(
                        pixelBytes[offset],
                        pixelBytes[offset + 1],
                        pixelBytes[offset + 2],
                        pixelBytes[offset + 3]);
                    offset += 4;
                }
            }
        });
    }

    public bool Equals(Tile? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Width != other.Width || Height != other.Height || hashCode != other.hashCode) return false;
        return pixelBytes.AsSpan().SequenceEqual(other.pixelBytes);
    }

    public override bool Equals(object? obj) => obj is Tile other && Equals(other);

    public override int GetHashCode() => hashCode;

    private static int ComputeHash(ReadOnlySpan<byte> data)
    {
        uint hash = 2166136261;
        for (var i = 0; i < data.Length; i++)
        {
            hash = (hash ^ data[i]) * 16777619;
        }
        return unchecked((int)hash);
    }
}

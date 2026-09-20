namespace ImageToTiled.Tests;

using System;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

public sealed class ConverterTests : IDisposable
{
    private readonly string testDir;

    public ConverterTests()
    {
        testDir = Path.Combine(Path.GetTempPath(), "ImageToTiledTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(testDir))
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public void Deduplicates_identical_tiles()
    {
        // 32x16 image with two identical 16x16 red tiles
        using var img = new Image<Rgba32>(32, 16);
        img.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < 16; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < 32; x++)
                {
                    row[x] = new Rgba32(255, 0, 0, 255);
                }
            }
        });

        var result = ImageToTiledConverter.Convert(img, testDir, new ConversionOptions
        {
            TileWidth = 16,
            TileHeight = 16,
            EmptyMode = EmptyMode.None,
            Name = "red"
        });

        result.MapWidth.Should().Be(2);
        result.MapHeight.Should().Be(1);
        result.UniqueTilesCount.Should().Be(1);
        result.VerifiedLossless.Should().BeTrue();
    }

    [Fact]
    public void EmptyMode_tile_reserves_tile0_for_transparent()
    {
        // 32x16 image: left tile transparent, right tile blue
        using var img = new Image<Rgba32>(32, 16);
        img.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < 16; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 16; x < 32; x++)
                {
                    row[x] = new Rgba32(0, 0, 255, 255);
                }
            }
        });

        var result = ImageToTiledConverter.Convert(img, testDir, new ConversionOptions
        {
            TileWidth = 16,
            TileHeight = 16,
            EmptyMode = EmptyMode.Tile,
            Name = "test_tile_mode"
        });

        result.UniqueTilesCount.Should().Be(2); // 1 empty + 1 blue
        result.TransparentCellsCount.Should().Be(1);

        // Verify TMX content
        var tmxDoc = XDocument.Load(result.TmxPath);
        var data = tmxDoc.Root!.Element("layer")!.Element("data")!.Value.Trim();
        data.Should().Be("1,2");
    }

    [Fact]
    public void EmptyMode_gid0_uses_zero_for_transparent_cells()
    {
        // 32x16 image: left tile transparent, right tile blue
        using var img = new Image<Rgba32>(32, 16);
        img.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < 16; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 16; x < 32; x++)
                {
                    row[x] = new Rgba32(0, 0, 255, 255);
                }
            }
        });

        var result = ImageToTiledConverter.Convert(img, testDir, new ConversionOptions
        {
            TileWidth = 16,
            TileHeight = 16,
            EmptyMode = EmptyMode.Gid0,
            Name = "test_gid0_mode"
        });

        result.UniqueTilesCount.Should().Be(1); // Only blue tile
        result.TransparentCellsCount.Should().Be(1);

        // Verify TMX content
        var tmxDoc = XDocument.Load(result.TmxPath);
        var data = tmxDoc.Root!.Element("layer")!.Element("data")!.Value.Trim();
        data.Should().Be("0,1");
    }

    [Fact]
    public void ColorKey_treats_specified_color_as_transparent()
    {
        // 16x16 magenta tile
        using var img = new Image<Rgba32>(16, 16);
        img.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < 16; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < 16; x++)
                {
                    row[x] = new Rgba32(255, 0, 255, 255); // Magenta
                }
            }
        });

        var result = ImageToTiledConverter.Convert(img, testDir, new ConversionOptions
        {
            TileWidth = 16,
            TileHeight = 16,
            EmptyMode = EmptyMode.Gid0,
            ColorKey = new Rgba32(255, 0, 255, 255),
            Name = "color_key"
        });

        result.TransparentCellsCount.Should().Be(1);
        result.UniqueTilesCount.Should().Be(0);
    }

    [Fact]
    public void Pads_image_if_not_multiple_of_tile_size()
    {
        // 20x20 image -> with 16x16 tiles, should pad to 32x32 (2x2 tiles)
        using var img = new Image<Rgba32>(20, 20);
        var result = ImageToTiledConverter.Convert(img, testDir, new ConversionOptions
        {
            TileWidth = 16,
            TileHeight = 16,
            Name = "padded"
        });

        result.MapWidth.Should().Be(2);
        result.MapHeight.Should().Be(2);
    }

    [Fact]
    public void RetrosharpProperties_included_or_omitted_correctly()
    {
        using var img = new Image<Rgba32>(16, 16);

        // Enabled
        var withProps = ImageToTiledConverter.Convert(img, testDir, new ConversionOptions
        {
            RetrosharpProperties = true,
            StreamY = 2,
            WorldY = 3,
            WorldHeight = 10,
            Name = "with_props"
        });

        var docWith = XDocument.Load(withProps.TmxPath);
        docWith.Root!.Element("properties").Should().NotBeNull();
        var props = docWith.Root!.Element("properties")!.Elements("property");
        props.Should().HaveCount(3);

        // Disabled
        var withoutProps = ImageToTiledConverter.Convert(img, testDir, new ConversionOptions
        {
            RetrosharpProperties = false,
            Name = "without_props"
        });

        var docWithout = XDocument.Load(withoutProps.TmxPath);
        docWithout.Root!.Element("properties").Should().BeNull();
    }

    [Fact]
    public void Converts_Part2_png_correctly_when_file_exists()
    {
        const string part2Path = "/home/jmn/Escritorio/SMB2/Part2.png";
        if (!File.Exists(part2Path))
        {
            return;
        }

        var result = ImageToTiledConverter.Convert(part2Path, testDir, new ConversionOptions
        {
            TileWidth = 16,
            TileHeight = 16,
            EmptyMode = EmptyMode.Tile,
            Columns = 16
        });

        result.SourceWidth.Should().Be(2544);
        result.SourceHeight.Should().Be(240);
        result.MapWidth.Should().Be(159);
        result.MapHeight.Should().Be(15);
        result.UniqueTilesCount.Should().Be(199);
        result.TilesetColumns.Should().Be(16);
        result.TilesetRows.Should().Be(13);
        result.TileCount.Should().Be(208);
        result.VerifiedLossless.Should().BeTrue();
    }
}

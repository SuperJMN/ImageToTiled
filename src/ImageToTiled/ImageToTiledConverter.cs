namespace ImageToTiled;

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public static class ImageToTiledConverter
{
    public static ConversionResult Convert(string inputImagePath, string? outputDirectory = null, ConversionOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(inputImagePath))
        {
            throw new ArgumentException("Input image path cannot be empty.", nameof(inputImagePath));
        }

        var fullInputPath = Path.GetFullPath(inputImagePath);
        if (!File.Exists(fullInputPath))
        {
            throw new FileNotFoundException($"Input image not found: '{fullInputPath}'", fullInputPath);
        }

        var resolvedOptions = options ?? new ConversionOptions();
        var outDir = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.GetDirectoryName(fullInputPath) ?? Directory.GetCurrentDirectory()
            : Path.GetFullPath(outputDirectory);

        Directory.CreateDirectory(outDir);

        using var image = Image.Load<Rgba32>(fullInputPath);
        return ConvertImage(image, fullInputPath, outDir, resolvedOptions);
    }

    public static ConversionResult Convert(Image<Rgba32> image, string outputDirectory, ConversionOptions? options = null)
    {
        if (image is null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));
        }

        var resolvedOptions = options ?? new ConversionOptions();
        var outDir = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outDir);

        return ConvertImage(image, null, outDir, resolvedOptions);
    }

    private static ConversionResult ConvertImage(
        Image<Rgba32> sourceImage,
        string? sourceFilePath,
        string outputDirectory,
        ConversionOptions options)
    {
        var tileWidth = options.TileWidth > 0 ? options.TileWidth : 16;
        var tileHeight = options.TileHeight > 0 ? options.TileHeight : 16;

        var baseName = !string.IsNullOrWhiteSpace(options.Name)
            ? options.Name
            : (sourceFilePath != null ? Path.GetFileNameWithoutExtension(sourceFilePath) : "map");

        // Apply ColorKey if specified
        var workingImage = sourceImage.Clone();
        if (options.ColorKey.HasValue)
        {
            var key = options.ColorKey.Value;
            workingImage.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < accessor.Width; x++)
                    {
                        var p = row[x];
                        if (p.R == key.R && p.G == key.G && p.B == key.B)
                        {
                            row[x] = new Rgba32(0, 0, 0, 0);
                        }
                    }
                }
            });
        }

        var origWidth = workingImage.Width;
        var origHeight = workingImage.Height;

        // Pad image if dimensions are not multiples of tile size
        var padX = (tileWidth - (origWidth % tileWidth)) % tileWidth;
        var padY = (tileHeight - (origHeight % tileHeight)) % tileHeight;
        Image<Rgba32> processedImage = workingImage;
        if (padX > 0 || padY > 0)
        {
            var padded = new Image<Rgba32>(origWidth + padX, origHeight + padY);
            processedImage.ProcessPixelRows(padded, (srcAccessor, dstAccessor) =>
            {
                for (var y = 0; y < srcAccessor.Height; y++)
                {
                    var srcRow = srcAccessor.GetRowSpan(y);
                    var dstRow = dstAccessor.GetRowSpan(y);
                    srcRow.CopyTo(dstRow);
                }
            });
            processedImage.Dispose();
            processedImage = padded;
        }

        var mapWidth = processedImage.Width / tileWidth;
        var mapHeight = processedImage.Height / tileHeight;

        // Slicing and deduplicating
        var uniqueTiles = new List<Tile>();
        var tileToGid = new Dictionary<Tile, int>();
        var grid = new int[mapHeight, mapWidth];
        var transparentCellsCount = 0;

        if (options.EmptyMode == EmptyMode.Tile)
        {
            var emptyTile = Tile.CreateEmpty(tileWidth, tileHeight);
            uniqueTiles.Add(emptyTile);
            tileToGid[emptyTile] = 1;
        }

        for (var r = 0; r < mapHeight; r++)
        {
            for (var c = 0; c < mapWidth; c++)
            {
                var tile = Tile.FromImage(processedImage, c * tileWidth, r * tileHeight, tileWidth, tileHeight);
                var isTrans = tile.IsTransparent(options.ColorKey);
                if (isTrans)
                {
                    transparentCellsCount++;
                }

                if (isTrans && options.EmptyMode == EmptyMode.Gid0)
                {
                    grid[r, c] = 0;
                }
                else if (isTrans && options.EmptyMode == EmptyMode.Tile)
                {
                    grid[r, c] = 1;
                }
                else
                {
                    if (!tileToGid.TryGetValue(tile, out var gid))
                    {
                        uniqueTiles.Add(tile);
                        gid = uniqueTiles.Count; // 1-based GID
                        tileToGid[tile] = gid;
                    }
                    grid[r, c] = gid;
                }
            }
        }

        // Determine tileset columns
        int columns;
        if (options.AutoSquareColumns)
        {
            columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(uniqueTiles.Count)));
        }
        else if (options.Columns.HasValue && options.Columns.Value > 0)
        {
            columns = options.Columns.Value;
        }
        else
        {
            columns = uniqueTiles.Count >= 16 ? 16 : Math.Max(1, uniqueTiles.Count);
        }

        var rows = Math.Max(1, (int)Math.Ceiling((double)uniqueTiles.Count / columns));
        var tileCount = columns * rows;

        // Pack unique tiles into tileset image
        var sheetWidth = columns * tileWidth;
        var sheetHeight = rows * tileHeight;
        using var tilesetSheet = new Image<Rgba32>(sheetWidth, sheetHeight);

        for (var idx = 0; idx < uniqueTiles.Count; idx++)
        {
            var col = idx % columns;
            var row = idx / columns;
            uniqueTiles[idx].CopyTo(tilesetSheet, col * tileWidth, row * tileHeight);
        }

        // Determine output file paths
        var tilesetImageFileName = !string.IsNullOrWhiteSpace(options.TilesetImageName)
            ? options.TilesetImageName
            : $"{baseName}_tiles.png";
        var tsxFileName = !string.IsNullOrWhiteSpace(options.TsxName)
            ? options.TsxName
            : $"{baseName}.tsx";
        var tmxFileName = !string.IsNullOrWhiteSpace(options.TmxName)
            ? options.TmxName
            : $"{baseName}.tmx";

        var tilesetImagePath = Path.Combine(outputDirectory, tilesetImageFileName);
        var tsxPath = Path.Combine(outputDirectory, tsxFileName);
        var tmxPath = Path.Combine(outputDirectory, tmxFileName);

        if (sourceFilePath != null &&
            string.Equals(Path.GetFullPath(tilesetImagePath), Path.GetFullPath(sourceFilePath), StringComparison.OrdinalIgnoreCase) &&
            !options.Overwrite)
        {
            throw new InvalidOperationException(
                $"Target tileset image would overwrite input image: '{tilesetImagePath}'. " +
                "Specify a different name via ConversionOptions.TilesetImageName or enable Overwrite.");
        }

        // Save tileset PNG
        tilesetSheet.SaveAsPng(tilesetImagePath);

        // Generate TSX
        var imageRelToTsx = Path.GetRelativePath(Path.GetDirectoryName(tsxPath)!, tilesetImagePath).Replace('\\', '/');
        var tsxContent = TsxWriter.Generate(
            tilesetName: baseName,
            tileWidth: tileWidth,
            tileHeight: tileHeight,
            tileCount: tileCount,
            columns: columns,
            imageRelativePath: imageRelToTsx,
            imageWidth: sheetWidth,
            imageHeight: sheetHeight);
        File.WriteAllText(tsxPath, tsxContent);

        // Generate TMX
        var tsxRelToTmx = Path.GetRelativePath(Path.GetDirectoryName(tmxPath)!, tsxPath).Replace('\\', '/');
        var tmxContent = TmxWriter.Generate(
            mapWidth: mapWidth,
            mapHeight: mapHeight,
            tileWidth: tileWidth,
            tileHeight: tileHeight,
            tsxRelativePath: tsxRelToTmx,
            grid: grid,
            layerName: options.LayerName,
            retrosharp: options.RetrosharpProperties,
            streamY: options.StreamY,
            worldY: options.WorldY,
            worldHeight: options.WorldHeight);
        File.WriteAllText(tmxPath, tmxContent);

        // Verification
        var verified = true;
        if (options.Verify)
        {
            using var reconstructed = new Image<Rgba32>(processedImage.Width, processedImage.Height);
            for (var r = 0; r < mapHeight; r++)
            {
                for (var c = 0; c < mapWidth; c++)
                {
                    var gid = grid[r, c];
                    if (gid > 0)
                    {
                        var tile = uniqueTiles[gid - 1];
                        tile.CopyTo(reconstructed, c * tileWidth, r * tileHeight);
                    }
                }
            }

            // Compare pixel by pixel
            processedImage.ProcessPixelRows(reconstructed, (origAccessor, reconAccessor) =>
            {
                for (var y = 0; y < origAccessor.Height; y++)
                {
                    var origRow = origAccessor.GetRowSpan(y);
                    var reconRow = reconAccessor.GetRowSpan(y);
                    for (var x = 0; x < origAccessor.Width; x++)
                    {
                        if (origRow[x] != reconRow[x])
                        {
                            verified = false;
                            return;
                        }
                    }
                }
            });

            if (!verified)
            {
                throw new InvalidOperationException("Verification failed: Reconstructed map image does not match source image pixels!");
            }
        }

        if (!ReferenceEquals(processedImage, workingImage))
        {
            processedImage.Dispose();
        }

        return new ConversionResult(
            TilesetImagePath: tilesetImagePath,
            TsxPath: tsxPath,
            TmxPath: tmxPath,
            SourceWidth: origWidth,
            SourceHeight: origHeight,
            MapWidth: mapWidth,
            MapHeight: mapHeight,
            UniqueTilesCount: uniqueTiles.Count,
            TilesetColumns: columns,
            TilesetRows: rows,
            TileCount: tileCount,
            TransparentCellsCount: transparentCellsCount,
            VerifiedLossless: verified);
    }
}

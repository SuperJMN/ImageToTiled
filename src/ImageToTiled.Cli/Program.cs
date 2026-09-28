namespace ImageToTiled.Cli;

using System;
using System.Globalization;
using CommandLine;
using SixLabors.ImageSharp.PixelFormats;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("optimize", StringComparison.OrdinalIgnoreCase))
        {
            var remaining = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Skip(args, 1));
            return Parser.Default.ParseArguments<CliOptions>(remaining)
                .MapResult(opts => { opts.Optimize = true; return Run(opts); }, _ => 1);
        }

        return Parser.Default.ParseArguments<CliOptions>(args)
            .MapResult(Run, _ => 1);
    }

    private static int Run(CliOptions opts)
    {
        try
        {
            var inputPath = opts.InputPath;
            var isTmx = opts.Optimize ||
                        inputPath.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase) ||
                        (System.IO.Directory.Exists(inputPath) && System.IO.Directory.GetFiles(inputPath, "*.tmx").Length > 0);

            if (isTmx)
            {
                return RunOptimize(opts);
            }

            return RunConvert(opts);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int RunOptimize(CliOptions opts)
    {
        int? cols = null;
        if (!string.IsNullOrWhiteSpace(opts.Columns) && int.TryParse(opts.Columns, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c))
        {
            cols = c;
        }

        var emptyMode = opts.EmptyMode?.Trim().ToLowerInvariant() switch
        {
            "tile" => EmptyMode.Tile,
            "none" => EmptyMode.None,
            _ => EmptyMode.Gid0
        };

        var optOptions = new OptimizationOptions
        {
            Tolerance = opts.Tolerance > 0 ? opts.Tolerance : 55,
            AlphaThreshold = opts.AlphaThreshold,
            Columns = cols,
            OutputDirectory = opts.OutputDirectory,
            EmptyMode = emptyMode,
            PreserveUnusedTiles = opts.PreserveUnused,
            Verify = !opts.NoVerify
        };

        var result = TiledMapOptimizer.Optimize(opts.InputPath, optOptions);

        Console.WriteLine($"Successfully optimized TMX map '{result.TmxPath}':");
        Console.WriteLine($"  Original Tileset: {result.OriginalTileCount} tiles");
        Console.WriteLine($"  Optimized Tiles:  {result.OptimizedTileCount} tiles (sheet: {result.TilesetColumns} cols x {result.TilesetRows} rows)");
        Console.WriteLine($"  Tolerance:        {optOptions.Tolerance} (alpha threshold: {optOptions.AlphaThreshold})");
        Console.WriteLine($"  Layers Updated:   {result.LayersUpdatedCount}");
        Console.WriteLine($"  Tileset Image:    {result.TilesetImagePath}");
        Console.WriteLine($"  Tileset (TSX):    {result.TsxPath}");
        Console.WriteLine($"  Map (TMX):        {result.TmxPath}");
        if (result.VerifiedLossless)
        {
            Console.WriteLine(optOptions.Tolerance == 0
                ? "  Pixel Fidelity:   100% EXACT LOSSLESS MATCH (Verified)"
                : $"  Pixel Fidelity:   VERIFIED (all map cells match original within tolerance {optOptions.Tolerance})");
        }

        return 0;
    }

    private static int RunConvert(CliOptions opts)
    {
        var tileW = opts.TileSize ?? opts.TileWidth;
        var tileH = opts.TileSize ?? opts.TileHeight;

            var autoSquare = false;
            int? cols = null;
            var colStr = (opts.Columns ?? "").Trim().ToLowerInvariant();
            if (colStr is "square" or "auto")
            {
                autoSquare = true;
            }
            else if (int.TryParse(colStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCols))
            {
                cols = parsedCols;
            }
            else
            {
                Console.Error.WriteLine($"Error: Invalid columns value '{opts.Columns}'. Expected an integer or 'square'.");
                return 1;
            }

            var emptyMode = opts.EmptyMode?.Trim().ToLowerInvariant() switch
            {
                "gid0" => EmptyMode.Gid0,
                "none" => EmptyMode.None,
                _ => EmptyMode.Tile
            };

            Rgba32? colorKey = null;
            if (!string.IsNullOrWhiteSpace(opts.ColorKey))
            {
                var hex = opts.ColorKey.Trim().TrimStart('#');
                if (hex.Length == 6 &&
                    byte.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                    byte.TryParse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                    byte.TryParse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                {
                    colorKey = new Rgba32(r, g, b, 255);
                }
                else
                {
                    Console.Error.WriteLine($"Error: Invalid color-key '{opts.ColorKey}'. Expected #RRGGBB hex format.");
                    return 1;
                }
            }

            var conversionOptions = new ConversionOptions
            {
                TileWidth = tileW,
                TileHeight = tileH,
                Columns = cols,
                AutoSquareColumns = autoSquare,
                EmptyMode = emptyMode,
                ColorKey = colorKey,
                LayerName = opts.LayerName,
                RetrosharpProperties = !opts.NoRetrosharp && opts.Retrosharp,
                StreamY = opts.StreamY,
                WorldY = opts.WorldY,
                WorldHeight = opts.WorldHeight,
                Name = opts.Name,
                TilesetImageName = opts.TilesetImage,
                TmxName = opts.Tmx,
                TsxName = opts.Tsx,
                Tolerance = opts.Tolerance,
                AlphaThreshold = opts.AlphaThreshold,
                Overwrite = opts.Force,
                Verify = !opts.NoVerify,
            };

            var result = ImageToTiledConverter.Convert(opts.ImagePath, opts.OutputDirectory, conversionOptions);

            Console.WriteLine($"Successfully migrated '{opts.ImagePath}' to Tiled map:");
            Console.WriteLine($"  Source Image:     {result.SourceWidth}x{result.SourceHeight} px");
            Console.WriteLine($"  Tile Size:        {tileW}x{tileH} px");
            Console.WriteLine($"  Map Dimensions:   {result.MapWidth}x{result.MapHeight} tiles ({result.MapWidth * result.MapHeight} total cells)");
            Console.WriteLine($"  Empty Mode:       {opts.EmptyMode} ({result.TransparentCellsCount} transparent cells)");
            Console.WriteLine($"  Tolerance:        {opts.Tolerance} (alpha threshold: {opts.AlphaThreshold})");
            Console.WriteLine($"  Unique Tiles:     {result.UniqueTilesCount} (sheet: {result.TilesetColumns} cols x {result.TilesetRows} rows = {result.TileCount} slots)");
            Console.WriteLine($"  Tileset Image:    {result.TilesetImagePath}");
            Console.WriteLine($"  Tileset (TSX):    {result.TsxPath}");
            Console.WriteLine($"  Map (TMX):        {result.TmxPath}");
            if (result.VerifiedLossless)
            {
                Console.WriteLine(opts.Tolerance == 0
                    ? "  Pixel Fidelity:   100% EXACT LOSSLESS MATCH (Verified)"
                    : $"  Pixel Fidelity:   VERIFIED (all pixels match within tolerance {opts.Tolerance})");
            }

            return 0;
    }
}

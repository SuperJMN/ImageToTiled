namespace ImageToTiled.Cli;

using CommandLine;

public sealed class CliOptions
{
    [Value(0, Required = true, MetaName = "image", HelpText = "Path to the input image file (PNG, etc.).")]
    public string ImagePath { get; set; } = string.Empty;

    [Option('o', "output-dir", Required = false, HelpText = "Output directory for generated files (default: same directory as input image).")]
    public string? OutputDirectory { get; set; }

    [Option('n', "name", Required = false, HelpText = "Base name for the map, tileset, and generated files (default: image filename without extension).")]
    public string? Name { get; set; }

    [Option("tile-size", Required = false, HelpText = "Tile size in pixels (sets both width and height to this value).")]
    public int? TileSize { get; set; }

    [Option("tile-width", Default = 16, HelpText = "Tile width in pixels.")]
    public int TileWidth { get; set; } = 16;

    [Option("tile-height", Default = 16, HelpText = "Tile height in pixels.")]
    public int TileHeight { get; set; } = 16;

    [Option("columns", Default = "16", HelpText = "Number of columns in the tileset image, or 'square'/'auto' for sqrt-based packing.")]
    public string Columns { get; set; } = "16";

    [Option("empty-mode", Default = "tile", HelpText = "How to treat transparent tiles: 'tile' (tile 0 in tileset, GID 1 in map; matches RetroSharp samples), 'gid0' (GID 0 in map, not in tileset; standard Tiled empty cell), 'none' (treat like regular tile).")]
    public string EmptyMode { get; set; } = "tile";

    [Option("color-key", Required = false, HelpText = "Hex color code (e.g. #FF00FF or FF00FF) to treat as transparent key.")]
    public string? ColorKey { get; set; }

    [Option("layer-name", Default = "world", HelpText = "Tile layer name in the TMX file.")]
    public string LayerName { get; set; } = "world";

    [Option("tileset-image", Required = false, HelpText = "Custom filename for the generated tileset PNG.")]
    public string? TilesetImage { get; set; }

    [Option("tmx", Required = false, HelpText = "Custom filename for the generated TMX map file.")]
    public string? Tmx { get; set; }

    [Option("tsx", Required = false, HelpText = "Custom filename for the generated TSX tileset file.")]
    public string? Tsx { get; set; }

    [Option("retrosharp", Default = true, HelpText = "Include RetroSharp custom properties in TMX.")]
    public bool Retrosharp { get; set; } = true;

    [Option("no-retrosharp", Required = false, HelpText = "Omit RetroSharp custom properties from TMX.")]
    public bool NoRetrosharp { get; set; }

    [Option("stream-y", Default = 0, HelpText = "Value for retrosharpStreamY custom property.")]
    public int StreamY { get; set; } = 0;

    [Option("world-y", Default = 0, HelpText = "Value for retrosharpWorldY custom property.")]
    public int WorldY { get; set; } = 0;

    [Option("world-height", Required = false, HelpText = "Value for retrosharpWorldHeight custom property (default: map height).")]
    public int? WorldHeight { get; set; }

    [Option('f', "force", Required = false, HelpText = "Force overwrite existing files if they exist.")]
    public bool Force { get; set; }

    [Option("no-verify", Required = false, HelpText = "Skip pixel-match verification.")]
    public bool NoVerify { get; set; }
}

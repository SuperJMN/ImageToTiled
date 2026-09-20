namespace ImageToTiled;

using SixLabors.ImageSharp.PixelFormats;

public sealed record ConversionOptions
{
    public int TileWidth { get; init; } = 16;
    public int TileHeight { get; init; } = 16;
    public int? Columns { get; init; }
    public bool AutoSquareColumns { get; init; }
    public EmptyMode EmptyMode { get; init; } = EmptyMode.Tile;
    public Rgba32? ColorKey { get; init; }
    public string LayerName { get; init; } = "world";
    public bool RetrosharpProperties { get; init; } = true;
    public int StreamY { get; init; } = 0;
    public int WorldY { get; init; } = 0;
    public int? WorldHeight { get; init; }
    public string? Name { get; init; }
    public string? TilesetImageName { get; init; }
    public string? TmxName { get; init; }
    public string? TsxName { get; init; }
    public bool Overwrite { get; init; }
    public bool Verify { get; init; } = true;
}

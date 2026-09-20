namespace ImageToTiled;

public sealed record ConversionResult(
    string TilesetImagePath,
    string TsxPath,
    string TmxPath,
    int SourceWidth,
    int SourceHeight,
    int MapWidth,
    int MapHeight,
    int UniqueTilesCount,
    int TilesetColumns,
    int TilesetRows,
    int TileCount,
    int TransparentCellsCount,
    bool VerifiedLossless
);

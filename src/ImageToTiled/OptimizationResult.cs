namespace ImageToTiled;

public sealed record OptimizationResult(
    string TmxPath,
    string TsxPath,
    string TilesetImagePath,
    int OriginalTileCount,
    int OptimizedTileCount,
    int TilesetColumns,
    int TilesetRows,
    int LayersUpdatedCount,
    bool VerifiedLossless);

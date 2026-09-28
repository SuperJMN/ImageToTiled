namespace ImageToTiled;

public sealed record OptimizationOptions
{
    /// <summary>
    /// Maximum allowed difference per RGB channel (0..255) for two tiles to be merged.
    /// Default is 15.
    /// </summary>
    public int Tolerance { get; init; } = 15;

    /// <summary>
    /// Alpha threshold (0..255) for transparency detection and noise filtering.
    /// Default is 16.
    /// </summary>
    public int AlphaThreshold { get; init; } = 16;

    /// <summary>
    /// Optional number of columns for the new optimized tileset image.
    /// If null, keeps the original columns count from the TSX.
    /// </summary>
    public int? Columns { get; init; }

    /// <summary>
    /// Output directory for optimized files. If null, files are overwritten in-place.
    /// </summary>
    public string? OutputDirectory { get; init; }

    /// <summary>
    /// Whether to preserve tiles in the tileset that are not used in any layer of the map.
    /// Default is false (unused tiles are pruned).
    /// </summary>
    public bool PreserveUnusedTiles { get; init; }

    /// <summary>
    /// Whether to verify that the optimized map matches the original within tolerance.
    /// Default is true.
    /// </summary>
    public bool Verify { get; init; } = true;
}

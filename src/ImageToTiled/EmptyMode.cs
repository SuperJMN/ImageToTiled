namespace ImageToTiled;

public enum EmptyMode
{
    /// <summary>
    /// Tile 0 in the tileset is reserved for a transparent tile (GID 1 in the map).
    /// All cells in the map have a valid GID >= 1.
    /// Compatible with RetroSharp's full-grid world expectations.
    /// </summary>
    Tile,

    /// <summary>
    /// Fully transparent cells get GID 0 in the map and are not added to the tileset.
    /// Native Tiled empty cell behavior.
    /// </summary>
    Gid0,

    /// <summary>
    /// No special empty cell handling; transparent tiles are treated as regular graphic tiles.
    /// </summary>
    None
}

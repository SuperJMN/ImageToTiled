namespace ImageToTiled;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public static class TiledMapOptimizer
{
    public static OptimizationResult Optimize(string tmxPathOrDirectory, OptimizationOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(tmxPathOrDirectory))
        {
            throw new ArgumentException("Path cannot be empty.", nameof(tmxPathOrDirectory));
        }

        var fullPath = Path.GetFullPath(tmxPathOrDirectory);
        string tmxFilePath;

        if (Directory.Exists(fullPath))
        {
            var tmxFiles = Directory.GetFiles(fullPath, "*.tmx");
            if (tmxFiles.Length == 0)
            {
                throw new FileNotFoundException($"No .tmx map files found in directory '{fullPath}'.");
            }
            if (tmxFiles.Length > 1)
            {
                throw new InvalidOperationException($"Multiple .tmx files found in '{fullPath}'. Please specify which one to optimize: {string.Join(", ", tmxFiles.Select(Path.GetFileName))}");
            }
            tmxFilePath = tmxFiles[0];
        }
        else if (File.Exists(fullPath))
        {
            tmxFilePath = fullPath;
        }
        else
        {
            throw new FileNotFoundException($"TMX map file or directory not found: '{fullPath}'");
        }

        var opt = options ?? new OptimizationOptions();
        return OptimizeMap(tmxFilePath, opt);
    }

    private static OptimizationResult OptimizeMap(string tmxPath, OptimizationOptions options)
    {
        var tmxDir = Path.GetDirectoryName(tmxPath)!;
        var tmxDoc = XDocument.Load(tmxPath);
        var mapRoot = tmxDoc.Root ?? throw new InvalidOperationException($"Invalid TMX root in '{tmxPath}'.");

        var tilesetElem = mapRoot.Element("tileset")
            ?? throw new InvalidOperationException($"No <tileset> element found in '{tmxPath}'.");

        var firstGid = int.Parse(tilesetElem.Attribute("firstgid")?.Value ?? "1");
        var tsxSource = tilesetElem.Attribute("source")?.Value
            ?? throw new NotSupportedException("Only external TSX tilesets are currently supported for optimization.");

        var tsxPath = Path.GetFullPath(Path.Combine(tmxDir, tsxSource));
        if (!File.Exists(tsxPath))
        {
            throw new FileNotFoundException($"Referenced TSX file not found: '{tsxPath}'");
        }

        var tsxDoc = XDocument.Load(tsxPath);
        var tsxRoot = tsxDoc.Root ?? throw new InvalidOperationException($"Invalid TSX root in '{tsxPath}'.");

        var tileWidth = int.Parse(tsxRoot.Attribute("tilewidth")?.Value ?? "16");
        var tileHeight = int.Parse(tsxRoot.Attribute("tileheight")?.Value ?? "16");
        var originalColumns = int.Parse(tsxRoot.Attribute("columns")?.Value ?? "16");
        var originalTileCount = int.Parse(tsxRoot.Attribute("tilecount")?.Value ?? "0");

        var imageElem = tsxRoot.Element("image")
            ?? throw new InvalidOperationException($"No <image> element found in TSX '{tsxPath}'.");

        var imageSource = imageElem.Attribute("source")?.Value
            ?? throw new InvalidOperationException($"No source attribute in <image> element of '{tsxPath}'.");

        var imagePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(tsxPath)!, imageSource));
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException($"Tileset image not found: '{imagePath}'");
        }

        // Load tileset texture and slice original tiles
        using var sheetImage = Image.Load<Rgba32>(imagePath);
        var originalTiles = new List<Tile>(originalTileCount);

        for (var i = 0; i < originalTileCount; i++)
        {
            var col = i % originalColumns;
            var row = i / originalColumns;
            var startX = col * tileWidth;
            var startY = row * tileHeight;

            if (startX + tileWidth <= sheetImage.Width && startY + tileHeight <= sheetImage.Height)
            {
                originalTiles.Add(Tile.FromImage(sheetImage, startX, startY, tileWidth, tileHeight, options.AlphaThreshold));
            }
            else
            {
                originalTiles.Add(Tile.CreateEmpty(tileWidth, tileHeight));
            }
        }

        // Separate visual tile layers from collision layers
        var allLayers = mapRoot.Elements("layer").ToList();
        var visualLayers = allLayers
            .Where(l => !string.Equals(l.Attribute("name")?.Value, "collision", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Scan visual layers to find used GIDs
        var usedGids = new HashSet<int>();
        foreach (var layer in visualLayers)
        {
            var data = layer.Element("data");
            if (data == null || data.Attribute("encoding")?.Value != "csv")
            {
                continue;
            }

            var csvText = data.Value.Trim();
            foreach (var row in csvText.Split('\n'))
            {
                var trimmedRow = row.Trim().TrimEnd(',');
                if (string.IsNullOrWhiteSpace(trimmedRow)) continue;

                foreach (var cell in trimmedRow.Split(','))
                {
                    if (uint.TryParse(cell.Trim(), out var rawVal))
                    {
                        var gid = (int)(rawVal & 0x1FFFFFFF);
                        if (gid >= firstGid && gid < firstGid + originalTileCount)
                        {
                            usedGids.Add(gid);
                        }
                    }
                }
            }
        }

        // Deduplicate tiles within tolerance
        var canonicalTiles = new List<Tile>();
        var remap = new Dictionary<int, int>
        {
            [0] = 0 // empty cell remains GID 0
        };

        // If EmptyMode is Tile, preserve an empty tile at canonical index 0 (GID = firstGid)
        if (options.EmptyMode == EmptyMode.Tile)
        {
            canonicalTiles.Add(Tile.CreateEmpty(tileWidth, tileHeight));
            remap[firstGid] = firstGid;
        }

        for (var i = 0; i < originalTiles.Count; i++)
        {
            var oldGid = firstGid + i;

            if (!usedGids.Contains(oldGid) && !options.PreserveUnusedTiles)
            {
                remap[oldGid] = 0;
                continue;
            }

            var tile = originalTiles[i];
            var isTransparent = tile.IsTransparent(null, options.AlphaThreshold);

            if (isTransparent && options.EmptyMode == EmptyMode.Gid0)
            {
                // Cell becomes empty cell GID 0; do not store in tileset
                remap[oldGid] = 0;
                continue;
            }

            if (isTransparent && options.EmptyMode == EmptyMode.Tile)
            {
                remap[oldGid] = firstGid;
                continue;
            }

            var matchedIndex = -1;
            var searchStart = (options.EmptyMode == EmptyMode.Tile) ? 1 : 0;
            for (var k = searchStart; k < canonicalTiles.Count; k++)
            {
                if (tile.Matches(canonicalTiles[k], options.Tolerance, options.AlphaThreshold))
                {
                    matchedIndex = k;
                    break;
                }
            }

            if (matchedIndex != -1)
            {
                remap[oldGid] = firstGid + matchedIndex;
            }
            else
            {
                canonicalTiles.Add(tile);
                remap[oldGid] = firstGid + canonicalTiles.Count - 1;
            }
        }

        // Ensure absolutely NO unused tiles in tileset when PreserveUnusedTiles is false
        if (!options.PreserveUnusedTiles && canonicalTiles.Count > 0)
        {
            var referencedNewGids = usedGids
                .Select(g => remap.TryGetValue(g, out var mapped) ? mapped : 0)
                .Where(g => g >= firstGid)
                .ToHashSet();

            if (referencedNewGids.Count < canonicalTiles.Count)
            {
                var compactCanonical = new List<Tile>();
                var secondRemap = new Dictionary<int, int> { [0] = 0 };

                for (var k = 0; k < canonicalTiles.Count; k++)
                {
                    var canonicalGid = firstGid + k;
                    if (referencedNewGids.Contains(canonicalGid))
                    {
                        compactCanonical.Add(canonicalTiles[k]);
                        secondRemap[canonicalGid] = firstGid + compactCanonical.Count - 1;
                    }
                    else
                    {
                        secondRemap[canonicalGid] = 0;
                    }
                }

                foreach (var key in remap.Keys.ToList())
                {
                    remap[key] = secondRemap.TryGetValue(remap[key], out var finalGid) ? finalGid : 0;
                }

                canonicalTiles = compactCanonical;
            }
        }

        // Determine destination paths
        string outTmxPath, outTsxPath, outImagePath;
        if (!string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            var outDir = Path.GetFullPath(options.OutputDirectory);
            Directory.CreateDirectory(outDir);
            outTmxPath = Path.Combine(outDir, Path.GetFileName(tmxPath));
            outTsxPath = Path.Combine(outDir, Path.GetFileName(tsxPath));
            outImagePath = Path.Combine(outDir, Path.GetFileName(imagePath));
        }
        else
        {
            outTmxPath = tmxPath;
            outTsxPath = tsxPath;
            outImagePath = imagePath;
        }

        // Determine new tileset dimensions
        var newColumns = options.Columns ?? originalColumns;
        if (newColumns <= 0) newColumns = 16;
        var canonicalCount = Math.Max(1, canonicalTiles.Count);
        var newRows = Math.Max(1, (int)Math.Ceiling((double)canonicalCount / newColumns));
        var newTileCount = newColumns * newRows;

        // Pack new tileset sheet
        var sheetW = newColumns * tileWidth;
        var sheetH = newRows * tileHeight;
        using var newSheet = new Image<Rgba32>(sheetW, sheetH);

        for (var idx = 0; idx < canonicalTiles.Count; idx++)
        {
            var c = idx % newColumns;
            var r = idx / newColumns;
            canonicalTiles[idx].CopyTo(newSheet, c * tileWidth, r * tileHeight);
        }

        newSheet.SaveAsPng(outImagePath);

        // Update TSX
        tsxRoot.SetAttributeValue("tilecount", newTileCount);
        tsxRoot.SetAttributeValue("columns", newColumns);
        imageElem.SetAttributeValue("width", sheetW);
        imageElem.SetAttributeValue("height", sheetH);

        var relImage = Path.GetRelativePath(Path.GetDirectoryName(outTsxPath)!, outImagePath).Replace('\\', '/');
        imageElem.SetAttributeValue("source", relImage);

        var xmlSettings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true
        };

        using (var writer = XmlWriter.Create(outTsxPath, xmlSettings))
        {
            tsxDoc.Save(writer);
        }

        // Update ONLY visual TMX layers (leaving collision and other layers untouched)
        var layersUpdated = 0;
        foreach (var layer in visualLayers)
        {
            var data = layer.Element("data");
            if (data == null || data.Attribute("encoding")?.Value != "csv")
            {
                continue;
            }

            var lines = data.Value.Trim().Split('\n');
            var newLines = new List<string>(lines.Length);

            for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
            {
                var line = lines[lineIdx].Trim().TrimEnd(',');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cells = line.Split(',');
                var newCells = new string[cells.Length];

                for (var cIdx = 0; cIdx < cells.Length; cIdx++)
                {
                    if (uint.TryParse(cells[cIdx].Trim(), out var rawVal))
                    {
                        var flags = rawVal & 0xE0000000;
                        var gid = (int)(rawVal & 0x1FFFFFFF);

                        if (gid >= firstGid && gid < firstGid + originalTileCount)
                        {
                            var mappedGid = remap.TryGetValue(gid, out var m) ? m : gid;
                            newCells[cIdx] = (flags | (uint)mappedGid).ToString();
                        }
                        else
                        {
                            newCells[cIdx] = rawVal.ToString();
                        }
                    }
                    else
                    {
                        newCells[cIdx] = cells[cIdx];
                    }
                }

                var endsWithComma = lineIdx < lines.Length - 1;
                newLines.Add(string.Join(",", newCells) + (endsWithComma ? "," : ""));
            }

            data.Value = "\n" + string.Join("\n", newLines) + "\n";
            layersUpdated++;
        }

        var relTsx = Path.GetRelativePath(Path.GetDirectoryName(outTmxPath)!, outTsxPath).Replace('\\', '/');
        tilesetElem.RemoveNodes();
        tilesetElem.RemoveAttributes();
        tilesetElem.SetAttributeValue("firstgid", firstGid);
        tilesetElem.SetAttributeValue("source", relTsx);

        using (var writer = XmlWriter.Create(outTmxPath, xmlSettings))
        {
            tmxDoc.Save(writer);
        }

        // Verification
        var verified = true;
        if (options.Verify)
        {
            foreach (var kvp in remap)
            {
                var oldGid = kvp.Key;
                var newGid = kvp.Value;

                if (oldGid < firstGid || oldGid >= firstGid + originalTiles.Count)
                {
                    continue;
                }

                var oldTile = originalTiles[oldGid - firstGid];

                if (newGid == 0)
                {
                    if (!oldTile.IsTransparent(null, options.AlphaThreshold))
                    {
                        verified = false;
                        throw new InvalidOperationException($"Verification failed: Non-transparent old GID {oldGid} mapped to empty GID 0.");
                    }
                    continue;
                }

                if (newGid >= firstGid && newGid < firstGid + canonicalTiles.Count)
                {
                    var newTile = canonicalTiles[newGid - firstGid];
                    if (!oldTile.Matches(newTile, options.Tolerance, options.AlphaThreshold))
                    {
                        verified = false;
                        throw new InvalidOperationException(
                            $"Verification failed: Tile GID {oldGid} mapped to GID {newGid} exceeds tolerance {options.Tolerance}.");
                    }
                }
            }
        }

        return new OptimizationResult(
            TmxPath: outTmxPath,
            TsxPath: outTsxPath,
            TilesetImagePath: outImagePath,
            OriginalTileCount: originalTileCount,
            OptimizedTileCount: canonicalTiles.Count,
            TilesetColumns: newColumns,
            TilesetRows: newRows,
            LayersUpdatedCount: layersUpdated,
            VerifiedLossless: verified);
    }
}

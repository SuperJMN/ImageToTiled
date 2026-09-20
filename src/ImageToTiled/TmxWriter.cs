namespace ImageToTiled;

using System.Text;

public static class TmxWriter
{
    public static string Generate(
        int mapWidth,
        int mapHeight,
        int tileWidth,
        int tileHeight,
        string tsxRelativePath,
        int[,] grid,
        string layerName = "world",
        bool retrosharp = true,
        int streamY = 0,
        int worldY = 0,
        int? worldHeight = null)
    {
        var resolvedWorldHeight = worldHeight ?? mapHeight;
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<map version=\"1.10\" tiledversion=\"1.12.2\" orientation=\"orthogonal\" renderorder=\"right-down\" width=\"{mapWidth}\" height=\"{mapHeight}\" tilewidth=\"{tileWidth}\" tileheight=\"{tileHeight}\" infinite=\"0\" nextlayerid=\"2\" nextobjectid=\"1\">");

        if (retrosharp)
        {
            sb.AppendLine(" <properties>");
            sb.AppendLine($"  <property name=\"retrosharpStreamY\" type=\"int\" value=\"{streamY}\"/>");
            sb.AppendLine($"  <property name=\"retrosharpWorldY\" type=\"int\" value=\"{worldY}\"/>");
            sb.AppendLine($"  <property name=\"retrosharpWorldHeight\" type=\"int\" value=\"{resolvedWorldHeight}\"/>");
            sb.AppendLine(" </properties>");
        }

        sb.AppendLine($" <tileset firstgid=\"1\" source=\"{tsxRelativePath}\"/>");
        sb.AppendLine($" <layer id=\"1\" name=\"{layerName}\" width=\"{mapWidth}\" height=\"{mapHeight}\">");
        sb.AppendLine("  <data encoding=\"csv\">");

        for (var r = 0; r < mapHeight; r++)
        {
            for (var c = 0; c < mapWidth; c++)
            {
                sb.Append(grid[r, c]);
                if (c < mapWidth - 1)
                {
                    sb.Append(',');
                }
            }

            if (r < mapHeight - 1)
            {
                sb.Append(',');
            }
            sb.AppendLine();
        }

        sb.AppendLine("  </data>");
        sb.AppendLine(" </layer>");
        sb.AppendLine("</map>");

        return sb.ToString();
    }
}

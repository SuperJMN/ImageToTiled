namespace ImageToTiled;

using System.Text;

public static class TsxWriter
{
    public static string Generate(
        string tilesetName,
        int tileWidth,
        int tileHeight,
        int tileCount,
        int columns,
        string imageRelativePath,
        int imageWidth,
        int imageHeight)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"{tilesetName}\" tilewidth=\"{tileWidth}\" tileheight=\"{tileHeight}\" tilecount=\"{tileCount}\" columns=\"{columns}\">");
        sb.AppendLine($" <image source=\"{imageRelativePath}\" width=\"{imageWidth}\" height=\"{imageHeight}\"/>");
        sb.AppendLine("</tileset>");
        return sb.ToString();
    }
}

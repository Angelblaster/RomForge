namespace PBP.Core.Models;

public class DiscInfo
{
    public DiskSource Source { get; set; } = null!;

    public string GameId { get; set; } = "SLUS00000";

    public string GameTitle { get; set; } = "Unknown";

    public byte[] TocData { get; set; } = [];
}
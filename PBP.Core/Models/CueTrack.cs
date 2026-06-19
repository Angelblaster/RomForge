namespace PBP.Core.Models;

public class CueTrack
{
    public int Number { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public long StartSector { get; set; }
    public long SectorCount { get; set; }
    public int SectorSize { get; set; } = 2352;
}
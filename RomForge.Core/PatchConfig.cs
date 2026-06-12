namespace RomForge.Core;

public enum OutputMode { Normal, Arcade }

public class PatchConfig
{
    public OutputMode OutputMode { get; set; } = OutputMode.Normal;

    public string? OutputFolder { get; set; } = null;
}
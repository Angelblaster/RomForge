namespace RomForge.Core.Models.Patch;

public class PatchPackageEntry
{
    public string SourceFileName { get; init; } = string.Empty;
    public string PatchBaseName { get; init; } = string.Empty;
    public string Crc { get; init; } = string.Empty;
}
namespace RomForge.Core.Models.Patch;

public class PatchPackage
{
    public string DisplayName { get; init; } = string.Empty;
    public string DatFileName { get; init; } = string.Empty;
    public List<PatchPackageEntry> Entries { get; init; } = [];
}
namespace RomForge.Core.Models.Patch;

public class SourceEntry
{
    public string DisplayName { get; init; } = string.Empty;
    public string? ZipPath { get; init; }
    public string EntryPath { get; init; } = string.Empty;
    public bool IsZipEntry => ZipPath is not null;
}
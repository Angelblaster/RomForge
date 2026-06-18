namespace RomForge.Core.Models.Patch;

public class PatchPackage
{
    public string DisplayName { get; init; } = string.Empty; // dat 안의 설명 텍스트 (예: 한국어 번역 v1.0)
    public string DatFileName { get; init; } = string.Empty; // 예: 01-Korean_Translation.dat
    public List<PatchPackageEntry> Entries { get; init; } = [];
}
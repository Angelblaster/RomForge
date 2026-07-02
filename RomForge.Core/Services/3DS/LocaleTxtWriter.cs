using _3DS.Core.Enums;
using System.IO;

namespace RomForge.Core.Services._3DS;

public static class LocaleTxtWriter
{
    public static async Task WriteAsync(string sdRoot, string titleId, LocaleRegion region, Locale3dsLanguage language, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sdRoot))
            throw new ArgumentException("SD 루트 경로가 비어있습니다.", nameof(sdRoot));

        if (string.IsNullOrEmpty(titleId) || titleId.Length != 16)
            throw new ArgumentException("타이틀 ID가 올바르지 않습니다.", nameof(titleId));

        if (region == LocaleRegion.None || language == Locale3dsLanguage.None)
            return;

        string dir = Path.Combine(sdRoot, "luma", "titles", titleId.ToUpperInvariant());

        Directory.CreateDirectory(dir);

        string path = Path.Combine(dir, "locale.txt");
        string content = $"{region} {language}";

        await File.WriteAllTextAsync(path, content, ct);
    }
}

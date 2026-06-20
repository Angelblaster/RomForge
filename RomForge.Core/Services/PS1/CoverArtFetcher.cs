using System.Net.Http;

namespace RomForge.Core.Services.PS1;

public static class CoverArtFetcher
{
    private const string BaseUrl = "https://raw.githubusercontent.com/sinjunyoung/psx-covers/main/covers/default";
    private static readonly HttpClient Http = new();

    /// <summary>
    /// gameId(대시 없는 내부 포맷, 예: SLUS00067)를 저장소 규칙(SLUS-00067)으로 바꿔서 다운로드.
    /// 실패하면 null -> 호출부에서 기본 리소스로 폴백.
    /// </summary>
    public static async Task<byte[]?> TryDownloadIconPngAsync(string gameId, CancellationToken ct = default)
    {
        var dashed = ToDashedSerial(gameId);
        if (dashed == null) return null;

        try
        {
            var jpgBytes = await Http.GetByteArrayAsync($"{BaseUrl}/{dashed}.jpg", ct);
            return ImageConversion.ToPng(jpgBytes);
        }
        catch
        {
            return null;
        }
    }

    private static string? ToDashedSerial(string gameId)
    {
        var m = System.Text.RegularExpressions.Regex.Match(gameId, @"^([A-Z]{4})(\d+)$");
        return m.Success ? $"{m.Groups[1].Value}-{m.Groups[2].Value}" : null;
    }
}
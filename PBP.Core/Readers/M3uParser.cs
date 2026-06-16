using PBP.Core.Models;

namespace PBP.Core.Readers;

/// <summary>
/// M3U 파서
/// .m3u 파일 읽어서 DiskSource 리스트 자동 생성
/// </summary>
public static class M3uParser
{
    /// <summary>
    /// M3U 파일 → DiskSource 리스트
    /// 순서 = 디스크 순서
    /// </summary>
    public static List<DiskSource> Parse(string m3uPath)
    {
        if (!File.Exists(m3uPath))
            throw new FileNotFoundException($"M3U 파일 없음: {m3uPath}");

        var baseDir = Path.GetDirectoryName(m3uPath) ?? ".";
        var result = new List<DiskSource>();

        foreach (var raw in File.ReadAllLines(m3uPath))
        {
            var line = raw.Trim();

            // 빈줄, 주석 스킵
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            // 상대경로 → 절대경로
            var fullPath = Path.IsPathRooted(line)
                ? line
                : Path.GetFullPath(Path.Combine(baseDir, line));

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"M3U 참조 파일 없음: {fullPath}");

            result.Add(ResolveDiskSource(fullPath));
        }

        if (result.Count == 0)
            throw new InvalidDataException($"M3U에 유효한 항목 없음: {m3uPath}");

        return result;
    }

    private static DiskSource ResolveDiskSource(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        return ext switch
        {
            ".cue" => DiskSource.FromBinCue(ResolveBinFromCue(path), path),
            ".iso" => DiskSource.FromIso(path),
            ".chd" => DiskSource.FromChd(path),
            _ => throw new NotSupportedException($"지원 안 하는 포맷: {ext}")
        };
    }

    /// <summary>
    /// CUE 파일에서 첫 번째 FILE 라인 파싱해서 BIN 경로 추출
    /// </summary>
    private static string ResolveBinFromCue(string cuePath)
    {
        var cueDir = Path.GetDirectoryName(cuePath) ?? ".";

        foreach (var line in File.ReadAllLines(cuePath))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("FILE", StringComparison.OrdinalIgnoreCase))
                continue;

            var start = trimmed.IndexOf('"');
            var end = trimmed.LastIndexOf('"');
            if (start >= 0 && end > start)
            {
                var binName = trimmed[(start + 1)..end];
                return Path.GetFullPath(Path.Combine(cueDir, binName));
            }
        }

        throw new InvalidDataException($"CUE에서 BIN 파일 못 찾음: {cuePath}");
    }
}

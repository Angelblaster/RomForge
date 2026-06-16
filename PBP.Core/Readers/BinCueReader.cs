using PBP.Core.Models;

namespace PBP.Core.Readers;

/// <summary>
/// BIN+CUE 멀티트랙 리더
/// CUE 파싱 후 트랙별 BIN을 섹터 단위로 읽음
/// </summary>
public class BinCueReader : IDiskReader, IDisposable
{
    private readonly List<CueTrack> _tracks;
    private readonly Dictionary<string, FileStream> _binStreams = [];
    private bool _disposed;

    public long TotalSectors { get; }
    public int SectorSize => 2352;

    public BinCueReader(string cuePath)
    {
        _tracks = ParseCue(cuePath);
        if (_tracks.Count == 0)
            throw new InvalidDataException($"CUE 파싱 실패: {cuePath}");

        TotalSectors = _tracks.Sum(t => t.SectorCount);

        // BIN 파일들 미리 열기
        var cueDir = Path.GetDirectoryName(cuePath) ?? ".";
        foreach (var binFile in _tracks.Select(t => t.File).Distinct())
        {
            var fullPath = Path.Combine(cueDir, binFile);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"BIN 파일 없음: {fullPath}");
            _binStreams[binFile] = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }

    public byte[] ReadSectors(long startSector, int count)
    {
        var result = new List<byte>();
        long remaining = count;
        long current = startSector;

        while (remaining > 0)
        {
            var track = FindTrack(current);
            if (track == null) break;

            long offsetInTrack = current - track.StartSector;
            long canRead = Math.Min(remaining, track.SectorCount - offsetInTrack);

            var stream = _binStreams[track.File];
            long fileOffset = offsetInTrack * track.SectorSize;
            stream.Seek(fileOffset, SeekOrigin.Begin);

            var buffer = new byte[canRead * track.SectorSize];
            stream.ReadExactly(buffer, 0, buffer.Length);
            result.AddRange(buffer);

            current += canRead;
            remaining -= canRead;
        }

        return [.. result];
    }

    private CueTrack? FindTrack(long sector)
        => _tracks.LastOrDefault(t => t.StartSector <= sector);

    private static List<CueTrack> ParseCue(string cuePath)
    {
        var tracks = new List<CueTrack>();
        var lines = File.ReadAllLines(cuePath);

        string currentFile = string.Empty;
        CueTrack? currentTrack = null;
        long currentSector = 0;
        int sectorSize = 2352;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line.StartsWith("FILE", StringComparison.OrdinalIgnoreCase))
            {
                // FILE "name.bin" BINARY
                var start = line.IndexOf('"');
                var end = line.LastIndexOf('"');
                if (start >= 0 && end > start)
                    currentFile = line[(start + 1)..end];
            }
            else if (line.StartsWith("TRACK", StringComparison.OrdinalIgnoreCase))
            {
                if (currentTrack != null)
                    tracks.Add(currentTrack);

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                int trackNum = int.Parse(parts[1]);
                string mode = parts.Length > 2 ? parts[2] : "MODE2/2352";

                sectorSize = mode.Contains("2352") ? 2352 : mode.Contains("2048") ? 2048 : 2352;

                currentTrack = new CueTrack
                {
                    Number = trackNum,
                    Mode = mode,
                    File = currentFile,
                    SectorSize = sectorSize,
                    StartSector = currentSector
                };
            }
            else if (line.StartsWith("INDEX 01", StringComparison.OrdinalIgnoreCase) && currentTrack != null)
            {
                // INDEX 01 MM:SS:FF
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var timeParts = parts[2].Split(':');
                    if (timeParts.Length == 3)
                    {
                        int mm = int.Parse(timeParts[0]);
                        int ss = int.Parse(timeParts[1]);
                        int ff = int.Parse(timeParts[2]);
                        long msf = mm * 60 * 75 + ss * 75 + ff;
                        currentTrack.StartSector = msf;
                    }
                }
            }
        }

        if (currentTrack != null)
            tracks.Add(currentTrack);

        // 섹터 카운트 계산
        for (int i = 0; i < tracks.Count; i++)
        {
            if (i + 1 < tracks.Count)
                tracks[i].SectorCount = tracks[i + 1].StartSector - tracks[i].StartSector;
            else
            {
                // 마지막 트랙: 파일 크기로 계산
                tracks[i].SectorCount = long.MaxValue; // ReadSectors에서 클램프됨
            }
        }

        return tracks;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var s in _binStreams.Values)
                s.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

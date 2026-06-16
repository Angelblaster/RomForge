namespace PBP.Core.Readers;

/// <summary>
/// ISO / 단일 BIN 파일 리더
/// ISO는 섹터 2048, BIN은 2352 자동 감지
/// </summary>
public class IsoReader : IDiskReader
{
    private readonly FileStream _stream;
    private bool _disposed;

    public long TotalSectors { get; }
    public int SectorSize { get; }

    public IsoReader(string path)
    {
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        SectorSize = DetectSectorSize(_stream);
        TotalSectors = _stream.Length / SectorSize;
    }

    /// <summary>
    /// 파일 크기로 섹터 크기 추정
    /// 2352로 나눠 떨어지면 BIN, 아니면 ISO(2048)
    /// </summary>
    private static int DetectSectorSize(FileStream fs)
    {
        return fs.Length % 2352 == 0 ? 2352 : 2048;
    }

    public byte[] ReadSectors(long startSector, int count)
    {
        long offset = startSector * SectorSize;
        int length = count * SectorSize;

        // 파일 끝 넘치면 클램프
        long available = _stream.Length - offset;
        if (available <= 0) return [];
        if (available < length) length = (int)available;

        var buffer = new byte[length];
        _stream.Seek(offset, SeekOrigin.Begin);
        _stream.ReadExactly(buffer, 0, length);
        return buffer;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _stream.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

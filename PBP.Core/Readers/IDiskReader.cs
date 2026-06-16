namespace PBP.Core.Readers;

/// <summary>
/// 디스크 이미지에서 섹터 단위로 읽는 추상화
/// </summary>
public interface IDiskReader : IDisposable
{
    /// <summary>총 섹터 수</summary>
    long TotalSectors { get; }

    /// <summary>섹터 크기 (보통 2048 or 2352)</summary>
    int SectorSize { get; }

    /// <summary>섹터 읽기</summary>
    byte[] ReadSectors(long startSector, int count);
}

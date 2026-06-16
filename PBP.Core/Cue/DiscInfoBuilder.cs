using PBP.Core.Models;
using PBP.Core.Readers;

namespace PBP.Core.Cue;

/// <summary>
/// DiskSource → DiscInfo 자동 생성
/// TOC, ISO 크기, GameId 전부 자동 계산
/// </summary>
public static class DiscInfoBuilder
{
    /// <summary>
    /// GameId 자동 추출 버전 (SYSTEM.CNF 파싱)
    /// gameTitle만 직접 입력
    /// </summary>
    public static DiscInfo Build(DiskSource source, string gameTitle)
    {
        var gameId = GameIdReader.ReadFromDisk(source);
        return Build(source, gameId, gameTitle);
    }

    /// <summary>
    /// GameId 직접 지정 버전
    /// </summary>
    public static DiscInfo Build(DiskSource source, string gameId, string gameTitle)
    {
        byte[] tocData = source.Type switch
        {
            DiskSourceType.Bin => BuildFromBinCue(source),
            DiskSourceType.Iso => BuildFromIso(source),
            DiskSourceType.Chd => BuildFromChd(source),
            _ => throw new NotSupportedException()
        };

        return new DiscInfo
        {
            Source = source,
            GameId = gameId,
            GameTitle = gameTitle,
            TocData = tocData
        };
    }

    private static byte[] BuildFromBinCue(DiskSource source)
    {
        var cuePath = source.CuePath
            ?? throw new ArgumentException("BIN+CUE는 CuePath 필수");
        long isoSize = new FileInfo(source.FilePath).Length;
        return TocBuilder.FromCue(cuePath, isoSize);
    }

    private static byte[] BuildFromIso(DiskSource source)
    {
        long isoSize = new FileInfo(source.FilePath).Length;
        return TocBuilder.FromDummy(isoSize);
    }

    private static byte[] BuildFromChd(DiskSource source)
    {
        using var reader = DiskReaderFactory.Create(source);
        long isoSize = reader.TotalSectors * reader.SectorSize;
        return TocBuilder.FromDummy(isoSize);
    }
}
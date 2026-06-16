namespace PBP.Core.Models;

/// <summary>
/// PBP 패킹 옵션
/// </summary>
public class PbpPackOptions
{
    /// <summary>
    /// zlib 압축 레벨 0~9 (0=무압축, 9=최고압축)
    /// popstation 기본값 = 5
    /// </summary>
    public int CompressionLevel { get; set; } = 9;
}

/// <summary>
/// PBP 에셋 (헤더에 박히는 파일들)
/// </summary>
public class PbpAssets
{
    public byte[]? Icon0Png { get; set; }
    public byte[]? Icon1Pmf { get; set; }
    public byte[]? Pic0Png  { get; set; }
    public byte[]? Pic1Png  { get; set; }
    public byte[]? Snd0At3  { get; set; }

    /// <summary>
    /// DATA.PSP - PSP 에뮬레이터 바이너리, BASE.PBP에서 추출
    /// </summary>
    public byte[]? DataPsp  { get; set; }
}

/// <summary>
/// 디스크 입력 소스 타입
/// </summary>
public enum DiskSourceType
{
    Iso,
    Bin,    // BIN+CUE
    Chd
}

/// <summary>
/// 디스크 하나 (멀티디스크 리스트의 원소)
/// </summary>
public class DiskSource
{
    public DiskSourceType Type     { get; init; }
    public string FilePath         { get; init; } = string.Empty;
    public string? CuePath         { get; init; }

    public static DiskSource FromIso(string path)
        => new() { Type = DiskSourceType.Iso, FilePath = path };

    public static DiskSource FromBinCue(string binPath, string cuePath)
        => new() { Type = DiskSourceType.Bin, FilePath = binPath, CuePath = cuePath };

    public static DiskSource FromChd(string path)
        => new() { Type = DiskSourceType.Chd, FilePath = path };
}

/// <summary>
/// 디스크 메타데이터
/// </summary>
public class DiscInfo
{
    public DiskSource Source    { get; set; } = null!;

    /// <summary>게임 ID (예: SLUS00000)</summary>
    public string GameId        { get; set; } = "SLUS00000";

    /// <summary>게임 타이틀</summary>
    public string GameTitle     { get; set; } = "Unknown";

    /// <summary>CUE 파싱된 TOC 데이터</summary>
    public byte[] TocData       { get; set; } = [];
}

/// <summary>
/// CUE 트랙 정보
/// </summary>
public class CueTrack
{
    public int    Number      { get; set; }
    public string Mode        { get; set; } = string.Empty;
    public string File        { get; set; } = string.Empty;
    public long   StartSector { get; set; }
    public long   SectorCount { get; set; }
    public int    SectorSize  { get; set; } = 2352;
}

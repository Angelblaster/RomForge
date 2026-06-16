using PBP.Core.Models;

namespace PBP.Core.Readers;

public static class DiskReaderFactory
{
    public static IDiskReader Create(DiskSource source) => source.Type switch
    {
        DiskSourceType.Iso => new IsoReader(source.FilePath),
        DiskSourceType.Bin => new BinCueReader(source.CuePath
            ?? throw new ArgumentException("BIN+CUE는 CuePath 필수")),
        DiskSourceType.Chd => new ChdReader(source.FilePath),
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };
}

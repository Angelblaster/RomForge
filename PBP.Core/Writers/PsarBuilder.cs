using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using PBP.Core.Models;
using PBP.Core.Readers;
using System.Text;

namespace PBP.Core.Writers;

/// <summary>
/// DATA.PSAR 빌더
/// PSXPackager(popstation-md) 포팅 기반으로 정확한 PSAR 포맷 구현
/// </summary>
internal class PsarBuilder(PbpPackOptions options)
{
    // 16섹터 * 2352(RAW) = 0x9300
    private const int BlockSize = 0x9300;
    private const int BufferSize = 1048576; // 1MB

    /// <summary>
    /// 단일 디스크 PSAR 섹션 쓰기
    /// </summary>
    public void WriteSingleDisc(
        Stream output,
        uint psarOffset,
        DiskSource disk,
        string gameId,
        string gameTitle,
        byte[] tocData,
        Action<int>? progress = null,
        CancellationToken ct = default)
    {
        WriteDisc(output, psarOffset, disk, gameId, gameTitle, tocData, isMultiDisc: false, progress, ct);
    }

    /// <summary>
    /// 멀티디스크 PSAR 전체 쓰기 (PSTITLEIMG 헤더 포함)
    /// </summary>
    public void WriteMultiDisc(
        Stream output,
        uint psarOffset,
        List<(DiskSource disk, string gameId, string gameTitle, byte[] tocData)> discs,
        IProgress<(int disc, int percent)>? progress = null,
        CancellationToken ct = default)
    {
        uint p1Offset, p2Offset, mOffset, endOffset;
        uint[] isoPositions = new uint[5];

        // PSTITLEIMG 헤더
        WriteAscii(output, "PSTITLEIMG000000");

        p1Offset = (uint)output.Position;
        WriteUInt32(output, 0, 2);
        WriteUInt32(output, 0x2CC9C5BC);
        WriteUInt32(output, 0x33B5A90F);
        WriteUInt32(output, 0x06F6B4B3);
        WriteUInt32(output, 0xB25945BA);

        // psarOffset + 0x200 까지 패딩
        WriteZeros(output, 0x76 * 4);

        mOffset = (uint)output.Position;

        // 디스크 오프셋 예약 (5개 슬롯)
        for (int i = 0; i < 5; i++) WriteUInt32(output, 0);

        WriteRandom(output, 12);
        WriteZeros(output, 8 * 4);

        var (_, firstGameId, _, _) = discs[0];
        output.WriteByte((byte)'_');
        WriteAscii(output, firstGameId[..4]);
        output.WriteByte((byte)'_');
        WriteAscii(output, firstGameId[4..]);
        WriteZeros(output, 0x15);

        p2Offset = (uint)output.Position;
        WriteZeros(output, 2 * 4);

        output.Write(PsarData.data3, 0, PsarData.data3.Length);

        var titleBytes = Encoding.ASCII.GetBytes(discs[0].gameTitle);
        output.Write(titleBytes, 0, titleBytes.Length);
        WriteZeros(output, 0x80 - titleBytes.Length);

        WriteUInt32(output, 7);
        WriteZeros(output, 0x1C * 4);

        // 각 디스크 쓰기
        for (int d = 0; d < discs.Count; d++)
        {
            var (disk, gameId, gameTitle, tocData) = discs[d];

            // 0x8000 경계 정렬
            uint pos = (uint)output.Position;
            if (pos % 0x8000 > 0)
                WriteZeros(output, (int)(0x8000 - (pos % 0x8000)));

            isoPositions[d] = (uint)(output.Position - psarOffset);

            WriteDisc(output, psarOffset, disk, gameId, gameTitle, tocData,
                isMultiDisc: true,
                p => progress?.Report((d, p)), ct);

            if (ct.IsCancellationRequested) return;
        }

        // 0x10 정렬
        uint x = (uint)output.Position;
        if (x % 0x10 != 0)
        {
            endOffset = x + (0x10 - (x % 0x10));
            WriteZeros(output, (int)(endOffset - x));
        }
        else endOffset = x;

        endOffset -= psarOffset;
        uint finalPos = (uint)output.Position;

        // p1, p2, 디스크 오프셋 테이블 역으로 채우기
        output.Seek(p1Offset, SeekOrigin.Begin);
        WriteUInt32(output, endOffset);

        output.Seek(p2Offset, SeekOrigin.Begin);
        WriteUInt32(output, endOffset + 0x2d31);

        output.Seek(mOffset, SeekOrigin.Begin);
        for (int i = 0; i < 5; i++) WriteUInt32(output, isoPositions[i]);

        output.Seek(finalPos, SeekOrigin.Begin);
    }

    // ─────────────────────────────────────────────
    // 디스크 1개 쓰기 (single/multi 공통)
    // ─────────────────────────────────────────────
    private void WriteDisc(
        Stream output,
        uint psarOffset,
        DiskSource disk,
        string gameId,
        string gameTitle,
        byte[] tocData,
        bool isMultiDisc,
        Action<int>? progress,
        CancellationToken ct)
    {
        using var reader = DiskReaderFactory.Create(disk);

        var isoPath = disk.Type == DiskSourceType.Chd
            ? ExtractToTemp(reader, out _)
            : disk.FilePath;

        bool tempFile = disk.Type == DiskSourceType.Chd;
        try
        {
            WriteDiscFromIso(output, psarOffset, isoPath, gameId, gameTitle,
                tocData, isMultiDisc, progress, ct);
        }
        finally
        {
            if (tempFile && File.Exists(isoPath))
                File.Delete(isoPath);
        }
    }

    private void WriteDiscFromIso(
        Stream output,
        uint psarOffset,
        string isoPath,
        string gameId,
        string gameTitle,
        byte[] tocData,
        bool isMultiDisc,
        Action<int>? progress,
        CancellationToken ct)
    {
        var fileInfo = new FileInfo(isoPath);
        uint actualIsoSize = (uint)fileInfo.Length;
        uint isoSize = actualIsoSize;

        // BlockSize 경계 정렬
        if (isoSize % BlockSize != 0)
            isoSize += (uint)(BlockSize - (isoSize % BlockSize));

        uint p1Offset = 0, p2Offset = 0;

        // PSISOIMG 매직
        WriteAscii(output, "PSISOIMG0000");

        p1Offset = (uint)output.Position;
        WriteUInt32(output, isoSize + 0x100000);   // 패딩된 ISO 크기
        WriteZeros(output, 0xFC * 4);               // psarOffset + 0x400 까지 패딩

        // data1 템플릿 + GameID + TOC 오버레이
        var data1 = (byte[])PsarData.data1.Clone();
        var idBytes = Encoding.ASCII.GetBytes(gameId);
        Array.Copy(idBytes, 0, data1, 1, 4);
        Array.Copy(idBytes, 4, data1, 6, Math.Min(5, idBytes.Length - 4));
        Array.Copy(tocData, 0, data1, 1024, tocData.Length);
        output.Write(data1, 0, data1.Length);

        if (isMultiDisc)
        {
            WriteZeros(output, 4);
        }
        else
        {
            p2Offset = (uint)output.Position;
            WriteUInt32(output, isoSize + 0x100000 + 0x2d31);
        }

        // data2 템플릿 + GameTitle 오버레이
        var data2 = (byte[])PsarData.data2.Clone();
        var titleBytes = Encoding.ASCII.GetBytes(gameTitle);
        Array.Copy(titleBytes, 0, data2, 8, Math.Min(titleBytes.Length, data2.Length - 8));
        output.Write(data2, 0, data2.Length);

        // 인덱스 테이블 위치 기록
        uint indexOffset = (uint)output.Position;
        uint blockCount = isoSize / BlockSize;

        // 인덱스 테이블 예약 (블록당 32바이트: offset(4) + length(4) + dummy(6*4))
        WriteZeros(output, (int)(blockCount * 32));

        // 데이터 시작 오프셋까지 패딩
        uint padTo = (uint)(output.Position - psarOffset + psarOffset + 0x100000);
        uint curPos = (uint)output.Position;
        if (padTo > curPos)
            WriteZeros(output, (int)(padTo - curPos));

        // ISO 읽고 블록별 압축 쓰기
        var indexes = new (uint offset, uint length)[blockCount];
        uint writeOffset = 0;

        using var isoStream = new FileStream(isoPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] readBuf = new byte[BlockSize];
        byte[] compBuf = new byte[BufferSize];

        for (uint i = 0; i < blockCount; i++)
        {
            if (ct.IsCancellationRequested) return;

            int bytesRead = isoStream.Read(readBuf, 0, BlockSize);

            // 모자라면 0으로 채움
            if (bytesRead < BlockSize)
                Array.Clear(readBuf, bytesRead, BlockSize - bytesRead);

            if (options.CompressionLevel == 0)
            {
                // 무압축
                indexes[i] = (writeOffset, (uint)BlockSize);
                output.Write(readBuf, 0, BlockSize);
                writeOffset += BlockSize;
            }
            else
            {
                int compSize = Compress(readBuf, compBuf, options.CompressionLevel);

                if (compSize >= BlockSize)
                {
                    // 압축이 안 되면 원본 그대로
                    indexes[i] = (writeOffset, (uint)BlockSize);
                    output.Write(readBuf, 0, BlockSize);
                    writeOffset += BlockSize;
                }
                else
                {
                    indexes[i] = (writeOffset, (uint)compSize);
                    output.Write(compBuf, 0, compSize);
                    writeOffset += (uint)compSize;
                }
            }

            progress?.Invoke((int)(i * 100 / blockCount));
        }

        // 단일 디스크: 0x10 정렬 + p1/p2 오프셋 업데이트
        if (!isMultiDisc)
        {
            uint endPos = (uint)output.Position;
            uint endOffset;
            if (endPos % 0x10 != 0)
            {
                endOffset = endPos + (0x10 - (endPos % 0x10));
                WriteZeros(output, (int)(endOffset - endPos));
            }
            else endOffset = endPos;

            endOffset -= psarOffset;
            uint finalPos = (uint)output.Position;

            output.Seek(p1Offset, SeekOrigin.Begin);
            WriteUInt32(output, endOffset);

            output.Seek(p2Offset, SeekOrigin.Begin);
            WriteUInt32(output, endOffset + 0x2d31);

            output.Seek(finalPos, SeekOrigin.Begin);
        }

        // 인덱스 테이블 역으로 채우기
        uint afterData = (uint)output.Position;
        output.Seek(indexOffset, SeekOrigin.Begin);

        foreach (var (off, len) in indexes)
        {
            WriteUInt32(output, off);
            WriteUInt32(output, len);
            WriteZeros(output, 6 * 4); // dummy 6 uints
        }

        output.Seek(afterData, SeekOrigin.Begin);
        progress?.Invoke(100);
    }

    /// <summary>
    /// CHD → 임시 ISO 추출
    /// </summary>
    private static string ExtractToTemp(IDiskReader reader, out long sectorCount)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"pbp_tmp_{Guid.NewGuid():N}.iso");
        sectorCount = reader.TotalSectors;

        using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
        const int batchSize = 64;
        for (long s = 0; s < sectorCount; s += batchSize)
        {
            int count = (int)Math.Min(batchSize, sectorCount - s);
            var data = reader.ReadSectors(s, count);
            // RAW → ISO 2048 추출
            if (reader.SectorSize == 2352)
            {
                for (int i = 0; i < count; i++)
                {
                    fs.Write(data, i * 2352 + 0x10, 2048);
                }
            }
            else
            {
                fs.Write(data, 0, data.Length);
            }
        }

        return tempPath;
    }

    // ─── 압축 (raw deflate, ICSharpCode.SharpZipLib) ───
    private static int Compress(byte[] inBuf, byte[] outBuf, int level)
    {
        using var ms = new MemoryStream(outBuf);
        var deflater = new Deflater(level, true); // true = raw deflate (no zlib header)
        using var stream = new DeflaterOutputStream(ms, deflater);
        stream.Write(inBuf, 0, inBuf.Length);
        stream.Flush();
        stream.Finish();
        return (int)ms.Position;
    }

    // ─── 스트림 헬퍼 ───
    private static void WriteUInt32(Stream s, uint value, int count = 1)
    {
        var bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < count; i++) s.Write(bytes, 0, 4);
    }

    private static void WriteZeros(Stream s, int bytes)
    {
        var buf = new byte[Math.Min(bytes, 4096)];
        int remaining = bytes;
        while (remaining > 0)
        {
            int write = Math.Min(remaining, buf.Length);
            s.Write(buf, 0, write);
            remaining -= write;
        }
    }

    private static void WriteAscii(Stream s, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        s.Write(bytes, 0, bytes.Length);
    }

    private static readonly Random _rng = new();
    private static void WriteRandom(Stream s, int bytes)
    {
        var buf = new byte[bytes];
        _rng.NextBytes(buf);
        s.Write(buf, 0, buf.Length);
    }
}
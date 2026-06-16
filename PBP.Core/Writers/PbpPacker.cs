using PBP.Core.Models;
using PBP.Core.Properties;
using System.Text;

namespace PBP.Core.Writers;

/// <summary>
/// PBP 패커 메인
/// PSXPackager(PbpWriter) 포팅 기반
/// </summary>
public class PbpPacker(PbpPackOptions? options = null)
{
    private const uint PbpMagic = 0x50425000;   // \x00PBP
    private const uint PbpVersion = 0x00010000;
    private const uint PsarAlignment = 0x10000;

    private readonly PbpPackOptions _options = options ?? new PbpPackOptions();

    public void Pack(
        List<DiscInfo> discs,
        string outputPath,
        PbpAssets? assets = null,
        byte[]? paramSfo = null,
        IProgress<(int disc, int percent)>? progress = null,
        CancellationToken ct = default)
    {
        if (discs.Count == 0)
            throw new ArgumentException("디스크 소스가 없음");

        assets ??= new PbpAssets();

        // 리소스 fallback
        byte[] icon0 = assets.Icon0Png ?? Resources.ICON0;
        byte[] dataPsp = assets.DataPsp ?? Resources.DATA;
        byte[] icon1 = assets.Icon1Pmf ?? [];
        byte[] pic0 = assets.Pic0Png ?? [];
        byte[] pic1 = assets.Pic1Png ?? [];
        byte[] snd0 = assets.Snd0At3 ?? [];

        byte[] sfo = paramSfo ?? BuildDefaultSfo(discs[0]);

        // ─── PSAR 오프셋 계산 (0x10000 정렬) ───
        uint currentOffset = 0x28;
        uint[] offsets = new uint[10];

        offsets[0] = PbpMagic;
        offsets[1] = PbpVersion;

        offsets[2] = currentOffset; currentOffset += (uint)sfo.Length;
        offsets[3] = currentOffset; currentOffset += (uint)icon0.Length;
        offsets[4] = currentOffset; currentOffset += (uint)icon1.Length;
        offsets[5] = currentOffset; currentOffset += (uint)pic0.Length;
        offsets[6] = currentOffset; currentOffset += (uint)pic1.Length;
        offsets[7] = currentOffset; currentOffset += (uint)snd0.Length;
        offsets[8] = currentOffset; currentOffset += (uint)dataPsp.Length;

        uint psarOffset = currentOffset;
        if (psarOffset % PsarAlignment != 0)
            psarOffset += PsarAlignment - (psarOffset % PsarAlignment);
        offsets[9] = psarOffset;

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var w = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: true);

        // ─── PBP 헤더 (40바이트) ───
        foreach (var o in offsets) w.Write(o);

        // ─── 섹션들 ───
        w.Write(sfo);
        w.Write(icon0);
        w.Write(icon1);
        w.Write(pic0);
        w.Write(pic1);
        w.Write(snd0);
        w.Write(dataPsp);

        // PSAR까지 패딩
        long padNeeded = psarOffset - fs.Position;
        if (padNeeded > 0) w.Write(new byte[padNeeded]);

        // ─── PSAR 쓰기 ───
        var psarBuilder = new PsarBuilder(_options);

        if (discs.Count == 1)
        {
            var disc = discs[0];
            psarBuilder.WriteSingleDisc(
                fs, psarOffset,
                disc.Source, disc.GameId, disc.GameTitle, disc.TocData,
                p => progress?.Report((0, p)), ct);
        }
        else
        {
            var discTuples = discs
                .Select(d => (d.Source, d.GameId, d.GameTitle, d.TocData))
                .ToList();

            psarBuilder.WriteMultiDisc(fs, psarOffset, discTuples, progress, ct);
        }

        if (ct.IsCancellationRequested) return;

        // ─── STARTDAT 쓰기 ───
        WriteStartDat(fs, assets.BootPng);
    }

    /// <summary>
    /// BASE.PBP에서 STARTDAT 섹션 추출해서 출력 스트림에 복사
    /// BootPng가 있으면 교체, 없으면 BASE.PBP의 boot.png 그대로 복사
    /// </summary>
    private static void WriteStartDat(Stream output, byte[]? bootPng)
    {
        using var basePbp = new MemoryStream(Resources.BASE);

        uint[] baseHeader = new uint[10];
        var headerBytes = new byte[40];
        basePbp.Read(headerBytes, 0, 40);
        Buffer.BlockCopy(headerBytes, 0, baseHeader, 0, 40);

        if (baseHeader[0] != PbpMagic)
            throw new Exception("BASE 리소스가 유효한 PBP 파일이 아님");

        // PSAR 오프셋 + 12 위치에서 STARTDAT 위치 계산
        basePbp.Seek(baseHeader[9] + 12, SeekOrigin.Begin);
        var temp = new byte[4];
        basePbp.Read(temp, 0, 4);
        uint startDatOffset = BitConverter.ToUInt32(temp, 0) + 0x50000;

        // STARTDAT 매직 확인
        basePbp.Seek(startDatOffset, SeekOrigin.Begin);
        var magicBuf = new byte[8];
        basePbp.Read(magicBuf, 0, 8);
        if (Encoding.ASCII.GetString(magicBuf, 0, 8) != "STARTDAT")
            throw new Exception("BASE 리소스에서 STARTDAT를 찾을 수 없음");

        // 헤더 크기(header[0])와 boot.png 크기(header[1]) 읽기
        basePbp.Seek(startDatOffset + 16, SeekOrigin.Begin);
        var sizeBytes = new byte[8];
        basePbp.Read(sizeBytes, 0, 8);
        uint headerSize = BitConverter.ToUInt32(sizeBytes, 0);   // 항상 0x50
        uint bootPngSize = BitConverter.ToUInt32(sizeBytes, 4);

        // STARTDAT 헤더 복사
        basePbp.Seek(startDatOffset, SeekOrigin.Begin);
        var startDatHeader = new byte[headerSize];
        basePbp.Read(startDatHeader, 0, (int)headerSize);

        // BootPng 교체 시 헤더 안의 크기 필드 업데이트
        if (bootPng != null)
        {
            var bootSizeBytes = BitConverter.GetBytes((uint)bootPng.Length);
            Buffer.BlockCopy(bootSizeBytes, 0, startDatHeader, 16 + 4, 4);
        }

        output.Write(startDatHeader, 0, (int)headerSize);

        // boot.png: 커스텀 or BASE에서 복사
        if (bootPng != null)
        {
            output.Write(bootPng, 0, bootPng.Length);
            // BASE의 boot.png는 건너뜀
            basePbp.Seek(bootPngSize, SeekOrigin.Current);
        }
        else
        {
            var bootBuf = new byte[bootPngSize];
            basePbp.Read(bootBuf, 0, (int)bootPngSize);
            output.Write(bootBuf, 0, (int)bootPngSize);
        }

        // 나머지 (encrypted PGD 등) 그대로 복사
        var copyBuf = new byte[1048576];
        int bytesRead;
        while ((bytesRead = basePbp.Read(copyBuf, 0, copyBuf.Length)) > 0)
            output.Write(copyBuf, 0, bytesRead);
    }

    private static byte[] BuildDefaultSfo(DiscInfo disc)
    {
        return new ParamSfoBuilder()
            .WithTitle(disc.GameTitle)
            .WithDiscId(disc.GameId)
            .Build();
    }
}
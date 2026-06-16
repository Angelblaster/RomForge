using PBP.Core.Models;
using System.Text;

namespace PBP.Core.Writers;

/// <summary>
/// PBP 패커 메인
/// 헤더(8개 오프셋) + 에셋들 + DATA.PSAR 조립
/// </summary>
public class PbpPacker(PbpPackOptions? options = null)
{
    private const uint PbpMagic = 0x50425000; // \x00PBP
    private const uint PbpVersion = 0x00010000;

    // PSAR 오프셋은 0x10000 경계 정렬
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

        // 섹션 바이트 배열 준비
        // [0] PARAM.SFO  [1] ICON0.PNG  [2] ICON1.PMF
        // [3] PIC0.PNG   [4] PIC1.PNG   [5] SND0.AT3
        // [6] DATA.PSP   [7] DATA.PSAR  (PSAR는 나중에 스트림으로)
        byte[][] sections =
        [
            paramSfo         ?? [],
            assets.Icon0Png  ?? [],
            assets.Icon1Pmf  ?? [],
            assets.Pic0Png   ?? [],
            assets.Pic1Png   ?? [],
            assets.Snd0At3   ?? [],
            assets.DataPsp   ?? [],
        ];

        // PSAR 오프셋 계산 (0x10000 정렬)
        uint currentOffset = 0x28; // 헤더 크기
        uint[] offsets = new uint[8];
        for (int i = 0; i < 7; i++)
        {
            offsets[i] = currentOffset;
            currentOffset += (uint)sections[i].Length;
        }

        // PSAR는 0x10000 경계로 올림
        uint psarOffset = currentOffset;
        if (psarOffset % PsarAlignment != 0)
            psarOffset += PsarAlignment - (psarOffset % PsarAlignment);
        offsets[7] = psarOffset;

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var w = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: true);

        // PBP 헤더
        w.Write(PbpMagic);
        w.Write(PbpVersion);
        foreach (var o in offsets) w.Write(o);

        // 섹션 데이터 쓰기
        foreach (var section in sections) w.Write(section);

        // PSAR까지 패딩
        long padNeeded = psarOffset - fs.Position;
        if (padNeeded > 0) w.Write(new byte[padNeeded]);

        // PSAR 쓰기
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
    }
}
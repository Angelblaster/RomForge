using PBP.Core.Cue;
using PBP.Core.Models;
using PBP.Core.Readers;
using PBP.Core.Writers;

namespace PBP.Core;

/// <summary>
/// PBP.Core 퍼블릭 진입점
/// ISO / BIN+CUE / CHD → PBP 변환
/// GameId, TOC 전부 자동 추출
/// </summary>
public class PbpConverter(PbpPackOptions? options = null)
{
    private readonly PbpPackOptions _options = options ?? new PbpPackOptions();

    /// <summary>
    /// 멀티디스크 PBP 변환 (DiscInfo 직접 지정)
    /// </summary>
    public void Convert(
        List<DiscInfo> discs,
        string outputPath,
        PbpAssets? assets = null,
        byte[]? paramSfo = null,
        IProgress<(int disc, int percent)>? progress = null,
        CancellationToken ct = default)
    {
        new PbpPacker(_options).Pack(discs, outputPath, assets, paramSfo, progress, ct);
    }

    /// <summary>
    /// 단일 디스크 (DiscInfo 직접 지정)
    /// </summary>
    public void Convert(
        DiscInfo disc,
        string outputPath,
        PbpAssets? assets = null,
        byte[]? paramSfo = null,
        IProgress<(int disc, int percent)>? progress = null,
        CancellationToken ct = default)
        => Convert([disc], outputPath, assets, paramSfo, progress, ct);

    /// <summary>
    /// 단일 디스크 - GameId 자동 추출 버전!!
    /// gameTitle만 넣으면 됨
    /// </summary>
    public void Convert(
        DiskSource source,
        string gameTitle,
        string outputPath,
        PbpAssets? assets = null,
        IProgress<(int disc, int percent)>? progress = null,
        CancellationToken ct = default)
    {
        var disc = DiscInfoBuilder.Build(source, gameTitle);
        var sfo = CreateSfo(gameTitle, disc.GameId).Build();
        Convert([disc], outputPath, assets, sfo, progress, ct);
    }

    /// <summary>
    /// 멀티디스크 - GameId 자동 추출 버전!!
    /// (source, gameTitle) 리스트만 넘기면 됨
    /// </summary>
    public void Convert(
        List<(DiskSource source, string gameTitle)> discs,
        string outputPath,
        PbpAssets? assets = null,
        IProgress<(int disc, int percent)>? progress = null,
        CancellationToken ct = default)
    {
        var discInfos = discs
            .Select(d => DiscInfoBuilder.Build(d.source, d.gameTitle))
            .ToList();

        var firstDisc = discInfos[0];
        var sfo = CreateSfo(firstDisc.GameTitle, firstDisc.GameId).Build();
        Convert(discInfos, outputPath, assets, sfo, progress, ct);
    }

    /// <summary>
    /// M3U → PBP - GameId 자동 추출 버전!!
    /// gameTitle만 넣으면 됨
    /// </summary>
    public void ConvertFromM3u(
        string m3uPath,
        string gameTitle,
        string outputPath,
        PbpAssets? assets = null,
        IProgress<(int disc, int percent)>? progress = null,
        CancellationToken ct = default)
    {
        var sources = M3uParser.Parse(m3uPath);
        var discInfos = sources
            .Select(src => DiscInfoBuilder.Build(src, gameTitle))
            .ToList();

        var firstDisc = discInfos[0];
        var sfo = CreateSfo(firstDisc.GameTitle, firstDisc.GameId).Build();
        Convert(discInfos, outputPath, assets, sfo, progress, ct);
    }

    /// <summary>
    /// PARAM.SFO 빌더 편의 팩토리
    /// </summary>
    public static ParamSfoBuilder CreateSfo(string title, string discId = "SLUS00000")
        => new ParamSfoBuilder()
            .WithTitle(title)
            .WithDiscId(discId);
}
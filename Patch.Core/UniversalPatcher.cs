using Common;
using Patch.Core.Enums;
using Patch.Core.Formats;

namespace Patch.Core;

public static class UniversalPatcher
{
    public const long MemoryThreshold = 2L * 1024 * 1024 * 1024;
    private const int IoChunkSize = 8 * 1024 * 1024;

    public static async Task ApplyPatchAsync(string sourcePath, string patchPath, string outputPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"원본 파일을 찾을 수 없습니다: {sourcePath}");

        if (!File.Exists(patchPath))
            throw new FileNotFoundException($"패치 파일을 찾을 수 없습니다: {patchPath}");

        ct.ThrowIfCancellationRequested();

        PatchFormat format = await DetectFormatAsync(patchPath, ct);

        if (format == PatchFormat.Xdelta)
        {
            await Task.Run(() => Xdelta3.ApplyPatch(sourcePath, patchPath, outputPath, progress, ct), ct);

            return;
        }

        var sourceLength = new FileInfo(sourcePath).Length;
        var patchLength = new FileInfo(patchPath).Length;

        if (sourceLength < MemoryThreshold && patchLength < MemoryThreshold)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var readSourceProgress = new ScopedProgress(progress, 0, 35);
            var readPatchProgress = new ScopedProgress(progress, 35, 40);
            var decodeProgress = new ScopedProgress(progress, 40, 90);
            var writeProgress = new ScopedProgress(progress, 90, 100);

            byte[] sourceData = await ReadAllBytesWithProgressAsync(sourcePath, "원본 읽는 중...", readSourceProgress, ct);
            System.Diagnostics.Debug.WriteLine($"[TIMING] 원본 읽기: {sw.ElapsedMilliseconds}ms"); sw.Restart();

            byte[] patchData = await ReadAllBytesWithProgressAsync(patchPath, "패치 읽는 중...", readPatchProgress, ct);
            System.Diagnostics.Debug.WriteLine($"[TIMING] 패치 읽기: {sw.ElapsedMilliseconds}ms"); sw.Restart();

            byte[] resultData = await ApplyPatchAsync(sourceData, patchData, decodeProgress, ct);
            System.Diagnostics.Debug.WriteLine($"[TIMING] 디코드: {sw.ElapsedMilliseconds}ms"); sw.Restart();

            await WriteAllBytesWithProgressAsync(outputPath, resultData, "저장 중...", writeProgress, ct);
            System.Diagnostics.Debug.WriteLine($"[TIMING] 쓰기: {sw.ElapsedMilliseconds}ms");
        }
        else
        {
            switch (format)
            {
                case PatchFormat.Ips: await Ips.ApplyPatchAsync(sourcePath, patchPath, outputPath, progress, ct); break;
                case PatchFormat.Bps: await Bps.ApplyPatchAsync(sourcePath, patchPath, outputPath, progress, ct); break;
                case PatchFormat.Ups: await Ups.ApplyPatchAsync(sourcePath, patchPath, outputPath, progress, ct); break;
                case PatchFormat.Ppf: await Ppf.ApplyPatchAsync(sourcePath, patchPath, outputPath, progress, ct); break;
                case PatchFormat.Aps: await Aps.ApplyPatchAsync(sourcePath, patchPath, outputPath, progress, ct); break;
                default: throw new NotSupportedException("지원되지 않거나 유효하지 않은 패치 포맷입니다.");
            }
        }
    }

    public static async Task<byte[]> ApplyPatchAsync(string sourcePath, string patchPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"원본 파일을 찾을 수 없습니다: {sourcePath}");

        if (!File.Exists(patchPath))
            throw new FileNotFoundException($"패치 파일을 찾을 수 없습니다: {patchPath}");

        var sourceLength = new FileInfo(sourcePath).Length;
        var patchLength = new FileInfo(patchPath).Length;

        if (sourceLength >= MemoryThreshold || patchLength >= MemoryThreshold)
            throw new InvalidOperationException("2GB 이상 파일은 ApplyPatch(string, string, string)을 사용하세요.");

        ct.ThrowIfCancellationRequested();

        var readSourceProgress = new ScopedProgress(progress, 0, 45);
        var readPatchProgress = new ScopedProgress(progress, 45, 50);
        var decodeProgress = new ScopedProgress(progress, 50, 100);

        byte[] sourceData = await ReadAllBytesWithProgressAsync(sourcePath, "원본 읽는 중...", readSourceProgress, ct);
        byte[] patchData = await ReadAllBytesWithProgressAsync(patchPath, "패치 읽는 중...", readPatchProgress, ct);

        return await ApplyPatchAsync(sourceData, patchData, decodeProgress, ct);
    }

    public static async Task<byte[]> ApplyPatchAsync(byte[] sourceData, byte[] patchData, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        PatchFormat format = DetectFormat(patchData);

        return format switch
        {
            PatchFormat.Xdelta => await Task.Run(() => Xdelta3.ApplyPatch(sourceData, patchData, progress, ct), ct),
            PatchFormat.Ips => await Ips.ApplyPatchAsync(sourceData, patchData, progress, ct),
            PatchFormat.Bps => await Bps.ApplyPatchAsync(sourceData, patchData, progress, ct),
            PatchFormat.Ups => await Ups.ApplyPatchAsync(sourceData, patchData, progress, ct),
            PatchFormat.Ppf => await Ppf.ApplyPatchAsync(sourceData, patchData, progress, ct),
            PatchFormat.Aps => await Aps.ApplyPatchAsync(sourceData, patchData, progress, ct),
            _ => throw new NotSupportedException("지원되지 않거나 유효하지 않은 패치 포맷입니다.")
        };
    }

    public static async Task<PatchFormat> DetectFormatAsync(string patchPath, CancellationToken ct = default)
    {
        if (!File.Exists(patchPath))
            throw new FileNotFoundException($"파일을 찾을 수 없습니다: {patchPath}");

        byte[] header = new byte[8];

        using var fs = new FileStream(patchPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);

        int read = await fs.ReadAsync(header, ct);

        return DetectFormat(header.AsSpan(0, read));
    }

    public static PatchFormat DetectFormat(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
            return PatchFormat.Unknown;

        if (data.Length >= 3 && data[0] == 0xD6 && data[1] == 0xC3 && data[2] == 0xC4)
            return PatchFormat.Xdelta;

        if (data.Length >= 5 && data[0] == 'P' && data[1] == 'A' && data[2] == 'T' && data[3] == 'C' && data[4] == 'H')
            return PatchFormat.Ips;

        if (data[0] == 'B' && data[1] == 'P' && data[2] == 'S' && data[3] == '1')
            return PatchFormat.Bps;

        if (data[0] == 'U' && data[1] == 'P' && data[2] == 'S' && data[3] == '1')
            return PatchFormat.Ups;

        if (data.Length >= 3 && data[0] == 'P' && data[1] == 'P' && data[2] == 'F')
            return PatchFormat.Ppf;

        if (data[0] == 'A' && data[1] == 'P' && data[2] == 'S' && data[3] == '1')
            return PatchFormat.Aps;

        return PatchFormat.Unknown;
    }

    private static async Task<byte[]> ReadAllBytesWithProgressAsync(string path, string label, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

        long length = fs.Length;
        byte[] buffer = new byte[length];

        if (length == 0)
            return buffer;

        Action<long, long>? report = null;

        if (progress is not null)
        {
            var reporter = new ProgressReporter(label, string.Empty, length, progress);
            report = reporter.CreateAction();
        }

        int totalRead = 0;

        while (totalRead < length)
        {
            ct.ThrowIfCancellationRequested();

            int toRead = (int)Math.Min(IoChunkSize, length - totalRead);
            int read = await fs.ReadAsync(buffer.AsMemory(totalRead, toRead), ct);

            if (read == 0)
                break;

            totalRead += read;

            report?.Invoke(totalRead, length);
        }

        report?.Invoke(length, length);

        return buffer;
    }

    private static async Task WriteAllBytesWithProgressAsync(string path, byte[] data, string label, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        long length = data.Length;

        Action<long, long>? report = null;

        if (progress is not null)
        {
            var reporter = new ProgressReporter(label, string.Empty, Math.Max(1, length), progress);
            report = reporter.CreateAction();
        }

        int written = 0;

        while (written < data.Length)
        {
            ct.ThrowIfCancellationRequested();

            int toWrite = (int)Math.Min(IoChunkSize, data.Length - written);

            await fs.WriteAsync(data.AsMemory(written, toWrite), ct);

            written += toWrite;

            report?.Invoke(written, length);
        }

        report?.Invoke(length, length);
    }

    private sealed class ScopedProgress(IProgress<ProgressInfo>? inner, int startPct, int endPct) : IProgress<ProgressInfo>
    {
        public void Report(ProgressInfo value)
        {
            int span = endPct - startPct;
            int mapped = startPct + (int)(span * (value.Percent / 100.0));

            inner?.Report(value with { Percent = Math.Clamp(mapped, 0, 100) });
        }
    }
}
using Common;
using Patch.Core;
using RomForge.Core.Models.Patch;
using System.IO;
using System.IO.Compression;
using System.Collections.Concurrent;

namespace RomForge.Core.Services;

public static class PatchService
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte[]>> _memoryCache = new();

    public static async Task ApplyAsync(SourceEntry source, PatchEntry patch, string outputDir, IProgress<ProgressInfo>? progress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        var sourceBytes = source.IsZipEntry
            ? await ReadZipEntryAsync(source.ZipPath!, source.EntryPath, ct)
            : await File.ReadAllBytesAsync(source.EntryPath, ct);

        var patchBytes = patch.IsZipEntry
            ? await ReadZipEntryAsync(patch.ZipPath!, patch.EntryPath, ct)
            : await File.ReadAllBytesAsync(patch.EntryPath, ct);

        ct.ThrowIfCancellationRequested();

        var result = await Task.Run(() =>
            UniversalPatcher.ApplyPatch(sourceBytes, patchBytes,
                p => progress?.Report(new ProgressInfo { Percent = (int)(p * 100) })), ct);

        if (source.IsZipEntry)
        {
            var zipCache = _memoryCache.GetOrAdd(source.ZipPath!, _ => new ConcurrentDictionary<string, byte[]>());
            zipCache[source.EntryPath] = result;
        }
        else
        {
            Directory.CreateDirectory(outputDir);
            await File.WriteAllBytesAsync(Path.Combine(outputDir, source.DisplayName), result, ct);
        }

        log?.Invoke($"[{source.DisplayName}] 메모리 캐싱 완료", LogLevel.Ok);
    }

    public static async Task FlushToDiskAsync(string outputDir, CancellationToken ct)
    {
        foreach (var zipGroup in _memoryCache)
        {
            string sourceZipPath = zipGroup.Key;
            string outputZipPath = Path.Combine(outputDir, Path.GetFileName(sourceZipPath));

            Directory.CreateDirectory(outputDir);

            if (!File.Exists(outputZipPath)) 
                File.Copy(sourceZipPath, outputZipPath);

            using var zip = ZipFile.Open(outputZipPath, ZipArchiveMode.Update);

            foreach (var entryData in zipGroup.Value)
            {
                var existing = zip.GetEntry(entryData.Key);

                existing?.Delete();

                var newEntry = zip.CreateEntry(entryData.Key, CompressionLevel.Optimal);
                using var stream = newEntry.Open();

                await stream.WriteAsync(entryData.Value, ct);
            }
        }

        _memoryCache.Clear();
    }

    private static async Task<byte[]> ReadZipEntryAsync(string zipPath, string entryPath, CancellationToken ct)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry(entryPath) ?? throw new FileNotFoundException($"ZIP 없음: {entryPath}");
        using var stream = entry.Open();
        using var ms = new MemoryStream();

        await stream.CopyToAsync(ms, ct);

        return ms.ToArray();
    }
}
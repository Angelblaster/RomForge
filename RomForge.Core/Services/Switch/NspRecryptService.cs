using Common;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.Utils;
using System.IO;

using Path = System.IO.Path;
using Res = NSW.Core.Properties.Resources;

namespace RomForge.Core.Services.Switch;

public static class NspRecryptService
{
    public static async Task<List<string>> Recrypt(IReadOnlyList<string> inputPaths, bool forceKeyGen0, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
        => await ExecuteProcess(inputPaths, forceKeyGen0, progress, log, ct);

    private static async Task<List<string>> ExecuteProcess(IReadOnlyList<string> inputPaths, bool forceKeyGen0, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        var ks = KeySetProvider.Instance.KeySet.Clone();
        var processedFiles = new List<string>();

        for (int i = 0; i < inputPaths.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            string path = inputPaths[i];

            try
            {
                log?.Invoke($"[{i + 1}/{inputPaths.Count}] Recrypt {Res.Log_ProcessStart}: {Path.GetFileName(path)}", LogLevel.Info, inputPaths[i]);

                string resultPath = await RunCoreAsync(path, forceKeyGen0, ks, progress, log, ct);

                if (!string.IsNullOrEmpty(resultPath))
                    processedFiles.Add(resultPath);
            }
            catch (OperationCanceledException) 
            {
                throw;
            }
            catch (Exception ex)
            {
                log?.Invoke($"{Path.GetFileName(path)} {Res.Log_ProcessError}: {ex.Message}", LogLevel.Error, string.Empty);
            }
        }

        return processedFiles;
    }

    private static async Task<string> RunCoreAsync(string inputPath, bool forceKeyGen0, KeySet keySet, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        var disposables = new List<IDisposable>();
        string? finalPath = null;
        bool isCompleted = false;

        try
        {
            var metas = MetadataReader.GetMetadataFromContainer(keySet, inputPath);

            if (metas.Count == 0)
                throw new InvalidOperationException(Res.Error_NoMetadata);

            var meta = metas.First();
            var sourceStorage = new LocalStorage(inputPath, FileAccess.Read);
            disposables.Add(sourceStorage);
            IFileSystem sourceFs = sourceStorage.OpenFileSystem(keySet, inputPath);
            disposables.Add(sourceFs);
            keySet.RegisterTickets(sourceFs);

            string inputExt = Path.GetExtension(inputPath).ToLowerInvariant();
            string outputExt = inputExt is ".nsz" or ".xcz" ? ".nsz" : ".nsp";
            finalPath = Utils.GetUniqueFilePath(Path.ChangeExtension(inputPath, outputExt));

            var fileEntries = new List<(string Name, Func<Stream, Action<long>, Task> Writer, long EstimatedSize, string Label)>();

            foreach (var entry in sourceFs.EnumerateEntries("/", "*"))
            {
                string entryName = entry.Name.ToString();
                string entryExt = Path.GetExtension(entryName).ToLowerInvariant();
                var fileRef = new UniqueRef<IFile>();

                if (!sourceFs.OpenFile(ref fileRef.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).IsSuccess())
                    continue;

                fileRef.Get.GetSize(out long size).ThrowIfFailure();
                IFile rawFile = fileRef.Release();
                disposables.Add(rawFile);
                IStorage currentStorage = new FileStorage(rawFile);
                disposables.Add(currentStorage);

                if (entryExt is ".tik" or ".cert")
                {
                    if (!forceKeyGen0)
                    {
                        var capturedStorage = currentStorage;
                        fileEntries.Add((entryName, async (s, onRead) => await Common.Utils.CopyStreamAsync(capturedStorage.AsStream(), s, onRead, ct), size, entryName));
                    }
                    continue;
                }

                if (entryExt is not ".nca" and not ".ncz")
                {
                    var capturedStorage = currentStorage;
                    fileEntries.Add((entryName, async (s, onRead) => await Common.Utils.CopyStreamAsync(capturedStorage.AsStream(), s, onRead, ct), size, entryName));
                    continue;
                }

                IStorage ncaStorage = currentStorage;
                string ncaName = entryName;
                long ncaSize = size;

                var nca = new Nca(keySet, ncaStorage);
                int keyGeneration = (int)nca.Header.KeyGeneration;
                string label = $"{meta.KrTitle ?? meta.EnTitle} [{nca.Header.ContentType}]";
                var capturedNcaStorage = ncaStorage;

                fileEntries.Add((ncaName, async (s, onRead) =>
                {
                    await NcaRecryptService.RecryptAsync(capturedNcaStorage.AsStream(), s, forceKeyGen0 ? 0 : keyGeneration, keySet, onRead, ct);
                }, ncaSize, $"{label} [Recrypting]"));
            }

            string displayName = $"Recrypt {NspNameBuilder.CompressDisplayNameBuild(meta.KrTitle, meta.TitleId, meta.DisplayVersion)}";
            var fout = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite);
            disposables.Add(fout);

            await Pfs0Builder.WriteAsync(displayName, Path.GetFileNameWithoutExtension(finalPath), fileEntries, fout, 0x20, progress, ct);

            isCompleted = true;

            log?.Invoke($"{Path.GetFileName(finalPath)} Recrypt {Res.Log_StatusDone}", LogLevel.Ok, meta.TitleId);

            return finalPath;
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--) disposables[i]?.Dispose();

            if (!isCompleted && !string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
                try { File.Delete(finalPath); } catch { }
        }
    }
}
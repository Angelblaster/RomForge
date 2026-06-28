using Common;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.NSZ;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.Core.Models;
using RomForge.Core.Models.Switch;
using System.IO;
using Path = System.IO.Path;
using Res = NSW.Core.Properties.Resources;

namespace RomForge.Core.Services.Switch;

public abstract class BaseSwitchService
{
    protected static (string FinalName, Func<Stream, Action<long>, Task> Writer, long Size, string Label)? 
        BuildFileEntry(string entryName, string originalExt, IStorage currentStorage, long size, Nca nca, string label, bool useCompression, bool useBlockMode, int compressionLevel, bool forceKeyGen0, KeySet keySet, Dictionary<string, NcaToNczConverter> converters, CancellationToken ct = default)
    {
        int keyGen = forceKeyGen0 ? 0 : (int)nca.Header.KeyGeneration;

        if (originalExt == ".ncz")
        {
            var ncz = new Ncz(keySet, currentStorage, NczReadMode.Original);
            var decStorage = ncz.BaseStorage;

            decStorage.GetSize(out long decSize).ThrowIfFailure();

            if (useCompression && nca.Header.ContentType is NcaContentType.Program or NcaContentType.PublicData)
            {
                string finalName = nca.HasSparseLayer() ? entryName : Path.ChangeExtension(entryName, ".ncz");
                var converter = new NcaToNczConverter(keySet);

                converters[Path.ChangeExtension(entryName, ".nca")] = converter;

                var cap = decStorage;

                return (finalName, async (s, onRead) =>
                {
                    var header = await NcaRecryptService.GetRecryptedHeaderAsync(cap, keyGen, keySet, ct);
                    using var hs = new MemoryStream(header);

                    await converter.ConvertAsync(hs, cap, s, useBlockMode, compressionLevel, onRead, ct);
                }, decSize, label);
            }
            else
            {
                string finalName = Path.ChangeExtension(entryName, ".nca");
                var cap = decStorage;

                return (finalName, async (s, onRead) =>
                {
                    await NcaRecryptService.RecryptAsync(cap.AsStream(), s, keyGen, keySet, onRead, ct);
                }, decSize, label);
            }
        }

        if (useCompression && nca.Header.ContentType is NcaContentType.Program or NcaContentType.PublicData)
        {
            string finalName = nca.HasSparseLayer() ? entryName : Path.ChangeExtension(entryName, ".ncz");
            var converter = new NcaToNczConverter(keySet);

            converters[Path.ChangeExtension(entryName, ".nca")] = converter;

            var cap = currentStorage;

            return (finalName, async (s, onRead) =>
            {
                var header = await NcaRecryptService.GetRecryptedHeaderAsync(cap, keyGen, keySet, ct);
                using var hs = new MemoryStream(header);

                await converter.ConvertAsync(hs, cap, s, useBlockMode, compressionLevel, onRead, ct);
            }, size, label);
        }
        else
        {
            var cap = currentStorage;

            return (entryName, async (s, onRead) =>
            {
                await NcaRecryptService.RecryptAsync(cap.AsStream(), s, keyGen, keySet, onRead, ct);
            }, size, label);
        }
    }

    protected static async Task RunValidation(Stream fout, Dictionary<string, NcaToNczConverter> converters, long totalValidationSize, string titleId, string displayLabel, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        fout.Position = 0;

        var validationFs = new PartitionFileSystem();

        validationFs.Initialize(fout.AsStorage()).ThrowIfFailure();

        var nczEntries = validationFs.EnumerateEntries("/", "*.ncz")
            .Where(e => converters.ContainsKey(Path.ChangeExtension(e.Name, ".nca")))
            .ToList();

        foreach (var entry in nczEntries)
        {
            ct.ThrowIfCancellationRequested();

            string origName = Path.ChangeExtension(entry.Name, ".nca");

            if (!converters.TryGetValue(origName, out var converter))
                continue;

            using var nczFile = new UniqueRef<IFile>();

            validationFs.OpenFile(ref nczFile.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

            log?.Invoke($"- {displayLabel} {Res.ToolTip_ValidateCompress}", LogLevel.Info, titleId);

            await converter.ValidateAsync(nczFile.Get.AsStream(), titleId, totalValidationSize, displayLabel, progress, ct);

            log?.Invoke($"- {displayLabel} OK", LogLevel.Ok, titleId);
        }
    }

    protected static Dictionary<string, TitleGroup> BuildTitleGroups(List<MetadataResult> allMeta)
    {
        var groups = new Dictionary<string, TitleGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var meta in allMeta)
        {
            string baseTid = LibHacHelper.GetBaseTitleId(meta.TitleId);

            if (string.IsNullOrEmpty(baseTid))
                continue;

            if (!groups.TryGetValue(baseTid, out var group))
            {
                group = new TitleGroup(baseTid, meta.KrTitle);
                groups[baseTid] = group;
            }

            switch (meta.Type)
            {
                case ContentMetaType.Application: group.BaseMetas.Add(meta); break;
                case ContentMetaType.Patch: group.PatchMetas.Add(meta); break;
                case ContentMetaType.AddOnContent: group.DlcMetas.Add(meta); break;
            }
        }

        return groups;
    }

    protected static HashSet<string>? BuildAllowedNcaIds(TitleGroup group, MetadataResult? latestPatch)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var meta in group.BaseMetas.GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            AddNcaIds(allowed, meta);

        if (latestPatch != null)
            AddNcaIds(allowed, latestPatch);

        foreach (var meta in group.DlcMetas.GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            AddNcaIds(allowed, meta);

        return allowed.Count > 0 ? allowed : null;
    }

    private static void AddNcaIds(HashSet<string> set, MetadataResult meta)
    {
        if (meta.ContentNcaIds == null) 
            return;

        foreach (var id in meta.ContentNcaIds) set.Add(id);
    }

    protected static List<string> GetAllPaths(BuildRequest req)
    {
        if (req.AllSourcePaths is { Count: > 0 })
            return [.. req.AllSourcePaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))];

        var list = new List<string>();

        if (!string.IsNullOrEmpty(req.UpdateFilePath) && File.Exists(req.UpdateFilePath))
            list.Add(req.UpdateFilePath);

        foreach (var p in req.DlcFilePaths)
            if (!string.IsNullOrEmpty(p) && File.Exists(p) && !list.Contains(p))
                list.Add(p);

        if (!string.IsNullOrEmpty(req.BaseFilePath) && !list.Contains(req.BaseFilePath))
            list.Add(req.BaseFilePath);

        return list;
    }

    protected static MetadataResult ExtractFinalMetadata(KeySet ks, List<string> paths, string? targetBaseTitleId = null)
    {
        var allMetas = paths
            .SelectMany(p => MetadataReader.GetMetadataFromContainer(ks, p))
            .GroupBy(m => new { m.TitleId, m.TitleVersion, m.Type })
            .Select(g => g.First())
            .ToList();

        if (!string.IsNullOrEmpty(targetBaseTitleId))
        {
            allMetas = [.. allMetas.Where(m =>
        {
            string baseTid = LibHacHelper.GetBaseTitleId(m.TitleId);

            if (string.IsNullOrEmpty(baseTid)) 
                return false;

            return baseTid.Equals(targetBaseTitleId, StringComparison.OrdinalIgnoreCase);
        })];
        }

        if (allMetas.Count == 0)
            return new MetadataResult(string.Empty, 0, "1.0.0", string.Empty, string.Empty, 0, ContentMetaType.Application);

        int dlcCount = allMetas.Count(m => m.Type == ContentMetaType.AddOnContent);
        var latestPatch = allMetas.Where(m => m.Type == ContentMetaType.Patch).OrderByDescending(m => m.TitleVersion).FirstOrDefault();
        var baseGame = allMetas.FirstOrDefault(m => m.Type == ContentMetaType.Application) ?? allMetas.First();
        var versionSource = latestPatch ?? baseGame;

        return new MetadataResult(baseGame.TitleId, versionSource.TitleVersion, versionSource.DisplayVersion, baseGame.KrTitle, baseGame.EnTitle, dlcCount, ContentMetaType.Application);
    }

    protected static void CleanupOnFailure(string? finalPath, Action<string, LogLevel, string> log, string titleId)
    {
        if (!string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
            try { File.Delete(finalPath); log?.Invoke(Res.Log_DeleteIncompleteFile, LogLevel.Info, titleId); }
            catch { }
    }
}
using Common;
using NSW.Core;
using Path = System.IO.Path;

namespace RomForge.Core.Services.Switch;

public class SwitchMergeService
{
    public static async Task<List<string>> Merge(IReadOnlyList<string> inputPaths, string outputDir, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, bool outputAsXci, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet.Clone();
        var results = new List<string>();
        bool useCompression = compressionLevel > 0;

        foreach (var path in inputPaths)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                List<string> output = outputAsXci
                    ? await XciMergeService.RunMergeAll([path], outputDir, useCompression, compressionLevel, useBlockMode, isValidationEnabled, forceKeyGen0, keySet.Clone(), progress, log, ct)
                    : await NspMergeService.RunMergeAll([path], outputDir, useCompression, compressionLevel, useBlockMode, isValidationEnabled, forceKeyGen0, keySet.Clone(), progress, log, ct);

                results.AddRange(output);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                log?.Invoke($"{Path.GetFileName(path)} {ex.Message}", LogLevel.Error, path);
            }
        }

        return results;
    }
}
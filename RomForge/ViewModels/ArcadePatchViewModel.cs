using Common.WPF.ViewModels;
using RomForge.Core.Models.Patch;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace RomForge.ViewModels;

public class ArcadePatchViewModel : ToolTabViewModel
{
    public ObservableCollection<ArcadeMatchItem> MatchItems { get; } = [];

    public ObservableCollection<PatchEntry> AllPatchEntries { get; } = [];

    public ObservableCollection<PatchEntry> UnmatchedPatches { get; } = [];

    public ObservableCollection<PatchPackage> AvailablePatchPackages { get; } = [];

    public bool HasPatchPackages => AvailablePatchPackages.Count > 0;

    private PatchPackage? _selectedPatchPackage;

    public PatchPackage? SelectedPatchPackage
    {
        get => _selectedPatchPackage;
        set
        {
            _selectedPatchPackage = value;
            OnPropertyChanged();
            MatchItems_Rebuild();
        }
    }

    private string? _sourcePath;
    public string? SourcePath
    {
        get => _sourcePath;
        set
        {
            _sourcePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SourceLabel));

            if (value is not null) 
                Analyze();
        }
    }

    private string? _patchPath;
    public string? PatchPath
    {
        get => _patchPath;
        set
        {
            _patchPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PatchLabel));

            if (value is not null)
                Analyze();
        }
    }

    private int _totalProgress;
    public int TotalProgress
    {
        get => _totalProgress;
        set { _totalProgress = value; OnPropertyChanged(); }
    }

    private string _progressSummary = string.Empty;
    public string ProgressSummary
    {
        get => _progressSummary;
        set { _progressSummary = value; OnPropertyChanged(); }
    }

    private string? _mismatchReason;
    public string? MismatchReason
    {
        get => _mismatchReason;
        set
        {
            _mismatchReason = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MismatchVisibility));
        }
    }

    public string SourceLabel => Path.GetFileName(SourcePath) ?? "원본 ZIP을 드래그하거나 클릭하세요";

    public string PatchLabel => Path.GetFileName(PatchPath) ?? "패치(IPS/폴더/ZIP)를 드래그하거나 클릭하세요";

    public Visibility HintVisibility => MatchItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility MismatchVisibility => MismatchReason is not null ? Visibility.Visible : Visibility.Collapsed;

    public void ManualMatch(ArcadeMatchItem item, PatchEntry? patch)
    {
        if (patch is not null)
        {
            var previousOwner = MatchItems.FirstOrDefault(x => x != item && ReferenceEquals(x.PatchEntry, patch));

            if (previousOwner is not null)
            {
                previousOwner.PatchEntry = null;
                previousOwner.PatchFileName = null;
            }

            if (!AllPatchEntries.Any(p => ReferenceEquals(p, patch)))
                AllPatchEntries.Add(patch);

            item.MismatchReason = null;
        }

        item.PatchEntry = patch;
        item.PatchFileName = patch?.DisplayName;

        RefreshUnmatchedPatches();
        UpdateSummary();
    }

    public void UpdateTotalProgress()
    {
        var matchedItems = MatchItems.Where(x => x.IsMatched).ToList();

        if (matchedItems.Count == 0)
        {
            TotalProgress = 0;
            return;
        }

        TotalProgress = (int)matchedItems.Average(x => x.Progress);
    }

    public void UpdateSummary()
    {
        var matchedItems = MatchItems.Where(x => x.IsMatched).ToList();
        int completedCount = matchedItems.Count(x => x.Progress >= 100);
        ProgressSummary = $"{completedCount} / {matchedItems.Count} 완료";
    }

    public void Clear()
    {
        SourcePath = null;
        PatchPath = null;
        MatchItems.Clear();
        AllPatchEntries.Clear();
        UnmatchedPatches.Clear();
        AvailablePatchPackages.Clear();
        SelectedPatchPackage = null;
        TotalProgress = 0;
        ProgressSummary = string.Empty;
        OnPropertyChanged(nameof(HintVisibility));
        OnPropertyChanged(nameof(HasPatchPackages));
    }

    private void Analyze()
    {
        if (SourcePath is null || PatchPath is null) 
            return;

        AvailablePatchPackages.Clear();

        foreach (var (fileName, content) in GetDatFiles(PatchPath).OrderBy(d => d.FileName))
            AvailablePatchPackages.Add(ParseDatFile(fileName, content));

        OnPropertyChanged(nameof(HasPatchPackages));

        SelectedPatchPackage = AvailablePatchPackages.FirstOrDefault();
    }

    private void MatchItems_Rebuild()
    {
        if (SourcePath is null || PatchPath is null) 
            return;

        MatchItems.Clear();

        var sourceEntries = GetSourceEntries(SourcePath);
        var patchEntries = GetPatchEntries(PatchPath);
        var usedPatches = new HashSet<PatchEntry>();
        var package = SelectedPatchPackage;

        AllPatchEntries.Clear();

        foreach (var p in patchEntries) AllPatchEntries.Add(p);

        var allowedPatchNames = package?.Entries.Select(e => e.PatchBaseName).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        foreach (var (fileName, fullPath, crc) in sourceEntries)
        {
            PatchEntry? matched = null;
            string? mismatchReason = null;

            var datEntry = package?.Entries.FirstOrDefault(
                e => string.Equals(e.SourceFileName, fileName, StringComparison.OrdinalIgnoreCase));

            if (datEntry is not null)
            {
                if (string.Equals(crc, datEntry.Crc, StringComparison.OrdinalIgnoreCase))
                {
                    matched = patchEntries
                        .Where(p => !usedPatches.Contains(p) &&
                            p.FileNameWithoutExtension.Contains(datEntry.PatchBaseName, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p.DisplayName)
                        .FirstOrDefault();

                    if (matched is null)
                        mismatchReason = $"마스터 데이터에 등록된 패치({datEntry.PatchBaseName}.ips)를 찾을 수 없습니다.";
                    else
                        mismatchReason = $"CRC 일치";
                }
                else
                    mismatchReason = $"CRC 불일치";
            }
            else
            {
                var ext = Path.GetExtension(fileName).TrimStart('.').ToLower();

                if (package == null || allowedPatchNames.Count == 0)
                {
                    matched = patchEntries
                        .Where(p => !usedPatches.Contains(p) &&
                        p.FileNameWithoutExtension.Contains(ext, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p.DisplayName)
                        .FirstOrDefault();                    
                }
                else
                {
                    matched = patchEntries
                        .Where(p => !usedPatches.Contains(p) &&
                            (package == null || !allowedPatchNames.Contains(p.FileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)) &&
                            p.FileNameWithoutExtension.Equals(ext, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p.DisplayName)
                        .FirstOrDefault();
                }
            }

            if (matched is not null)
                usedPatches.Add(matched);

            MatchItems.Add(new ArcadeMatchItem
            {
                SourceFileName = fileName,
                SourcePath = fullPath,
                PatchEntry = matched,
                PatchFileName = matched?.DisplayName,
                MismatchReason = mismatchReason
            });
        }

        RefreshUnmatchedPatches();
        UpdateSummary();
        OnPropertyChanged(nameof(HintVisibility));
    }

    private void RefreshUnmatchedPatches()
    {
        UnmatchedPatches.Clear();

        var used = MatchItems
            .Select(m => m.PatchEntry)
            .Where(p => p is not null)
            .Select(p => p!)
            .ToHashSet();

        foreach (var p in AllPatchEntries.Where(p => !used.Contains(p)))
            UnmatchedPatches.Add(p);
    }

    private static List<(string FileName, string FullPath, string Crc)> GetSourceEntries(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);

        return [.. zip.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .Select(e => (e.Name, $"{zipPath}|{e.FullName}", e.Crc32.ToString("x8")))];
    }

    private static List<PatchEntry> GetPatchEntries(string path)
    {
        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
        {
            using var zip = ZipFile.OpenRead(path);

            return [.. zip.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name) && e.Name.EndsWith(".ips", StringComparison.OrdinalIgnoreCase))
                .Select(e => new PatchEntry
                {
                    DisplayName = e.Name,
                    ZipPath = path,
                    EntryPath = e.FullName
                })];
        }

        if (File.Exists(path))
            return path.EndsWith(".ips", StringComparison.OrdinalIgnoreCase)
                ? [new PatchEntry { DisplayName = Path.GetFileName(path), EntryPath = path }] : [];

        if (Directory.Exists(path))
            return [.. Directory.GetFiles(path, "*.ips")
                .Select(f => new PatchEntry { DisplayName = Path.GetFileName(f), EntryPath = f })];

        return [];
    }

    private static List<(string FileName, string Content)> GetDatFiles(string patchPath)
    {
        if (patchPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(patchPath))
        {
            using var zip = ZipFile.OpenRead(patchPath);

            return [.. zip.Entries
                .Where(e => e.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                .Select(e =>
                {
                    using var stream = e.Open();
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

                    return (e.Name, reader.ReadToEnd());
                })];
        }

        if (Directory.Exists(patchPath))
            return [.. Directory.GetFiles(patchPath, "*.dat")
                .Select(f => (Path.GetFileName(f), File.ReadAllText(f, Encoding.UTF8)))];

        return [];
    }

    private static PatchPackage ParseDatFile(string fileName, string content)
    {
        var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        if (lines.Length > 0 && lines[0].Length > 0 && lines[0][0] == '\uFEFF')
            lines[0] = lines[0][1..];

        var entries = new List<PatchPackageEntry>();
        int i = 0;

        for (; i < lines.Length; i++)
        {
            var line = lines[i];

            if (string.IsNullOrWhiteSpace(line)) 
                break;

            var parts = line.Split('\t');

            if (parts.Length < 3) 
                break;

            var crcMatch = Regex.Match(parts[2], @"CRC\(([0-9a-fA-F]+)\)");

            if (!crcMatch.Success)
                break;

            entries.Add(new PatchPackageEntry
            {
                SourceFileName = parts[0].Trim(),
                PatchBaseName = parts[1].Trim(),
                Crc = crcMatch.Groups[1].Value.ToLowerInvariant()
            });
        }

        string? koTitle = null;
        string? firstTitle = null;

        for (; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (!line.StartsWith('[') || !line.EndsWith(']')) 
                continue;

            var locale = line[1..^1];
            string? title = null;

            for (int j = i + 1; j < lines.Length; j++)
            {
                if (string.IsNullOrWhiteSpace(lines[j])) 
                    continue;

                if (lines[j].Trim().StartsWith('['))
                    break;

                title = lines[j].Trim();

                break;
            }

            firstTitle ??= title;

            if (string.Equals(locale, "ko_KR", StringComparison.OrdinalIgnoreCase))
            {
                koTitle = title;
                break;
            }
        }

        return new PatchPackage
        {
            DisplayName = koTitle ?? firstTitle ?? Path.GetFileNameWithoutExtension(fileName),
            DatFileName = fileName,
            Entries = entries
        };
    }
}
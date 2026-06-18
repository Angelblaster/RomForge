using Common.WPF.ViewModels;
using RomForge.Core.Models;
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
    public List<PatchEntry> UnmatchedPatches { get; private set; } = [];

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
            if (value is not null) Analyze();
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
            if (value is not null) Analyze();
        }
    }

    public string SourceLabel => SourcePath ?? "원본 ZIP을 드래그하거나 클릭하세요";
    public string PatchLabel => PatchPath ?? "패치(IPS/폴더/ZIP)를 드래그하거나 클릭하세요";
    public Visibility HintVisibility => MatchItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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

    public void ManualMatch(ArcadeMatchItem item, PatchEntry patch)
    {
        item.PatchEntry = patch;
        item.PatchFileName = patch.DisplayName;
        item.MismatchReason = null;
        UpdateSummary();
    }

    public void UpdateTotalProgress()
    {
        if (MatchItems.Count == 0) { TotalProgress = 0; return; }
        TotalProgress = (int)MatchItems.Average(x => x.Progress);
    }

    public void UpdateSummary()
    {
        int matched = MatchItems.Count(x => x.IsMatched);
        ProgressSummary = $"{matched} / {MatchItems.Count} 매칭";
    }

    public void Clear()
    {
        SourcePath = null;
        PatchPath = null;
        MatchItems.Clear();
        UnmatchedPatches = [];
        AvailablePatchPackages.Clear();
        SelectedPatchPackage = null;
        TotalProgress = 0;
        ProgressSummary = string.Empty;
        OnPropertyChanged(nameof(HintVisibility));
        OnPropertyChanged(nameof(HasPatchPackages));
    }

    // SourcePath/PatchPath가 바뀔 때마다 dat 파일을 다시 스캔해서 패키지 목록을 갱신
    private void Analyze()
    {
        if (SourcePath is null || PatchPath is null) return;

        AvailablePatchPackages.Clear();

        foreach (var (fileName, content) in GetDatFiles(PatchPath).OrderBy(d => d.FileName))
            AvailablePatchPackages.Add(ParseDatFile(fileName, content));

        OnPropertyChanged(nameof(HasPatchPackages));

        // 패키지가 있으면 첫 번째를 기본 선택, 없으면 null (둘 다 setter에서 MatchItems_Rebuild 호출됨)
        SelectedPatchPackage = AvailablePatchPackages.FirstOrDefault();
    }

    // 선택된 패키지(또는 dat 없음)를 기준으로 실제 칩 파일 ↔ ips 매칭을 수행
    private void MatchItems_Rebuild()
    {
        if (SourcePath is null || PatchPath is null) return;

        MatchItems.Clear();

        var sourceEntries = GetSourceEntries(SourcePath);
        var patchEntries = GetPatchEntries(PatchPath);
        var usedPatches = new HashSet<PatchEntry>();
        var package = SelectedPatchPackage;

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
                }
                else
                {
                    mismatchReason = $"CRC 불일치 (예상 {datEntry.Crc}, 실제 {crc})";
                }
            }
            else
            {
                // dat에 없는 파일명 → 기존 확장자 추측 로직으로 폴백
                var ext = Path.GetExtension(fileName).TrimStart('.').ToLower();
                matched = patchEntries
                    .Where(p => !usedPatches.Contains(p) &&
                        p.FileNameWithoutExtension.Contains(ext, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p.DisplayName)
                    .FirstOrDefault();
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

        UnmatchedPatches = [.. patchEntries.Where(p => !usedPatches.Contains(p))];

        UpdateSummary();
        OnPropertyChanged(nameof(HintVisibility));
    }

    // 원본 zip의 엔트리명 + 풀패스("zip|entry") + CRC32(zip 메타데이터, 압축해제 불필요)
    private static List<(string FileName, string FullPath, string Crc)> GetSourceEntries(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        return [.. zip.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .Select(e => (e.Name, $"{zipPath}|{e.FullName}", e.Crc32.ToString("x8")))];
    }

    // .ips 파일만 패치 후보로 인정 (dat 파일은 별도 처리하므로 여기서 제외)
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
                ? [new PatchEntry { DisplayName = Path.GetFileName(path), EntryPath = path }]
                : [];

        if (Directory.Exists(path))
            return [.. Directory.GetFiles(path, "*.ips")
                .Select(f => new PatchEntry { DisplayName = Path.GetFileName(f), EntryPath = f })];

        return [];
    }

    // 패치 zip/폴더 내부의 .dat 파일을 전부 찾아서 (파일명, 본문텍스트)로 반환
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

    // dat 파일 본문을 파싱해서 PatchPackage로 변환
    // 형식: "원본파일명\t패치베이스명\tCRC(hex)" 줄들 → 빈 줄 → [locale] 섹션(설명 텍스트)들
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
            if (string.IsNullOrWhiteSpace(line)) break;

            var parts = line.Split('\t');
            if (parts.Length < 3) break;

            var crcMatch = Regex.Match(parts[2], @"CRC\(([0-9a-fA-F]+)\)");
            if (!crcMatch.Success) break;

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
            if (!line.StartsWith('[') || !line.EndsWith(']')) continue;

            var locale = line[1..^1];
            string? title = null;

            for (int j = i + 1; j < lines.Length; j++)
            {
                if (string.IsNullOrWhiteSpace(lines[j])) continue;
                if (lines[j].Trim().StartsWith('[')) break;
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
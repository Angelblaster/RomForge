using _3DS.Core.Crypto;
using _3DS.Core.Enums;
using _3DS.Core.FileSystem;
using _3DS.Core.Interfaces;
using _3DS.Core.Models;
using _3DS.Core.Services;
using Common;
using Common.WPF.ViewModels;
using NSW.Core.Enums;
using NSW.WPF.Services;
using RomForge.Helpers;
using RomForge.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels._3DS;

public class RepackMainViewModel : ToolTabViewModel
{
    private CancellationTokenSource _cts = new();
    private BuildMode? _currentMode;

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    private string _inputPath = string.Empty;
    private string _patchPath = string.Empty;
    private string _outputPath = string.Empty;
    private int _progressPct;
    private string _progressLabel = "대기 중...";    
    private string _progressPercent = string.Empty;
    private string _progressTime = "00:00 경과";
    private string _progressSpeed = string.Empty;

    private TitleViewModel? _romInfo;

    public string InputPath
    {
        get => _inputPath;
        set
        {
            _inputPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InputHintVisibility));
            _ = ParseRomInfoAsync(value);
        }
    }

    public string PatchPath
    {
        get => _patchPath;
        set { _patchPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(PatchHintVisibility)); }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            _outputPath = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputHintVisibility));

            if (string.IsNullOrEmpty(InputPath))
                _ = ParseRomInfoFromUnpackedAsync();
        }
    }

    public int ProgressPct
    {
        get => _progressPct;
        set { _progressPct = value; OnPropertyChanged(); }
    }

    public string ProgressLabel
    {
        get => _progressLabel;
        set { _progressLabel = value; OnPropertyChanged(); }
    }

    public string ProgressPercent
    {
        get => _progressPercent;
        set { _progressPercent = value; OnPropertyChanged(); }
    }

    public string ProgressTime
    {
        get => _progressTime;
        set { _progressTime = value; OnPropertyChanged(); }
    }

    public string ProgressSpeed
    {
        get => _progressSpeed;
        set { _progressSpeed = value; OnPropertyChanged(); }
    }

    public TitleViewModel? RomInfo
    {
        get => _romInfo;
        set { _romInfo = value; OnPropertyChanged(); OnPropertyChanged(nameof(RomInfoVisibility)); }
    }

    public Visibility InputHintVisibility => string.IsNullOrEmpty(InputPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PatchHintVisibility => string.IsNullOrEmpty(PatchPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OutputHintVisibility => string.IsNullOrEmpty(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RomInfoVisibility => RomInfo != null ? Visibility.Visible : Visibility.Collapsed;

    public bool IsUnpackRunning => IsLocked && _currentMode == BuildMode.UnpackOnly;

    public bool IsRebuildRunning => IsLocked && _currentMode == BuildMode.RebuildOnly;

    public bool IsFullRunning => IsLocked && _currentMode == BuildMode.FullProcess;

    public bool UnpackEnabled => !IsLocked || _currentMode == BuildMode.UnpackOnly;

    public bool RebuildEnabled => !IsLocked || _currentMode == BuildMode.RebuildOnly;

    public bool StartEnabled => !IsLocked || _currentMode == BuildMode.FullProcess;

    public ICommand BrowseInputCommand { get; }
    public ICommand BrowsePatchCommand { get; }
    public ICommand BrowseOutputCommand { get; }

    public RepackMainViewModel()
    {
        OutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
        BrowseInputCommand = new RelayCommand(async _ => await BrowseInput());
        BrowsePatchCommand = new RelayCommand(async _ => await BrowsePatch());
        BrowseOutputCommand = new RelayCommand(async _ => await BrowseOutput());

        _ = ParseRomInfoFromUnpackedAsync();

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsLocked))
                NotifyButtonStates();
        };
    }

    public async Task StartAsync(BuildMode mode)
    {
        if (!Validate(mode, out string error))
        {
            Log(error, LogLevel.Error);
            return;
        }

        _currentMode = mode;
        NotifyButtonStates();

        using (BeginWork())
        {
            try
            {
                _cts.Dispose();
                _cts = new CancellationTokenSource();
                await ExecuteAsync(mode, _cts.Token);
            }
            finally
            {
                ProgressPct = 0;
                _currentMode = null;
                NotifyButtonStates();
            }
        }
    }

    public void Cancel() => _cts.Cancel();

    private async Task ExecuteAsync(BuildMode mode, CancellationToken ct)
    {
        var keyStore = new KeyStore();
        string unpackedPath = Path.Combine(OutputPath, "unpacked");
        string outputCci = Utils.GetUniqueFilePath(Path.Combine(OutputPath, Path.GetFileNameWithoutExtension(InputPath) + "_Repack.cci"));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long startTime = System.Diagnostics.Stopwatch.GetTimestamp();
        var progress = BuildProgressReporter();
        string inputFileName = Path.GetFileNameWithoutExtension(InputPath);
        var reporter = new ProgressReporter(inputFileName, string.Empty, 0, progress);
        bool isCompleted = false;

        try
        {
            if (!Directory.Exists(OutputPath))
                Directory.CreateDirectory(OutputPath);

            switch (mode)
            {
                case BuildMode.UnpackOnly:
                    await UnpackAsync(keyStore, unpackedPath, reporter.CreateAction(), ct);
                    break;
                case BuildMode.RebuildOnly:
                    await RepackAsync(unpackedPath, reporter.CreateAction(), ct);
                    break;
                case BuildMode.FullProcess:
                    await RepackDirectAsync(keyStore, outputCci, reporter.CreateAction(), ct);
                    break;
            }

            isCompleted = true;
            Log($"완료! 총 소요: {sw.Elapsed:mm\\:ss}", LogLevel.Ok);
            OutputPath.OpenFolder();
        }
        catch (OperationCanceledException)
        {
            Log("작업이 취소되었습니다.", LogLevel.Error);
        }
        catch (Exception ex)
        {
            Log($"오류: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            if (!isCompleted)
            {
                if (mode != BuildMode.UnpackOnly && File.Exists(outputCci))
                    try { File.Delete(outputCci); } catch { }

                if (mode == BuildMode.UnpackOnly && Directory.Exists(unpackedPath))
                    try { Directory.Delete(unpackedPath, true); } catch { }
            }
        }
    }

    private Progress<ProgressInfo> BuildProgressReporter() =>
        new(info =>
        {
            ProgressPct = info.Percent;
            ProgressLabel = info.Label;
            ProgressPercent = $"{info.Percent}%";
            ProgressTime = info.TimeInfo;
            ProgressSpeed = info.Speed;
        });

    private async Task UnpackAsync(KeyStore keyStore, string unpackedPath, Action<long, long>? reporter = null, CancellationToken ct = default)
    {
        Log("언팩 시작...", LogLevel.Highlight);

        await using var source = await OpenSourceAsync(InputPath, keyStore, (msg, level, _) => Log(msg, level),ct);

        foreach (var content in source.Contents)
        {
            int idx = content.ContentIndex;
            var (ncchStream, _) = await source.OpenContentDecrypted(idx);

            await using (ncchStream)
            {
                byte[] hdrBuf = new byte[NcchHeader.Size];
                await ncchStream.ReadExactlyAsync(hdrBuf, ct);
                var ncchHeader = NcchHeader.Parse(hdrBuf);
                ncchStream.Position = 0;

                var unpack = await NcchUnpacker.UnpackAsync(ncchStream, ncchHeader,  ct);
                string partDir = Path.Combine(unpackedPath, $"partition{idx}");

                await NcchUnpacker.SaveToDirectoryAsync(ncchStream, unpack, partDir, content, reporter, ct);
                Log($"파티션 {idx} 언팩 완료", LogLevel.Info);
            }
        }
    }

    private async Task RepackAsync(string unpackedPath, Action<long, long>? reporter = null, CancellationToken ct = default)
    {
        Log("리팩 시작...", LogLevel.Highlight);

        string outputCci = Utils.GetUniqueFilePath(Path.Combine(OutputPath, _romInfo?.ShortDescription + "_Repack.cci"));

        var repackedNcchs = new Dictionary<int, (NcchUnpackResult, byte[], Stream, RomFsUnpackResult?, IRomFsFileSource?)>();
        var contentsList = new List<Contents>();

        int idx = 0;
        while (true)
        {
            string partDir = Path.Combine(unpackedPath, $"partition{idx}");
            if (!Directory.Exists(partDir))
                break;

            string headerPath = Path.Combine(partDir, "header.bin");
            byte[] headerRaw = await File.ReadAllBytesAsync(headerPath, ct);
            var ncchHeader = NcchHeader.Parse(headerRaw);

            string contentPath = Path.Combine(partDir, "content.bin");
            byte[] contentRaw = await File.ReadAllBytesAsync(contentPath, ct);
            using var cms = new MemoryStream(contentRaw);
            using var cbr = new BinaryReader(cms);
            var contents = new Contents
            {
                ContentId = cbr.ReadUInt32(),
                ContentIndex = cbr.ReadUInt16(),
                ContentType = cbr.ReadUInt16(),
            };
            contentsList.Add(contents);

            byte[]? exHeader = null;
            byte[]? logo = null;
            byte[]? plainRegion = null;

            string exHeaderPath = Path.Combine(partDir, "exheader.bin");
            string logoPath = Path.Combine(partDir, "logo.bin");
            string plainPath = Path.Combine(partDir, "plain.bin");

            if (File.Exists(exHeaderPath)) 
                exHeader = await File.ReadAllBytesAsync(exHeaderPath, ct);

            if (File.Exists(logoPath)) 
                logo = await File.ReadAllBytesAsync(logoPath, ct);

            if (File.Exists(plainPath)) 
                plainRegion = await File.ReadAllBytesAsync(plainPath, ct);

            string? exefsPatchDir = idx == 0 ? GetPatchDir("exefs") : null;
            string exefsDir = Path.Combine(partDir, "exefs");
            var exefsFiles = Directory.Exists(exefsDir)
                ? ExeFsUnpacker.LoadFromDirectory(exefsDir)
                : [];
            byte[] exefsBlock = exefsFiles.Count > 0
                ? await ExeFsPacker.PackWithPatchAsync(exefsFiles, exefsPatchDir, ct)
                : [];

            string? romfsPatchDir = idx == 0 ? GetPatchDir("romfs") : null;
            string romfsDir = Path.Combine(partDir, "romfs");

            RomFsUnpackResult? romfsResult = null;
            IRomFsFileSource? romfsSource = null;

            if (Directory.Exists(romfsDir))
            {
                romfsResult = RomFsPacker.ScanFolderAsUnpackResult(romfsDir);
                romfsSource = romfsPatchDir != null ? new PatchFolderFileSource(romfsPatchDir) : null;
                romfsSource = new FolderRomFsFileSource(romfsDir, romfsSource);
            }

            var unpackResult = new NcchUnpackResult
            {
                Header = ncchHeader,
                ExHeader = exHeader,
                Logo = logo,
                PlainRegion = plainRegion,
                ExeFs = null,
                RomFs = romfsResult,
            };

            repackedNcchs[idx] = (unpackResult, exefsBlock, Stream.Null, romfsResult, romfsSource);

            idx++;
        }

        if (repackedNcchs.Count == 0)
        {
            Log("언팩된 파티션이 없습니다.", LogLevel.Error);
            return;
        }

        var repackedSource = await RepackedNcsdSource.CreateAsync(repackedNcchs, contentsList, ct);

        await using var outputStream = File.Open(outputCci, FileMode.Create, FileAccess.ReadWrite);
        await NcsdBuilder.BuildAsync(repackedSource, outputStream, reporter, ct);

        Log($"출력: {outputCci}", LogLevel.Ok);
    }

    private async Task RepackDirectAsync(KeyStore keyStore, string outputCci, Action<long, long>? reporter = null, CancellationToken ct = default)
    {
        Log("메모리 기반 리팩 시작...", LogLevel.Highlight);

        await using var source = await OpenSourceAsync(InputPath, keyStore, (msg, level, _) => Log(msg, level), ct);

        var repackedNcchs = new Dictionary<int, (NcchUnpackResult, byte[], Stream, RomFsUnpackResult?, IRomFsFileSource?)>();

        foreach (var content in source.Contents)
        {
            int idx = content.ContentIndex;
            var (ncchStream, _) = await source.OpenContentDecrypted(idx);

            byte[] hdrBuf = new byte[NcchHeader.Size];
            await ncchStream.ReadExactlyAsync(hdrBuf, ct);
            var ncchHeader = NcchHeader.Parse(hdrBuf);
            ncchStream.Position = 0;

            var unpack = await NcchUnpacker.UnpackAsync(ncchStream, ncchHeader,  ct);

            string? exefsPatchDir = GetPatchDir("exefs");
            string? romfsPatchDir = GetPatchDir("romfs");

            byte[] exefsBlock = unpack.ExeFs != null
                ? await ExeFsPacker.PackWithPatchAsync(unpack.ExeFs.Files, idx == 0 ? exefsPatchDir : null, ct)
                : [];

            IRomFsFileSource? patchSource = idx == 0 && romfsPatchDir != null
                ? new PatchFolderFileSource(romfsPatchDir)
                : null;

            repackedNcchs[idx] = (unpack, exefsBlock, ncchStream, unpack.RomFs, patchSource);
        }

        var repackedSource = await RepackedNcsdSource.CreateAsync(repackedNcchs, source.Contents, ct);

        await using var outputStream = File.Open(outputCci, FileMode.Create, FileAccess.ReadWrite);
        await NcsdBuilder.BuildAsync(repackedSource, outputStream, reporter, ct);

        Log($"출력: {outputCci}", LogLevel.Ok);
    }

    private string? GetPatchDir(string subFolder)
    {
        if (string.IsNullOrEmpty(PatchPath))
            return null;

        string path = Path.Combine(PatchPath, subFolder);
        return Directory.Exists(path) ? path : null;
    }

    private static async Task<INcsdSource> OpenSourceAsync(string inputPath, KeyStore keyStore, Action<string, LogLevel, string>? log = null, CancellationToken ct = default)
    {
        string ext = Path.GetExtension(inputPath).ToLowerInvariant();

        return ext switch
        {
            ".cia" => await new CiaReader(keyStore).OpenAsync(inputPath, log, ct),
            ".cci" or ".3ds" => await CciSource.OpenAsync(inputPath, keyStore, log, ct),
            _ => throw new NotSupportedException($"지원하지 않는 파일 형식: {ext}")
        };
    }

    private bool Validate(BuildMode mode, out string error)
    {
        error = string.Empty;

        if (mode != BuildMode.RebuildOnly && string.IsNullOrEmpty(InputPath))
        {
            error = "원본 파일을 선택하세요.";
            return false;
        }

        if (string.IsNullOrEmpty(OutputPath))
        {
            error = "작업 폴더를 선택하세요.";
            return false;
        }

        if (mode == BuildMode.RebuildOnly)
        {
            string unpackedPath = Path.Combine(OutputPath, "unpacked");
            if (!Directory.Exists(unpackedPath))
            {
                error = "언팩된 데이터가 없습니다.";
                return false;
            }
        }

        return true;
    }

    private void NotifyButtonStates()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            OnPropertyChanged(nameof(IsUnpackRunning));
            OnPropertyChanged(nameof(IsRebuildRunning));
            OnPropertyChanged(nameof(IsFullRunning));
            OnPropertyChanged(nameof(UnpackEnabled));
            OnPropertyChanged(nameof(RebuildEnabled));
            OnPropertyChanged(nameof(StartEnabled));
        });
    }

    private void Log(string msg, LogLevel level = LogLevel.Info) =>
        Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));

    private async Task BrowseInput()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "원본 파일 선택",
            Filter = "3DS ROM 파일|*.cci;*.3ds;*.cia"
        };
        if (dlg.ShowDialog() == true)
            InputPath = dlg.FileName;
    }

    private async Task BrowsePatch()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "패치 폴더 선택" };
        if (dlg.ShowDialog() == true)
            PatchPath = dlg.FolderName;
    }

    private async Task BrowseOutput()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "작업 폴더 선택" };
        if (dlg.ShowDialog() == true)
            OutputPath = dlg.FolderName;
    }

    private async Task ParseRomInfoAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            await ParseRomInfoFromUnpackedAsync();
            return;
        }

        try
        {
            var result = await Util.ParseFile(path);
            var vm = new TitleViewModel
            {
                FilePath = path,
                Title = result.Title!,
                ProductCode = result.ProductCode,
                ShortDescription = result.ShortDescription,
                Publisher = result.Publisher,
                Crypto = result.Crypto
            };

            if (result.IconPixels is not null)
            {
                var bitmap = BitmapSource.Create(48, 48, 96, 96, PixelFormats.Bgr32, null, result.IconPixels, 48 * 4);
                bitmap.Freeze();
                vm.Icon = bitmap;
            }

            RomInfo = vm;
        }
        catch
        {
            RomInfo = null;
        }
    }

    private async Task ParseRomInfoFromUnpackedAsync()
    {
        try
        {
            string partition0 = Path.Combine(OutputPath, "unpacked", "partition0");
            string headerPath = Path.Combine(partition0, "header.bin");
            string iconPath = Path.Combine(partition0, "exefs", "icon.bin");

            if (!File.Exists(headerPath))
            {
                RomInfo = null;
                return;
            }

            byte[] headerRaw = await File.ReadAllBytesAsync(headerPath);
            var ncchHeader = NcchHeader.Parse(headerRaw);

            SmdhInfo? smdhInfo = null;
            if (File.Exists(iconPath))
            {
                byte[] iconData = await File.ReadAllBytesAsync(iconPath);
                smdhInfo = SmdhParser.TryParse(iconData);
            }

            var vm = new TitleViewModel
            {
                FilePath = string.Empty,
                Title = new InstalledTitle
                {
                    TitleId = ncchHeader.ProgramId.ToString("x16"),
                    Version = ncchHeader.Version,
                    ContentSize = 0,
                    ContentPath = string.Empty,
                    Type = (TitleType)(ncchHeader.ProgramId >> 32)
                },
                ProductCode = ncchHeader.ProductCodeString,
                ShortDescription = smdhInfo?.ShortDescription ?? string.Empty,
                Publisher = smdhInfo?.Publisher ?? string.Empty,
                Crypto = !ncchHeader.NoCrypto
            };

            if (smdhInfo?.IconPixels is not null)
            {
                var bitmap = BitmapSource.Create(48, 48, 96, 96, PixelFormats.Bgr32, null, smdhInfo.IconPixels, 48 * 4);
                bitmap.Freeze();
                vm.Icon = bitmap;
            }

            RomInfo = vm;
        }
        catch
        {
            RomInfo = null;
        }
    }
}
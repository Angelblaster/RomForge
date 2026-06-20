using Common.WPF.ViewModels;
using System.IO;

namespace RomForge.ViewModels.PS1;

public class DiscFileItem(string filePath) : ViewModelBase
{
    public string FilePath { get; } = filePath;

    public string FileName => Path.GetFileName(FilePath);

    public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();

    private int _no;
    public int No { get => _no; set { _no = value; OnPropertyChanged(); } }

    private string _gameId = "인식중...";
    public string GameId { get => _gameId; set { _gameId = value; OnPropertyChanged(); } }

    private long _fileSizeBytes;
    public long FileSizeBytes
    {
        get => _fileSizeBytes;
        set { _fileSizeBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSize)); }
    }

    public string FileSize => FileSizeBytes <= 0 ? "..." : FileSizeBytes >= 1024L * 1024 * 1024
        ? $"{FileSizeBytes / (1024.0 * 1024 * 1024):F2} GB"
        : $"{FileSizeBytes / (1024.0 * 1024):F1} MB";
}
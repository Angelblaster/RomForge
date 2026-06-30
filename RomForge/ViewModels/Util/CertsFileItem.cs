using Common.WPF.ViewModels;
using System.IO;

namespace RomForge.ViewModels.Util;

public class CertsFileItem(string filePath) : ViewModelBase
{
    private string _status = "대기중";
    private int _progress;

    public string FilePath { get; } = filePath;
    public string FileName => Path.GetFileName(FilePath);
    public string Directory => Path.GetDirectoryName(FilePath) ?? string.Empty;

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }
}
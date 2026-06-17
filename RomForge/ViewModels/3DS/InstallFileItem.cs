using Common.WPF.ViewModels;
using System.Windows.Media;

namespace RomForge.ViewModels._3DS;

public class InstallFileItem : ViewModelBase
{
    private double _progress;
    private int _no;

    public int No
    {
        get => _no;
        set { _no = value; OnPropertyChanged(); }
    }

    public string FileName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public string Directory { get; init; } = string.Empty;

    public string FileSize { get; init; } = string.Empty;

    public string ExtensionLabel { get; init; } = string.Empty;

    public SolidColorBrush ExtensionBackground { get; init; } = new(Color.FromRgb(0x4F, 0x8E, 0xF7));

    public double Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }
}
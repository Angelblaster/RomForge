using Common;
using RomForge.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RomForge.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private int _selectedTabIndex;

    private readonly AppConfig _config = new AppConfig().Load();

    public PatchViewModel PatchVM { get; }

    public CompressViewModel CompressVM { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveLogEntries)); }
    }

    public System.Collections.ObjectModel.ObservableCollection<LogEntry> ActiveLogEntries => _selectedTabIndex == 0 ? PatchVM.LogEntries : CompressVM.LogEntries;

    #region 압축 설정 프로퍼티

    public double SwitchCompressLevel
    {
        get => _config.Switch.CompressLevel;
        set { _config.Switch.CompressLevel = (int)value; OnPropertyChanged(); }
    }

    public bool SwitchIsValidationEnabled
    {
        get => _config.Switch.VerifyCompress;
        set { _config.Switch.VerifyCompress = value; OnPropertyChanged(); }
    }

    public bool SwitchUseBlockMode
    {
        get => _config.Switch.UseBlockMode;
        set { _config.Switch.UseBlockMode = value; if (value) _config.Switch.UseBlocklessMode = false; OnPropertyChanged(); OnPropertyChanged(nameof(SwitchUseBlocklessMode)); }
    }

    public bool SwitchUseBlocklessMode
    {
        get => _config.Switch.UseBlocklessMode;
        set { _config.Switch.UseBlocklessMode = value; if (value) _config.Switch.UseBlockMode = false; OnPropertyChanged(); OnPropertyChanged(nameof(SwitchUseBlockMode)); }
    }

    public double AzaharCompressLevel
    {
        get => _config.Azahar.CompressLevel;
        set { _config.Azahar.CompressLevel = (int)value; OnPropertyChanged(); }
    }

    public double DolphinCompressLevel
    {
        get => _config.Dolphin.CompressLevel;
        set { _config.Dolphin.CompressLevel = (int)value; OnPropertyChanged(); }
    }

    #endregion

    #region 패치 설정 프로퍼티

    public bool OutputModeNormal
    {
        get => _config.Patch.OutputMode == OutputMode.Normal;
        set
        {
            if (value)
                _config.Patch.OutputMode = OutputMode.Normal;

            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputModeArcade));
        }
    }

    public bool OutputModeArcade
    {
        get => _config.Patch.OutputMode == OutputMode.Arcade;
        set
        {
            if (value) 
                _config.Patch.OutputMode = OutputMode.Arcade;

            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputModeNormal));
        }
    }

    public bool UseCustomOutputFolder
    {
        get => _config.Patch.OutputFolder != null;
        set
        {
            _config.Patch.OutputFolder = value ? _config.Patch.OutputFolder ?? string.Empty : null;

            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputFolder));
        }
    }

    public string OutputFolder
    {
        get => _config.Patch.OutputFolder ?? string.Empty;
        set { _config.Patch.OutputFolder = string.IsNullOrWhiteSpace(value) ? null : value; OnPropertyChanged(); }
    }

    #endregion

    public static string AppVersion => $"{AppDomain.CurrentDomain.FriendlyName} - Ver {Utils.ToAppVersionString()}";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        PatchVM = new PatchViewModel(_config);
        CompressVM = new CompressViewModel(_config);
    }

    public void SaveConfig() => _config.Save();

    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
using Common.WPF.ViewModels;
using RomForge.Core;

namespace RomForge.ViewModels.Settings;

public class PS1SettingsMainViewModel(AppConfig config) : ToolTabViewModel
{
    public double CompressLevel
    {
        get => config.PS1.CompressLevel;
        set { config.PS1.CompressLevel = (int)value; OnPropertyChanged(); }
    }

    public bool UseGameIdMode
    {
        get => config.PS1.UseGameIdMode;
        set
        {
            config.PS1.UseGameIdMode = value;
            if (value) config.PS1.UseFileNameMode = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UseFileNameMode));
        }
    }

    public bool UseFileNameMode
    {
        get => config.PS1.UseFileNameMode;
        set
        {
            config.PS1.UseFileNameMode = value;
            if (value) config.PS1.UseGameIdMode = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UseGameIdMode));
        }
    }
}
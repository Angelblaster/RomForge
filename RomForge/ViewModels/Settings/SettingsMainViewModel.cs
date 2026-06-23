using Common.WPF.ViewModels;
using RomForge.Core;

namespace RomForge.ViewModels.Settings;

public class SettingsMainViewModel(AppConfig config) : ToolTabViewModel
{
    public PatchSettingsMainViewModel Patch { get; } = new(config);

    public CompressSettingsMainViewModel Compress { get; } = new(config);

    public PS1SettingsMainViewModel PS1 { get; } = new(config);

}
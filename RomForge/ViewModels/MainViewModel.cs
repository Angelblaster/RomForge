using Common;
using Common.WPF.ViewModels;
using RomForge.Core;
using RomForge.Models;
using RomForge.ViewModels._3DS;
using RomForge.ViewModels.Patch;
using RomForge.ViewModels.PS1;
using RomForge.ViewModels.Settings;
using RomForge.ViewModels.Switch;
using RomForge.ViewModels.Util;
using System.Collections.ObjectModel;

namespace RomForge.ViewModels;

public class MainViewModel : ToolTabViewModel
{
    private int _selectedTabIndex;
    private readonly AppConfig _config = new AppConfig().Load();

    public PatchMainViewModel PatchVM { get; }

    public CompressMainViewModel CompressVM { get; }

    public SwitchMainViewModel SwitchMainVM { get; }

    public _3DSMainViewModel Main3DsVM { get; }

    public PS1MainViewModel PS1MainVM { get; }

    public UtilMainViewModel UtilMainVM { get; }

    public SettingsMainViewModel Settings { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            _selectedTabIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveLogEntries));
        }
    }

    public ObservableCollection<LogEntry> ActiveLogEntries => _selectedTabIndex switch
    {
        0 => PatchVM.LogEntries,
        1 => CompressVM.LogEntries,
        2 => SwitchMainVM.LogEntries,
        3 => Main3DsVM.LogEntries,
        4 => PS1MainVM.LogEntries,
        5 => UtilMainVM.LogEntries,
        _ => PatchVM.LogEntries
    };

    public static string AppVersion => $"{AppDomain.CurrentDomain.FriendlyName} - Ver {Utils.ToAppVersionString()}";

    public MainViewModel()
    {
        PatchVM = new PatchMainViewModel(_config);
        CompressVM = new CompressMainViewModel(_config);
        SwitchMainVM = new SwitchMainViewModel(_config);
        Main3DsVM = new _3DSMainViewModel();
        PS1MainVM = new PS1MainViewModel();
        UtilMainVM = new UtilMainViewModel();
        Settings = new SettingsMainViewModel(_config);

        RegisterChild(PatchVM);
        RegisterChild(CompressVM);
        RegisterChild(SwitchMainVM);
        RegisterChild(Main3DsVM);
        RegisterChild(PS1MainVM);
        RegisterChild(UtilMainVM);
        RegisterChild(Settings);
    }

    public void SaveConfig() => _config.Save();
}
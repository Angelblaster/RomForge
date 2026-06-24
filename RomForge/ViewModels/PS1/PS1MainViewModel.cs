using RomForge.Core;

namespace RomForge.ViewModels.PS1;

public class PS1MainViewModel : MultiToolTabViewModel
{
    public PackingMainViewModel PackingVM { get; }
    public UnpackingMainViewModel UnPackingVM { get; }

    public PS1MainViewModel(AppConfig config)
    {
        PackingVM = new PackingMainViewModel(config);
        UnPackingVM = new UnpackingMainViewModel();

        Tools.Add(PackingVM);
        Tools.Add(UnPackingVM);

        InitializeMultiTools();
    }
}
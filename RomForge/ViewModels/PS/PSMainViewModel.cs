namespace RomForge.ViewModels.PS;

public class PS1MainViewModel : MultiToolTabViewModel
{
    public PackingMainViewModel PackingVM { get; } = new();

    public UnpackingMainViewModel UnPackingVM { get; } = new();

    public PSPConverterViewModel ConverterVM { get; } = new();


    public event EventHandler RunNavigatePackingSettings;

    public PS1MainViewModel()
    {
        PackingVM.RunNavigateSettings += (sender, e) => RunNavigatePackingSettings?.Invoke(sender, e);

        Tools.Add(PackingVM);
        Tools.Add(UnPackingVM);
        Tools.Add(ConverterVM);

        InitializeMultiTools();
    }
}
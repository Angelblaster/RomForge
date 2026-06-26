namespace RomForge.ViewModels._3DS;

public class _3DSMainViewModel : MultiToolTabViewModel
{
    public RepackMainViewModel RepackVM { get; }
    public InstallerMainViewModel InstallerVM { get; }
    public ConverterMainViewModel ConverterVM { get; }

    public _3DSMainViewModel()
    {
        RepackVM = new RepackMainViewModel();
        InstallerVM = new InstallerMainViewModel();
        ConverterVM = new ConverterMainViewModel();

        Tools.Add(RepackVM);
        Tools.Add(InstallerVM);
        Tools.Add(ConverterVM);

        InitializeMultiTools();
    }
}
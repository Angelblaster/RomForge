namespace RomForge.ViewModels.PSP;

public class PSPMainViewModel : MultiToolTabViewModel
{
    public PSPConverterViewModel ConverterVM { get; }

    public PSPMainViewModel()
    {
        ConverterVM = new PSPConverterViewModel();

        Tools.Add(ConverterVM);

        InitializeMultiTools();
    }
}
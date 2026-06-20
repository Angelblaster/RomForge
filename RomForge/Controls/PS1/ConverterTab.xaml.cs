using RomForge.ViewModels.PS1;
using System.Windows;
using System.Windows.Controls;

namespace RomForge.Controls.PS1;

public partial class ConverterTab : UserControl
{
    private ConverterMainViewModel? ViewModel => DataContext as ConverterMainViewModel;


    public ConverterTab()
    {
        InitializeComponent();
    }

    private void LvFiles_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
            return;

        ViewModel?.AddPaths(paths);
    }

    private void Icon0_Drop(object sender, DragEventArgs e)
    {

    }

    private void Pic0_Drop(object sender, DragEventArgs e)
    {

    }

    private void Pic1_Drop(object sender, DragEventArgs e)
    {

    }
}
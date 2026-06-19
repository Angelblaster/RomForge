using System.Windows;
using System.Windows.Controls;
using RomForge.ViewModels.Switch;

namespace RomForge.Controls.Switch;

public partial class KeygenTab : UserControl
{
    private KeygenMainViewModel ViewModel => (KeygenMainViewModel)DataContext;

    public KeygenTab()
    {
        InitializeComponent();
    }

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);

        if (VisualParent == null)
            ViewModel?.Cancel();
    }

    private async void BtnStartWork_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) 
            return;

        if (ViewModel.IsLocked)
            ViewModel.Cancel();
        else
            await ViewModel.ExecuteStartWorkAsync(fileMgr.GameFiles);
    }
}
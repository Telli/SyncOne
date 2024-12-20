namespace SyncOne.Views;

public partial class ConfigurationPage : ContentPage
{
    public ConfigurationPage(SyncOne.ViewModels.ConfigurationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

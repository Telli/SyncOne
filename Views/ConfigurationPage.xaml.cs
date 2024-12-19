namespace SyncOne.Views;

public partial class ConfigurationPage : ContentPage
{
    public ConfigurationPage(ViewModels.ConfigurationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
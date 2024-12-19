using SyncOne.ViewModels;

namespace SyncOne
{
    public partial class MainPage : ContentPage
    {

        private readonly MainViewModel _viewModel;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.Initialize(Navigation);
        }

    }

}

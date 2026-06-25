using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using TextProcessor.ViewModels;

namespace TextProcessor;

public partial class MainWindow : Window
{
    private const double ExpandedFeaturesHeight = 600;
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            _viewModel = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsFeaturesExpanded) && _viewModel is not null)
            AnimateFeaturesPanel(_viewModel.IsFeaturesExpanded);
    }

    private void AnimateFeaturesPanel(bool expand)
    {
        var animation = new DoubleAnimation
        {
            To = expand ? ExpandedFeaturesHeight : 0,
            Duration = TimeSpan.FromMilliseconds(60),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        FeaturesContent.BeginAnimation(System.Windows.FrameworkElement.MaxHeightProperty, animation);
    }
}

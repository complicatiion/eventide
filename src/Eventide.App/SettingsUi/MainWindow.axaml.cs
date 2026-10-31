using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace Eventide.App.SettingsUi;

public partial class MainWindow : Window
{
    private SettingsViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Closing += (_, _) => _viewModel?.Shutdown();
        Opened += (_, _) => FitToScreen();
        DataContextChanged += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelChanged;
            }

            _viewModel = DataContext as SettingsViewModel;
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelChanged;
            }
        };
    }

    /// <summary>Title bar and brand area move the window (client area extended into the title bar).</summary>
    private void OnDragAreaPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.Page) || _viewModel is null)
        {
            return;
        }

        PageScroll.Offset = default;
        Control page = _viewModel.Page switch
        {
            SettingsPage.Filter => PageFilter,
            SettingsPage.Schedule => PageSchedule,
            SettingsPage.General => PageGeneral,
            SettingsPage.About => PageAbout,
            _ => PageStatus,
        };
        AnimateIn(page);
    }

    /// <summary>Soft page change: start invisible and 14 px lower, then let the style transitions run to the target.</summary>
    private static void AnimateIn(Control page)
    {
        // Jump to the start values without animation …
        page.Transitions = new Transitions();
        page.Opacity = 0;
        page.RenderTransform = TransformOperations.Parse("translateY(14px)");

        // … then restore the style transitions and animate to the end values.
        Dispatcher.UIThread.Post(() =>
        {
            page.ClearValue(Animatable.TransitionsProperty);
            page.Opacity = 1;
            page.RenderTransform = TransformOperations.Parse("translateY(0px)");
        }, DispatcherPriority.Render);
    }

    /// <summary>Default size 980 × 860, but never larger than 90 % of the work area (small / laptop screens).</summary>
    private void FitToScreen()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var scaling = screen.Scaling;
        var area = screen.WorkingArea;
        var maxWidth = area.Width / scaling * 0.9;
        var maxHeight = area.Height / scaling * 0.9;
        if (Width <= maxWidth && Height <= maxHeight)
        {
            return;
        }

        Width = Math.Max(MinWidth, Math.Min(Width, maxWidth));
        Height = Math.Max(MinHeight, Math.Min(Height, maxHeight));
        Position = new PixelPoint(
            area.X + (int)((area.Width - (Width * scaling)) / 2),
            area.Y + (int)((area.Height - (Height * scaling)) / 2));
    }
}

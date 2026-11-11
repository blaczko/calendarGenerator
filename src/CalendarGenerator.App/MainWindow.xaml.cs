using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CalendarGenerator.App.Controls;
using CalendarGenerator.App.ViewModels;
using CalendarGenerator.Core.Models;

namespace CalendarGenerator.App;

public partial class MainWindow : Window
{
    private readonly List<CalendarPageControl> _previewPages = [];

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private MainViewModel Vm => (MainViewModel)DataContext;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsurePreviewPages();
        SyncOrientationRadios();
        Vm.PhotosChanged += (_, _) =>
        {
            SyncCropEditor();
            SyncLivePreview();
        };
        Vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainViewModel.CurrentPage)
                or nameof(MainViewModel.Project)
                or nameof(MainViewModel.SelectedTemplateId)
                or nameof(MainViewModel.SelectedOrientation))
            {
                SyncCropEditor();
                SyncLivePreview();
            }
            else if (args.PropertyName == nameof(MainViewModel.SelectedMonth))
            {
                SyncCropEditor();
            }
            else if (args.PropertyName == nameof(MainViewModel.PreviewImage))
            {
                // "Frissítés" and theme/layout updates bump PreviewImage — rebuild pages if visible.
                SyncLivePreview();
            }
            else if (args.PropertyName == nameof(MainViewModel.SelectedPhoto))
            {
                CropEditor.SetPhoto(Vm.SelectedPhoto?.Photo);
                SelectPhotoOnPreviewPages(Vm.SelectedPhoto?.Photo.Id);
            }
        };
        SyncCropEditor();
        SyncLivePreview();
    }

    private void EnsurePreviewPages()
    {
        if (_previewPages.Count > 0)
            return;

        for (var month = 1; month <= 12; month++)
        {
            var page = new CalendarPageControl { Tag = month };
            page.PhotoLayoutChanged += LivePreviewPage_PhotoLayoutChanged;

            var frame = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xEB, 0xE3)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 18),
                Child = new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = page
                }
            };

            PreviewPagesHost.Children.Add(frame);
            _previewPages.Add(page);
        }
    }

    private void SyncCropEditor()
    {
        if (!IsLoaded) return;
        CropEditor.SetPhoto(Vm.SelectedPhoto?.Photo);
    }

    private void SyncLivePreview()
    {
        if (!IsLoaded) return;
        EnsurePreviewPages();
        // Rebuild all month pages when Preview is open (or data changed via PhotosChanged).
        if (Vm.IsPreview)
            Vm.BindAllInteractivePreviews(_previewPages);
    }

    private void SelectPhotoOnPreviewPages(Guid? photoId)
    {
        foreach (var page in _previewPages)
            page.SelectInteractivePhoto(photoId);
    }

    private void LivePreviewPage_PhotoLayoutChanged(object? sender, EventArgs e) =>
        Vm.NotifyPhotoLayoutChanged();

    private void CropEditor_CropChanged(object? sender, EventArgs e)
    {
        foreach (var page in _previewPages)
            page.RefreshInteractivePhotos();
        Vm.NotifyPhotoLayoutChanged();
        Vm.StatusMessage = "Vágás mentve.";
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
            return;

        Vm.CurrentPage = NavPage.Photos;
        await Vm.ImportPhotosAsync(files);
    }

    private void Portrait_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.SelectedOrientation = PageOrientation.Portrait;
    }

    private void Landscape_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.SelectedOrientation = PageOrientation.Landscape;
    }

    private void SyncOrientationRadios()
    {
        LandscapeRadio.IsChecked = Vm.SelectedOrientation == PageOrientation.Landscape;
    }
}

using System.Windows;
using CalendarGenerator.App.ViewModels;
using CalendarGenerator.Core.Models;

namespace CalendarGenerator.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private MainViewModel Vm => (MainViewModel)DataContext;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncOrientationRadios();
        Vm.PhotosChanged += (_, _) =>
        {
            SyncCropEditor();
            SyncLivePreview();
        };
        Vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainViewModel.CurrentPage)
                or nameof(MainViewModel.SelectedMonth)
                or nameof(MainViewModel.Project)
                or nameof(MainViewModel.SelectedTemplateId)
                or nameof(MainViewModel.SelectedOrientation))
            {
                SyncCropEditor();
                SyncLivePreview();
            }
            else if (args.PropertyName == nameof(MainViewModel.SelectedPhoto))
            {
                CropEditor.SetPhoto(Vm.SelectedPhoto?.Photo);
                LivePreviewPage.SelectInteractivePhoto(Vm.SelectedPhoto?.Photo.Id);
            }
            else if (args.PropertyName == nameof(MainViewModel.PreviewImage))
            {
                LivePreviewPage.RefreshInteractivePhotos();
            }
        };
        SyncCropEditor();
        SyncLivePreview();
    }

    private void SyncCropEditor()
    {
        if (!IsLoaded) return;
        CropEditor.SetPhoto(Vm.SelectedPhoto?.Photo);
    }

    private void SyncLivePreview()
    {
        if (!IsLoaded) return;
        Vm.BindInteractivePreview(LivePreviewPage);
    }

    private void LivePreviewPage_PhotoLayoutChanged(object? sender, EventArgs e) =>
        Vm.NotifyPhotoLayoutChanged();

    private void CropEditor_CropChanged(object? sender, EventArgs e)
    {
        LivePreviewPage.RefreshInteractivePhotos();
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

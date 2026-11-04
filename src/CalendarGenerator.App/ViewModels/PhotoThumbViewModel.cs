using CalendarGenerator.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CalendarGenerator.App.ViewModels;

public partial class PhotoThumbViewModel : ObservableObject
{
    public PhotoItem Photo { get; }

    [ObservableProperty] private string _displayPath = string.Empty;

    public PhotoThumbViewModel(PhotoItem photo)
    {
        Photo = photo;
        DisplayPath = !string.IsNullOrWhiteSpace(photo.ProjectCopyPath)
            ? photo.ProjectCopyPath!
            : photo.SourcePath;
    }
}

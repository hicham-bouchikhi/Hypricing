using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Hypricing.Desktop.ViewModels;

namespace Hypricing.Desktop.Views;

public partial class WallpaperView : UserControl
{
    public WallpaperView() => InitializeComponent();

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var folders = await TopLevel.GetTopLevel(this)!
            .StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select wallpaper folder",
                AllowMultiple = false,
            });

        if (folders.Count > 0 && DataContext is WallpaperViewModel vm)
        {
            var path = folders[0].Path.LocalPath;
            vm.SetFolder(path);
            vm.SaveFolder(path);
        }
    }
}

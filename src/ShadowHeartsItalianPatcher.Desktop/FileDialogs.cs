using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Diagnostics;
namespace ShadowHeartsItalianPatcher.Desktop;

public interface IFileDialogs
{
    Task<string?> SelectIso(); Task<string?> SelectFolder(); void OpenFolder(string path);
}
public sealed class FileDialogs(Window window) : IFileDialogs
{
    public async Task<string?> SelectIso()
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Seleziona la tua ISO originale USA",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Immagini ISO") { Patterns = ["*.iso", "*.ISO"] }]
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
    public async Task<string?> SelectFolder()
    {
        var dirs = await window.StorageProvider.OpenFolderPickerAsync(new()
        {
            Title = "Destinazione della nuova ISO italiana",
            AllowMultiple = false
        });
        return dirs.FirstOrDefault()?.TryGetLocalPath();
    }
    public void OpenFolder(string path)
    {
        if (Directory.Exists(path))
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }
}

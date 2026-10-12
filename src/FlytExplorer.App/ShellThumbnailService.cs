using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace FlytExplorer.App;

// Fetches shell thumbnails via WinRT StorageFile/StorageFolder.GetThumbnailAsync.
// Must be called from the UI-thread dispatcher (BitmapImage.SetSourceAsync requires it).
// Returns null on failure or when the OS returns a generic icon (indistinguishable from glyph).
public sealed class ShellThumbnailService
{
    public async Task<BitmapImage?> GetThumbnailAsync(
        string path, uint sizePx, CancellationToken ct)
    {
        try
        {
            IStorageItemProperties storageItem;
            if (Directory.Exists(path))
                storageItem = await StorageFolder.GetFolderFromPathAsync(path).AsTask(ct);
            else
                storageItem = await StorageFile.GetFileFromPathAsync(path).AsTask(ct);

            ct.ThrowIfCancellationRequested();

            using var thumb = await storageItem
                .GetThumbnailAsync(ThumbnailMode.SingleItem, sizePx)
                .AsTask(ct);

            if (thumb == null) return null;

            // ThumbnailType.Icon means the OS returned a generic shell icon —
            // visually the same as our glyph, so skip to avoid the redundant load.
            if (thumb.Type == ThumbnailType.Icon) return null;

            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(thumb).AsTask(ct);
            return bmp;
        }
        catch (OperationCanceledException) { return null; }
        catch { return null; }
    }
}

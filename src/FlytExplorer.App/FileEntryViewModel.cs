using System.ComponentModel;
using FlytExplorer.Core;
using FlytExplorer.Core.Models;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FlytExplorer.App;

public sealed class FileEntryViewModel(FileEntry entry) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName => entry.DisplayName;
    public string FullPath    => entry.FullPath;
    public FileEntryKind Kind => entry.Kind;

    private (string Glyph, string ColorHex) IconInfo =>
        FileIconMapper.GetIconInfo(entry.Kind, Path.GetExtension(entry.FullPath));

    public string IconGlyph    => IconInfo.Glyph;
    public string IconColorHex => IconInfo.ColorHex;

    private BitmapImage? _thumbnailBitmap;
    public BitmapImage? ThumbnailBitmap
    {
        get => _thumbnailBitmap;
        set
        {
            _thumbnailBitmap = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailBitmap)));
        }
    }

    public long?           SizeBytes    => entry.SizeBytes;
    public DateTimeOffset? DateModified => entry.DateModified;

    public string SizeDisplay => entry.SizeBytes switch
    {
        null                  => "",
        < 1024                => $"{entry.SizeBytes} B",
        < 1024 * 1024         => $"{entry.SizeBytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{entry.SizeBytes / (1024.0 * 1024):F1} MB",
        _                     => $"{entry.SizeBytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    public string DateModifiedDisplay =>
        entry.DateModified?.LocalDateTime.ToString("yyyy/MM/dd  HH:mm") ?? "";

    public string ParentPath => Path.GetDirectoryName(entry.FullPath) ?? "";
}

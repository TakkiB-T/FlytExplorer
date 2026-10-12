using FlytExplorer.Core.Models;

namespace FlytExplorer.App;

public sealed class FileEntryViewModel(FileEntry entry)
{
    public string DisplayName    => entry.DisplayName;
    public string FullPath       => entry.FullPath;
    public FileEntryKind Kind    => entry.Kind;

    // Segoe MDL2 Assets glyphs — filled variants are more visible at small sizes
    public string KindGlyph => entry.Kind switch
    {
        FileEntryKind.Directory => "\uED41",  // FolderFill (solid folder)
        _                       => "\uE8A5"   // Document
    };

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

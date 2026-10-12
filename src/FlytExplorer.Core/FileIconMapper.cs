using FlytExplorer.Core.Models;

namespace FlytExplorer.Core;

public static class FileIconMapper
{
    private record struct IconInfo(string Glyph, string ColorHex);

    private const string GlyphFolder   = "\uED41"; // FolderFill
    private const string GlyphPhoto    = "\uEB9F"; // Photo
    private const string GlyphMusic    = "\uE8D6"; // MusicNote
    private const string GlyphDocument = "\uE8A5"; // Document
    private const string GlyphZip      = "\uE8B1"; // ZipFolder

    private static readonly IconInfo _folder  = new(GlyphFolder,   "#98B5AC");
    private static readonly IconInfo _default = new(GlyphDocument, "#A2B0B4");

    private static readonly Dictionary<string, IconInfo> _map =
        new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"]  = new(GlyphPhoto, "#9B8EC4"),
        [".jpg"]  = new(GlyphPhoto, "#9B8EC4"),
        [".jpeg"] = new(GlyphPhoto, "#9B8EC4"),
        [".gif"]  = new(GlyphPhoto, "#9B8EC4"),
        [".bmp"]  = new(GlyphPhoto, "#9B8EC4"),
        [".webp"] = new(GlyphPhoto, "#9B8EC4"),
        [".ico"]  = new(GlyphPhoto, "#9B8EC4"),
        [".svg"]  = new(GlyphPhoto, "#9B8EC4"),
        [".tiff"] = new(GlyphPhoto, "#9B8EC4"),
        [".heic"] = new(GlyphPhoto, "#9B8EC4"),
        [".mp3"]  = new(GlyphMusic, "#C4778E"),
        [".wav"]  = new(GlyphMusic, "#C4778E"),
        [".flac"] = new(GlyphMusic, "#C4778E"),
        [".ogg"]  = new(GlyphMusic, "#C4778E"),
        [".aac"]  = new(GlyphMusic, "#C4778E"),
        [".m4a"]  = new(GlyphMusic, "#C4778E"),
        [".wma"]  = new(GlyphMusic, "#C4778E"),
        [".mp4"]  = new(GlyphDocument, "#C47878"),
        [".avi"]  = new(GlyphDocument, "#C47878"),
        [".mkv"]  = new(GlyphDocument, "#C47878"),
        [".mov"]  = new(GlyphDocument, "#C47878"),
        [".wmv"]  = new(GlyphDocument, "#C47878"),
        [".webm"] = new(GlyphDocument, "#C47878"),
        [".flv"]  = new(GlyphDocument, "#C47878"),
        [".m4v"]  = new(GlyphDocument, "#C47878"),
        [".zip"]  = new(GlyphZip, "#C4A06E"),
        [".rar"]  = new(GlyphZip, "#C4A06E"),
        [".7z"]   = new(GlyphZip, "#C4A06E"),
        [".tar"]  = new(GlyphZip, "#C4A06E"),
        [".gz"]   = new(GlyphZip, "#C4A06E"),
        [".bz2"]  = new(GlyphZip, "#C4A06E"),
        [".cab"]  = new(GlyphZip, "#C4A06E"),
        [".xz"]   = new(GlyphZip, "#C4A06E"),
        [".cs"]   = new(GlyphDocument, "#7EB5C4"),
        [".py"]   = new(GlyphDocument, "#7EB5C4"),
        [".js"]   = new(GlyphDocument, "#7EB5C4"),
        [".ts"]   = new(GlyphDocument, "#7EB5C4"),
        [".html"] = new(GlyphDocument, "#7EB5C4"),
        [".css"]  = new(GlyphDocument, "#7EB5C4"),
        [".json"] = new(GlyphDocument, "#7EB5C4"),
        [".xml"]  = new(GlyphDocument, "#7EB5C4"),
        [".xaml"] = new(GlyphDocument, "#7EB5C4"),
        [".yaml"] = new(GlyphDocument, "#7EB5C4"),
        [".yml"]  = new(GlyphDocument, "#7EB5C4"),
        [".sh"]   = new(GlyphDocument, "#7EB5C4"),
        [".ps1"]  = new(GlyphDocument, "#7EB5C4"),
        [".bat"]  = new(GlyphDocument, "#7EB5C4"),
        [".cmd"]  = new(GlyphDocument, "#7EB5C4"),
        [".cpp"]  = new(GlyphDocument, "#7EB5C4"),
        [".c"]    = new(GlyphDocument, "#7EB5C4"),
        [".h"]    = new(GlyphDocument, "#7EB5C4"),
        [".java"] = new(GlyphDocument, "#7EB5C4"),
        [".go"]   = new(GlyphDocument, "#7EB5C4"),
        [".rs"]   = new(GlyphDocument, "#7EB5C4"),
        [".rb"]   = new(GlyphDocument, "#7EB5C4"),
        [".php"]  = new(GlyphDocument, "#7EB5C4"),
        [".docx"] = new(GlyphDocument, "#7EA8C4"),
        [".doc"]  = new(GlyphDocument, "#7EA8C4"),
        [".odt"]  = new(GlyphDocument, "#7EA8C4"),
        [".rtf"]  = new(GlyphDocument, "#7EA8C4"),
        [".xlsx"] = new(GlyphDocument, "#7EC49E"),
        [".xls"]  = new(GlyphDocument, "#7EC49E"),
        [".csv"]  = new(GlyphDocument, "#7EC49E"),
        [".ods"]  = new(GlyphDocument, "#7EC49E"),
        [".pptx"] = new(GlyphDocument, "#C4A86E"),
        [".ppt"]  = new(GlyphDocument, "#C4A86E"),
        [".odp"]  = new(GlyphDocument, "#C4A86E"),
        [".pdf"]  = new(GlyphDocument, "#C47878"),
        [".exe"]  = new(GlyphDocument, "#A0A8B0"),
        [".msi"]  = new(GlyphDocument, "#A0A8B0"),
        [".appx"] = new(GlyphDocument, "#A0A8B0"),
        [".msix"] = new(GlyphDocument, "#A0A8B0"),
        [".dll"]  = new(GlyphDocument, "#7A8088"),
        [".sys"]  = new(GlyphDocument, "#7A8088"),
        [".drv"]  = new(GlyphDocument, "#7A8088"),
    };

    public static (string Glyph, string ColorHex) GetIconInfo(FileEntryKind kind, string extension)
    {
        if (kind == FileEntryKind.Directory)
            return (_folder.Glyph, _folder.ColorHex);

        if (!string.IsNullOrEmpty(extension) && _map.TryGetValue(extension, out var info))
            return (info.Glyph, info.ColorHex);

        return (_default.Glyph, _default.ColorHex);
    }
}
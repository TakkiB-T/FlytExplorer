using FlytExplorer.Core;
using FlytExplorer.Core.Models;
using Xunit;

namespace FlytExplorer.UnitTests;

public sealed class FileIconMapperTests
{
    // ---- Directory -----------------------------------------------------------

    [Fact]
    public void Directory_ReturnsAccentColor()
    {
        var (_, color) = FileIconMapper.GetIconInfo(FileEntryKind.Directory, "");
        Assert.Equal("#98B5AC", color);
    }

    [Fact]
    public void Directory_ReturnsFolderGlyph()
    {
        var (glyph, _) = FileIconMapper.GetIconInfo(FileEntryKind.Directory, "");
        Assert.Equal("\uED41", glyph); // FolderFill
    }

    // ---- Images --------------------------------------------------------------

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".gif")]
    [InlineData(".svg")]
    [InlineData(".webp")]
    public void Image_ReturnsPhotoGlyphAndPurple(string ext)
    {
        var (glyph, color) = FileIconMapper.GetIconInfo(FileEntryKind.File, ext);
        Assert.Equal("\uEB9F", glyph); // Photo
        Assert.Equal("#9B8EC4", color);
    }

    // ---- Audio ---------------------------------------------------------------

    [Theory]
    [InlineData(".mp3")]
    [InlineData(".wav")]
    [InlineData(".flac")]
    [InlineData(".aac")]
    public void Audio_ReturnsMusicGlyphAndRose(string ext)
    {
        var (glyph, color) = FileIconMapper.GetIconInfo(FileEntryKind.File, ext);
        Assert.Equal("\uE8D6", glyph); // MusicNote
        Assert.Equal("#C4778E", color);
    }

    // ---- Archives ------------------------------------------------------------

    [Theory]
    [InlineData(".zip")]
    [InlineData(".rar")]
    [InlineData(".7z")]
    [InlineData(".tar")]
    [InlineData(".gz")]
    public void Archive_ReturnsZipGlyphAndAmber(string ext)
    {
        var (glyph, color) = FileIconMapper.GetIconInfo(FileEntryKind.File, ext);
        Assert.Equal("\uE8B1", glyph); // ZipFolder
        Assert.Equal("#C4A06E", color);
    }

    // ---- Code ----------------------------------------------------------------

    [Theory]
    [InlineData(".cs")]
    [InlineData(".py")]
    [InlineData(".js")]
    [InlineData(".ts")]
    [InlineData(".json")]
    [InlineData(".yaml")]
    public void Code_ReturnsDocumentGlyphAndCyan(string ext)
    {
        var (glyph, color) = FileIconMapper.GetIconInfo(FileEntryKind.File, ext);
        Assert.Equal("\uE8A5", glyph); // Document
        Assert.Equal("#7EB5C4", color);
    }

    // ---- Video ---------------------------------------------------------------

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".mkv")]
    [InlineData(".avi")]
    public void Video_ReturnsSoftRed(string ext)
    {
        var (_, color) = FileIconMapper.GetIconInfo(FileEntryKind.File, ext);
        Assert.Equal("#C47878", color);
    }

    // ---- Documents -----------------------------------------------------------

    [Fact]
    public void Docx_ReturnsSteelBlue()
    {
        var (_, color) = FileIconMapper.GetIconInfo(FileEntryKind.File, ".docx");
        Assert.Equal("#7EA8C4", color);
    }

    [Fact]
    public void Xlsx_ReturnsMintGreen()
    {
        var (_, color) = FileIconMapper.GetIconInfo(FileEntryKind.File, ".xlsx");
        Assert.Equal("#7EC49E", color);
    }

    [Fact]
    public void Pptx_ReturnsGolden()
    {
        var (_, color) = FileIconMapper.GetIconInfo(FileEntryKind.File, ".pptx");
        Assert.Equal("#C4A86E", color);
    }

    // ---- Fallback ------------------------------------------------------------

    [Fact]
    public void UnknownExtension_ReturnsDocumentGlyphAndMuted()
    {
        var (glyph, color) = FileIconMapper.GetIconInfo(FileEntryKind.File, ".xyz");
        Assert.Equal("\uE8A5", glyph);
        Assert.Equal("#A2B0B4", color);
    }

    [Fact]
    public void EmptyExtension_ReturnsDefault()
    {
        var (glyph, color) = FileIconMapper.GetIconInfo(FileEntryKind.File, "");
        Assert.Equal("\uE8A5", glyph);
        Assert.Equal("#A2B0B4", color);
    }

    // ---- Case insensitivity --------------------------------------------------

    [Fact]
    public void Extension_CaseInsensitive()
    {
        var (g1, c1) = FileIconMapper.GetIconInfo(FileEntryKind.File, ".PNG");
        var (g2, c2) = FileIconMapper.GetIconInfo(FileEntryKind.File, ".png");
        Assert.Equal(g1, g2);
        Assert.Equal(c1, c2);
    }

    // ---- Hex format ----------------------------------------------------------

    [Fact]
    public void AllColors_AreValidHexFormat()
    {
        string[] exts = { ".png", ".mp3", ".zip", ".cs", ".mp4",
                          ".docx", ".xlsx", ".pptx", ".pdf", ".exe", ".dll", ".xyz" };
        foreach (var ext in exts)
        {
            var (_, color) = FileIconMapper.GetIconInfo(FileEntryKind.File, ext);
            Assert.True(color.Length == 7 && color[0] == '#',
                $"Color '{color}' for '{ext}' is not in #RRGGBB format");
        }
    }
}
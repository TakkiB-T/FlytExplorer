using FlytExplorer.Core.Models;
using FlytExplorer.FileSystem;
using Xunit;

namespace FlytExplorer.UnitTests;

public sealed class LocalFolderProviderTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFolderProvider _provider = new();

    public LocalFolderProviderTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(), "FlytExplorer_UnitTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task EmptyFolder_ReturnsNoEntries()
    {
        var entries = await Collect(_root);
        Assert.Empty(entries);
    }

    [Fact]
    public async Task FolderWith100Files_Returns100Entries()
    {
        for (int i = 0; i < 100; i++)
            File.WriteAllText(Path.Combine(_root, $"file_{i:D4}.txt"), "x");

        var entries = await Collect(_root);
        Assert.Equal(100, entries.Count(e => e.Kind == FileEntryKind.File));
    }

    [Fact]
    public async Task Subdirectory_IsReturnedAsDirectory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "subdir"));

        var entries = await Collect(_root);
        Assert.Single(entries, e => e.Kind == FileEntryKind.Directory && e.DisplayName == "subdir");
    }

    [Fact]
    public async Task UnicodeFileName_IsHandled()
    {
        var name = "テスト_🗂️_file.txt";
        File.WriteAllText(Path.Combine(_root, name), "unicode");

        var entries = await Collect(_root);
        Assert.Contains(entries, e => e.DisplayName == name);
    }

    [Fact]
    public async Task Cancellation_StopsEnumeration()
    {
        for (int i = 0; i < 500; i++)
            File.WriteAllText(Path.Combine(_root, $"f{i}.dat"), "x");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Collect(_root, cts.Token));
    }

    private async Task<List<FileEntry>> Collect(string path, CancellationToken ct = default)
    {
        var list = new List<FileEntry>();
        await foreach (var e in _provider.EnumerateAsync(path, ct))
            list.Add(e);
        return list;
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }
}

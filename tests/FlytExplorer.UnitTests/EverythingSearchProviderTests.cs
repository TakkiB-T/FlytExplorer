using FlytExplorer.Search;
using Xunit;

namespace FlytExplorer.UnitTests;

// These tests require Everything 1.4+ running as x64.
// Tests are skipped when Everything is unavailable so CI without Everything still passes.
public sealed class EverythingSearchProviderTests
{
    private readonly EverythingSearchProvider _provider = new();

    [SkippableFact]
    public async Task GetHealth_WhenEverythingRunning_ReturnsAvailable()
    {
        var health = await _provider.GetHealthAsync(CancellationToken.None);

        Skip.If(!health.IsAvailable, $"Everything unavailable: {health.StatusMessage}");

        Assert.True(health.IsAvailable);
        Assert.NotNull(health.Version);
    }

    [SkippableFact]
    public async Task Search_EmptyQuery_ReturnsBoundedResults()
    {
        var health = await _provider.GetHealthAsync(CancellationToken.None);
        Skip.If(!health.IsAvailable, "Everything not running");

        var page = await _provider.SearchAsync(
            new Core.Models.SearchRequest("", null, 0, 50, 1),
            CancellationToken.None);

        Assert.True(page.Items.Count <= 50);
        Assert.True(page.TotalCount > 0);
    }

    [SkippableFact]
    public async Task Search_Paging_SecondPageDiffers()
    {
        var health = await _provider.GetHealthAsync(CancellationToken.None);
        Skip.If(!health.IsAvailable, "Everything not running");

        const uint limit = 25;
        var page1 = await _provider.SearchAsync(
            new Core.Models.SearchRequest("", null, 0, limit, 1), CancellationToken.None);
        var page2 = await _provider.SearchAsync(
            new Core.Models.SearchRequest("", null, limit, limit, 2), CancellationToken.None);

        Skip.If(page1.TotalCount < limit * 2, "Not enough indexed results for paging test");
        Assert.NotEqual(page1.Items[0].FullPath, page2.Items[0].FullPath);
    }

    [SkippableFact]
    public async Task Search_WhenEverythingNotRunning_ThrowsUnavailable()
    {
        // Only meaningful when Everything is actually not running.
        var health = await _provider.GetHealthAsync(CancellationToken.None);
        Skip.If(health.IsAvailable, "Everything is running; unavailable path not testable here");

        await Assert.ThrowsAsync<EverythingUnavailableException>(() =>
            _provider.SearchAsync(
                new Core.Models.SearchRequest("test", null, 0, 10, 1),
                CancellationToken.None));
    }

    // ── Phase 2 search tests ──────────────────────────────────────────────

    [SkippableFact]
    public async Task Search_Latency_IsUnder500ms()
    {
        var health = await _provider.GetHealthAsync(CancellationToken.None);
        Skip.If(!health.IsAvailable, "Everything not running");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _provider.SearchAsync(
            new Core.Models.SearchRequest(".exe", null, 0, 50, 1),
            CancellationToken.None);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 500,
            $"Search latency {sw.ElapsedMilliseconds} ms exceeded 500 ms target");
    }

    [SkippableFact]
    public async Task Search_FolderScope_ReturnsSubsetOfGlobal()
    {
        var health = await _provider.GetHealthAsync(CancellationToken.None);
        Skip.If(!health.IsAvailable, "Everything not running");

        var globalPage = await _provider.SearchAsync(
            new Core.Models.SearchRequest(".txt", null, 0, 200, 1),
            CancellationToken.None);
        Skip.If(globalPage.TotalCount == 0, "No .txt files indexed");

        // Use the first result's parent directory as scope
        var scopePath = Path.GetDirectoryName(globalPage.Items[0].FullPath);
        Skip.If(string.IsNullOrEmpty(scopePath), "Could not determine scope path");

        var scopedPage = await _provider.SearchAsync(
            new Core.Models.SearchRequest(".txt", scopePath, 0, 200, 2),
            CancellationToken.None);

        // Scoped results must be a subset (≤ global total) and all under scope path
        Assert.True(scopedPage.TotalCount <= globalPage.TotalCount,
            "Scoped result count should not exceed global count");
        Assert.All(scopedPage.Items, item =>
            Assert.True(item.FullPath.StartsWith(scopePath!, StringComparison.OrdinalIgnoreCase),
                $"Result '{item.FullPath}' is outside scope '{scopePath}'"));
    }

    [SkippableFact]
    public async Task Search_GenerationTracking_StaleResultsIgnorable()
    {
        // This test verifies the SearchPage carries the generation number
        // so callers can discard stale results.
        var health = await _provider.GetHealthAsync(CancellationToken.None);
        Skip.If(!health.IsAvailable, "Everything not running");

        const int latestGen = 42;
        var page = await _provider.SearchAsync(
            new Core.Models.SearchRequest("a", null, 0, 10, latestGen),
            CancellationToken.None);

        Assert.Equal(latestGen, page.Generation);
    }

    // ── Phase 8 BuildQuery tests (no Everything required) ────────────────

    [Fact]
    public void BuildQuery_NoScope_ReturnsTextOnly()
    {
        var q = EverythingSearchProvider.BuildQuery("*.jpg", null);
        Assert.Equal("*.jpg", q);
    }

    [Fact]
    public void BuildQuery_EmptyScope_ReturnsTextOnly()
    {
        var q = EverythingSearchProvider.BuildQuery("*.jpg", "");
        Assert.Equal("*.jpg", q);
    }

    [Fact]
    public void BuildQuery_ScopeWithoutSpaces_QuotesPath()
    {
        var q = EverythingSearchProvider.BuildQuery("*.txt", @"C:\Projects");
        Assert.Equal(@"path:""C:\Projects"" *.txt", q);
    }

    [Fact]
    public void BuildQuery_ScopeWithSpaces_QuotesPath()
    {
        // Paths with spaces must be quoted so Everything parses them correctly.
        var q = EverythingSearchProvider.BuildQuery("report", @"C:\Users\John Doe\Documents");
        Assert.Equal(@"path:""C:\Users\John Doe\Documents"" report", q);
    }

    [Fact]
    public void BuildQuery_UncScope_QuotesPath()
    {
        var q = EverythingSearchProvider.BuildQuery("*.log", @"\\server\share\logs");
        Assert.Equal(@"path:""\\server\share\logs"" *.log", q);
    }
}

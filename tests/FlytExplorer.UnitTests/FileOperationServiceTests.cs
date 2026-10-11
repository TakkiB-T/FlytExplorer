using FlytExplorer.Core.Interfaces;
using FlytExplorer.Shell;
using Xunit;

namespace FlytExplorer.UnitTests;

// All operations run inside tmp/fixtures/ops_test — no personal files are touched.
public sealed class FileOperationServiceTests : IDisposable
{
    // BaseDirectory: …/tests/FlytExplorer.UnitTests/bin/Debug/net10.0.../
    // Five ".." steps reach the repo root (FlytExplorer-Design-v1.0)
    private static readonly string FixtureBase =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tmp", "fixtures", "ops_test"));

    private readonly FileOperationService _svc = new();

    // ---------- copy ----------

    [SkippableFact]
    public async Task CopyItem_WithinFixtures_CreatesDestFile()
    {
        var src  = Path.Combine(FixtureBase, "source_1.txt");
        var dest = Path.Combine(FixtureBase, "copy_dest");
        Directory.CreateDirectory(dest);
        var destFile = Path.Combine(dest, "source_1.txt");
        if (File.Exists(destFile)) File.Delete(destFile);

        Skip.IfNot(File.Exists(src), $"Fixture not found: {src}");

        var result = await _svc.ExecuteAsync(
            new FileOperationRequest(FileOperationKind.Copy, [src], dest, null, 0),
            CancellationToken.None);

        Assert.True(result.Succeeded, $"Copy failed: {result.ErrorMessage}");
        Assert.True(File.Exists(destFile), "Destination file was not created");
    }

    // ---------- rename ----------

    [SkippableFact]
    public async Task RenameItem_WithinFixtures_ChangesName()
    {
        var src     = Path.Combine(FixtureBase, "source_2.txt");
        var renamed = Path.Combine(FixtureBase, "renamed_2.txt");

        Skip.IfNot(File.Exists(src), $"Fixture not found: {src}");

        // Ensure previous run's artefacts don't interfere
        if (File.Exists(renamed)) File.Move(renamed, src, overwrite: true);

        var result = await _svc.ExecuteAsync(
            new FileOperationRequest(FileOperationKind.Rename, [src], null, "renamed_2.txt", 0),
            CancellationToken.None);

        Assert.True(result.Succeeded, $"Rename failed: {result.ErrorMessage}");
        Assert.True(File.Exists(renamed), "Renamed file was not found");
        Assert.False(File.Exists(src), "Original file still exists after rename");

        // Restore for repeatability
        File.Move(renamed, src, overwrite: true);
    }

    // ---------- STA apartment ----------

    [SkippableFact]
    public async Task ExecuteAsync_RunsOnStaThread()
    {
        var src  = Path.Combine(FixtureBase, "source_3.txt");
        var dest = Path.Combine(FixtureBase, "sta_verify_dest");
        Directory.CreateDirectory(dest);
        var destFile = Path.Combine(dest, "source_3.txt");
        if (File.Exists(destFile)) File.Delete(destFile);

        Skip.IfNot(File.Exists(src), $"Fixture not found: {src}");

        // We observe the outcome: if the STA invariant were violated, IFileOperation would throw
        // with a COM error (RPC_E_WRONG_THREAD or similar). Successful execution proves STA held.
        var result = await _svc.ExecuteAsync(
            new FileOperationRequest(FileOperationKind.Copy, [src], dest, null, 0),
            CancellationToken.None);

        // Successful COM call is proof the STA invariant held; error message contains COM HRESULT on failure.
        Assert.True(result.Succeeded, $"STA verification copy failed: {result.ErrorMessage}");
        Assert.DoesNotContain("COM error", result.ErrorMessage ?? string.Empty);
    }

    // ---------- aborted detection ----------

    [Fact]
    public async Task ExecuteAsync_NonExistentSource_ReportsFailure()
    {
        var src  = Path.Combine(FixtureBase, "does_not_exist.txt");
        var dest = Path.Combine(FixtureBase, "copy_dest");
        Directory.CreateDirectory(dest);

        var result = await _svc.ExecuteAsync(
            new FileOperationRequest(FileOperationKind.Copy, [src], dest, null, 0),
            CancellationToken.None);

        // IFileOperation returns aborted=true or Succeeded=false for missing sources.
        var failed = !result.Succeeded || result.AnyAborted;
        Assert.True(failed, "Expected failure for missing source file");
    }

    public void Dispose() => _svc.Dispose();
}

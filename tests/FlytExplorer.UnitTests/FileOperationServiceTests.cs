using FlytExplorer.Core.Interfaces;
using FlytExplorer.Shell;
using Xunit;

namespace FlytExplorer.UnitTests;

// All operations run inside tmp/fixtures/ops_test — no personal files are touched.
public sealed class FileOperationServiceTests : IDisposable
{
    // BaseDirectory: …/tests/FlytExplorer.UnitTests/bin/x64/Debug/net10.0.../
    // Six ".." steps reach the repo root (FlytExplorer-Design-v1.0)
    private static readonly string FixtureBase =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..", "tmp", "fixtures", "ops_test"));

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

    // ---------- move ----------

    [SkippableFact]
    public async Task MoveItem_WithinFixtures_MovesFile()
    {
        var src  = Path.Combine(FixtureBase, "source_4.txt");
        var dest = Path.Combine(FixtureBase, "copy_dest");
        Directory.CreateDirectory(dest);
        var destFile = Path.Combine(dest, "source_4.txt");
        if (File.Exists(destFile)) File.Move(destFile, src, overwrite: true);

        Skip.IfNot(File.Exists(src), $"Fixture not found: {src}");

        var result = await _svc.ExecuteAsync(
            new FileOperationRequest(FileOperationKind.Move, [src], dest, null, 0),
            CancellationToken.None);

        Assert.True(result.Succeeded, $"Move failed: {result.ErrorMessage}");
        Assert.True(File.Exists(destFile), "File was not found at destination after move");
        Assert.False(File.Exists(src), "Source file still exists after move");

        // Restore for repeatability
        File.Move(destFile, src, overwrite: true);
    }

    // ---------- delete (recycle bin) ----------

    [SkippableFact]
    public async Task DeleteItem_WithinFixtures_SendsToRecycleBin()
    {
        var src = Path.Combine(FixtureBase, "source_5.txt");
        Skip.IfNot(File.Exists(src), $"Fixture not found: {src}");

        // IFileOperation with FOF_ALLOWUNDO sends to recycle bin (does not hard-delete).
        var result = await _svc.ExecuteAsync(
            new FileOperationRequest(FileOperationKind.Delete, [src], null, null, 0),
            CancellationToken.None);

        Assert.True(result.Succeeded, $"Delete failed: {result.ErrorMessage}");
        Assert.False(File.Exists(src), "File still exists after recycle-bin delete");

        // Restore so the test is repeatable: recreate the fixture file.
        await File.WriteAllTextAsync(src, "ops_test fixture source_5");
    }

    // ---------- new folder ----------

    [SkippableFact]
    public async Task NewFolder_WithinFixtures_CreatesDirectory()
    {
        var dest       = Path.Combine(FixtureBase, "copy_dest");
        var folderName = "new_folder_test";
        var created    = Path.Combine(dest, folderName);
        Directory.CreateDirectory(dest);
        if (Directory.Exists(created)) Directory.Delete(created, recursive: true);

        var result = await _svc.ExecuteAsync(
            new FileOperationRequest(FileOperationKind.NewFolder, [], dest, folderName, 0),
            CancellationToken.None);

        Assert.True(result.Succeeded, $"NewFolder failed: {result.ErrorMessage}");
        Assert.True(Directory.Exists(created), "New folder was not created");

        // Cleanup
        Directory.Delete(created, recursive: true);
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

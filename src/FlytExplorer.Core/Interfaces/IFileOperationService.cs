using FlytExplorer.Core.Models;

namespace FlytExplorer.Core.Interfaces;

public enum FileOperationKind { Copy, Move, Rename, Delete, NewFolder }

public sealed record FileOperationRequest(
    FileOperationKind Kind,
    IReadOnlyList<string> SourcePaths,
    string? DestinationPath,
    string? NewName,
    nint OwnerHwnd);

public sealed record FileOperationOutcome(
    bool Succeeded,
    bool AnyAborted,
    string? ErrorMessage);

public interface IFileOperationService
{
    Task<FileOperationOutcome> ExecuteAsync(
        FileOperationRequest request,
        CancellationToken cancellationToken);
}

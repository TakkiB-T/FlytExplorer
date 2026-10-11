using FlytExplorer.Core.Models;

namespace FlytExplorer.Core.Interfaces;

public interface IFolderProvider
{
    IAsyncEnumerable<FileEntry> EnumerateAsync(string path, CancellationToken cancellationToken);
}

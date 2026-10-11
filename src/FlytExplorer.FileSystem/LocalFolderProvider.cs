using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FlytExplorer.Core.Interfaces;
using FlytExplorer.Core.Models;

namespace FlytExplorer.FileSystem;

public sealed class LocalFolderProvider : IFolderProvider
{
    private const int ChannelCapacity = 512;

    public async IAsyncEnumerable<FileEntry> EnumerateAsync(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<FileEntry>(
            new BoundedChannelOptions(ChannelCapacity) { FullMode = BoundedChannelFullMode.Wait });

        // Enumerate on thread pool — keeps blocking IO off the UI thread.
        var producer = Task.Run(async () =>
        {
            try
            {
                var opts = new EnumerationOptions
                {
                    IgnoreInaccessible      = true,
                    RecurseSubdirectories   = false,
                    AttributesToSkip        = 0,
                };

                foreach (var fsi in new DirectoryInfo(path).EnumerateFileSystemInfos("*", opts))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var kind     = fsi is DirectoryInfo ? FileEntryKind.Directory : FileEntryKind.File;
                    long?   size = fsi is FileInfo fi   ? fi.Length               : null;
                    DateTimeOffset? modified = fsi.LastWriteTimeUtc == DateTime.MinValue
                        ? null
                        : new DateTimeOffset(fsi.LastWriteTimeUtc);

                    await channel.Writer.WriteAsync(
                        new FileEntry(fsi.FullName, fsi.Name, kind, size, modified, fsi.Attributes, FileEntryOrigin.Local),
                        cancellationToken);
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        await foreach (var entry in channel.Reader.ReadAllAsync(cancellationToken))
            yield return entry;

        await producer; // propagate any producer-side exception
    }
}

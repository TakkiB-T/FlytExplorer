namespace FlytExplorer.Core.Models;

public enum FileEntryKind { File, Directory, Unknown }

public sealed record FileEntry(
    string FullPath,
    string DisplayName,
    FileEntryKind Kind,
    long? SizeBytes,
    DateTimeOffset? DateModified,
    System.IO.FileAttributes Attributes,
    FileEntryOrigin Origin);

public enum FileEntryOrigin { Local, Network, EverythingIndex }

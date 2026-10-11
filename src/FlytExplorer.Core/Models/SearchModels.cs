namespace FlytExplorer.Core.Models;

public sealed record SearchRequest(
    string Text,
    string? ScopePath,
    uint Offset,
    uint Limit,
    int Generation);

public sealed record SearchPage(
    IReadOnlyList<FileEntry> Items,
    uint TotalCount,
    int Generation);

public sealed record SearchHealth(
    bool IsAvailable,
    string? Version,
    string? StatusMessage);

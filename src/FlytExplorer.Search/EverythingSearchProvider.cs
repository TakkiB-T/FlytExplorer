using System.Runtime.InteropServices;
using System.Text;
using FlytExplorer.Core.Interfaces;
using FlytExplorer.Core.Models;

namespace FlytExplorer.Search;

public sealed class EverythingSearchProvider : ISearchProvider
{
    // Everything64.dll is NOT thread-safe; serialise all calls through one dedicated thread.
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private const int MaxPathLen = 32768;

    public async Task<SearchHealth> GetHealthAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                EverythingNative.Everything_SetSearchW(string.Empty);
                EverythingNative.Everything_SetMax(0);
                EverythingNative.Everything_QueryW(true);

                var err = EverythingNative.Everything_GetLastError();
                if (err == EverythingNative.EVERYTHING_ERROR_IPC)
                    return new SearchHealth(false, null, "Everything is not running");

                var machine = EverythingNative.Everything_GetTargetMachine();
                if (machine != EverythingNative.TARGET_MACHINE_X64)
                    return new SearchHealth(false, null, $"Wrong Everything bitness (machine={machine}), need x64");

                var major = EverythingNative.Everything_GetMajorVersion();
                var minor = EverythingNative.Everything_GetMinorVersion();
                var rev   = EverythingNative.Everything_GetRevision();
                var ver   = $"{major}.{minor}.{rev}";
                return new SearchHealth(true, ver, $"Everything {ver} ready");
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<SearchPage> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => ExecuteQuery(request), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    // Public for unit testing — keeps query-building logic isolated from IPC calls.
    public static string BuildQuery(string text, string? scopePath) =>
        string.IsNullOrEmpty(scopePath)
            ? text
            : $"path:\"{scopePath}\" {text}";

    private static SearchPage ExecuteQuery(SearchRequest request)
    {
        var query = BuildQuery(request.Text, request.ScopePath);

        EverythingNative.Everything_SetSearchW(query);
        EverythingNative.Everything_SetRequestFlags(
            EverythingNative.REQUEST_FILE_NAME |
            EverythingNative.REQUEST_PATH |
            EverythingNative.REQUEST_SIZE |
            EverythingNative.REQUEST_DATE_MODIFIED |
            EverythingNative.REQUEST_ATTRIBUTES);
        EverythingNative.Everything_SetSort(EverythingNative.SORT_NAME_ASCENDING);
        EverythingNative.Everything_SetMax(request.Limit);
        EverythingNative.Everything_SetOffset(request.Offset);

        // Blocking call — must run on a non-UI thread
        bool ok = EverythingNative.Everything_QueryW(true);

        if (!ok)
        {
            var err = EverythingNative.Everything_GetLastError();
            if (err == EverythingNative.EVERYTHING_ERROR_IPC)
                throw new EverythingUnavailableException("Everything IPC is not available");
            throw new EverythingQueryException($"Everything query failed, error code {err}");
        }

        var count = EverythingNative.Everything_GetNumResults();
        var total = EverythingNative.Everything_GetTotResults();

        var sb = new StringBuilder(MaxPathLen);
        var items = new List<FileEntry>((int)count);

        for (uint i = 0; i < count; i++)
        {
            sb.Clear();
            EverythingNative.Everything_GetResultFullPathName(i, sb, (uint)MaxPathLen);
            var fullPath = sb.ToString();

            var namePtr = EverythingNative.Everything_GetResultFileName(i);
            var name = namePtr == IntPtr.Zero ? Path.GetFileName(fullPath) : Marshal.PtrToStringUni(namePtr) ?? string.Empty;

            var kind = EverythingNative.Everything_IsFolderResult(i)
                ? FileEntryKind.Directory
                : FileEntryKind.File;

            long? size = EverythingNative.Everything_GetResultSize(i, out var rawSize) ? rawSize : null;

            DateTimeOffset? modified = null;
            if (EverythingNative.Everything_GetResultDateModified(i, out var ft) && ft > 0)
                modified = DateTimeOffset.FromFileTime(ft);

            var attrs = (System.IO.FileAttributes)EverythingNative.Everything_GetResultAttributes(i);

            items.Add(new FileEntry(fullPath, name, kind, size, modified, attrs, FileEntryOrigin.EverythingIndex));
        }

        return new SearchPage(items, total, request.Generation);
    }
}

public sealed class EverythingUnavailableException(string message) : Exception(message) { }
public sealed class EverythingQueryException(string message) : Exception(message) { }

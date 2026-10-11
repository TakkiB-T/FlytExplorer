using System.Runtime.InteropServices;
using System.Text;

namespace FlytExplorer.Search;

internal static partial class EverythingNative
{
    private const string Dll = "Everything64.dll";

    internal const uint EVERYTHING_OK = 0;
    internal const uint EVERYTHING_ERROR_MEMORY = 1;
    internal const uint EVERYTHING_ERROR_IPC = 2;
    internal const uint EVERYTHING_ERROR_REGISTERCLASSEX = 3;
    internal const uint EVERYTHING_ERROR_CREATEWINDOW = 4;
    internal const uint EVERYTHING_ERROR_CREATETHREAD = 5;
    internal const uint EVERYTHING_ERROR_INVALIDINDEX = 6;
    internal const uint EVERYTHING_ERROR_INVALIDCALL = 7;
    internal const uint EVERYTHING_ERROR_INVALIDREQUEST = 8;
    internal const uint EVERYTHING_ERROR_INVALIDPARAMETER = 9;

    internal const uint REQUEST_FILE_NAME = 0x00000001;
    internal const uint REQUEST_PATH = 0x00000002;
    internal const uint REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004;
    internal const uint REQUEST_EXTENSION = 0x00000008;
    internal const uint REQUEST_SIZE = 0x00000010;
    internal const uint REQUEST_DATE_MODIFIED = 0x00000040;
    internal const uint REQUEST_ATTRIBUTES = 0x00000100;

    internal const uint SORT_NAME_ASCENDING = 1;
    internal const uint SORT_DATE_MODIFIED_DESCENDING = 14;

    internal const uint TARGET_MACHINE_X64 = 2;

    [DllImport(Dll, CharSet = CharSet.Unicode)]
    internal static extern uint Everything_SetSearchW(string lpSearchString);

    [DllImport(Dll)]
    internal static extern void Everything_SetMax(uint dwMax);

    [DllImport(Dll)]
    internal static extern void Everything_SetOffset(uint dwOffset);

    [DllImport(Dll)]
    internal static extern void Everything_SetRequestFlags(uint dwRequestFlags);

    [DllImport(Dll)]
    internal static extern void Everything_SetSort(uint dwSortType);

    [DllImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything_QueryW([MarshalAs(UnmanagedType.Bool)] bool bWait);

    [DllImport(Dll)]
    internal static extern uint Everything_GetLastError();

    [DllImport(Dll)]
    internal static extern uint Everything_GetNumResults();

    [DllImport(Dll)]
    internal static extern uint Everything_GetTotResults();

    [DllImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything_IsFolderResult(uint nIndex);

    [DllImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything_IsFileResult(uint nIndex);

    [DllImport(Dll, CharSet = CharSet.Unicode)]
    internal static extern void Everything_GetResultFullPathName(uint nIndex, StringBuilder lpString, uint nMaxCount);

    [DllImport(Dll, CharSet = CharSet.Unicode)]
    internal static extern IntPtr Everything_GetResultFileName(uint nIndex);

    [DllImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything_GetResultSize(uint nIndex, out long lpFileSize);

    [DllImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything_GetResultDateModified(uint nIndex, out long lpFileTime);

    [DllImport(Dll)]
    internal static extern uint Everything_GetResultAttributes(uint nIndex);

    [DllImport(Dll)]
    internal static extern void Everything_Reset();

    [DllImport(Dll)]
    internal static extern uint Everything_GetTargetMachine();

    [DllImport(Dll)]
    internal static extern uint Everything_GetMajorVersion();

    [DllImport(Dll)]
    internal static extern uint Everything_GetMinorVersion();

    [DllImport(Dll)]
    internal static extern uint Everything_GetRevision();
}

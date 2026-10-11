using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using FlytExplorer.Core.Interfaces;
using FlytExplorer.Core.Models;

namespace FlytExplorer.Shell;

public sealed class FileOperationService : IFileOperationService, IDisposable
{
    private readonly StaTaskScheduler _sta = new();

    public async Task<FileOperationOutcome> ExecuteAsync(
        FileOperationRequest request,
        CancellationToken cancellationToken)
    {
        return await Task.Factory.StartNew(
            () => Run(request),
            cancellationToken,
            TaskCreationOptions.None,
            _sta).ConfigureAwait(false);
    }

    private static FileOperationOutcome Run(FileOperationRequest request)
    {
        // Verify STA before any COM
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("FileOperation must run on STA thread");

        IFileOperation? op = null;
        try
        {
            op = (IFileOperation)Activator.CreateInstance(
                Type.GetTypeFromCLSID(new Guid("3ad05575-8857-4850-9277-11b85bdb8e09"))!)!;

            op.SetOperationFlags(
                FOF_ALLOWUNDO |
                FOF_NOCONFIRMMKDIR |
                FOF_NOERRORUI);

            if (request.OwnerHwnd != 0)
                op.SetOwnerWindow(request.OwnerHwnd);

            switch (request.Kind)
            {
                case FileOperationKind.Copy:
                    foreach (var src in request.SourcePaths)
                        op.CopyItem(CreateShellItem(src), CreateShellItem(request.DestinationPath!), null, IntPtr.Zero);
                    break;

                case FileOperationKind.Move:
                    foreach (var src in request.SourcePaths)
                        op.MoveItem(CreateShellItem(src), CreateShellItem(request.DestinationPath!), null, IntPtr.Zero);
                    break;

                case FileOperationKind.Rename:
                    if (request.SourcePaths.Count != 1 || request.NewName == null)
                        throw new ArgumentException("Rename requires exactly one source and a new name");
                    op.RenameItem(CreateShellItem(request.SourcePaths[0]), request.NewName, IntPtr.Zero);
                    break;

                case FileOperationKind.Delete:
                    foreach (var src in request.SourcePaths)
                        op.DeleteItem(CreateShellItem(src), IntPtr.Zero);
                    break;

                case FileOperationKind.NewFolder:
                    op.NewItem(CreateShellItem(request.DestinationPath!), 0x10 /*FILE_ATTRIBUTE_DIRECTORY*/,
                        request.NewName!, null, IntPtr.Zero);
                    break;
            }

            op.PerformOperations();
            op.GetAnyOperationsAborted(out bool aborted);

            return new FileOperationOutcome(true, aborted, null);
        }
        catch (COMException ex)
        {
            return new FileOperationOutcome(false, false, $"COM error 0x{ex.HResult:X8}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new FileOperationOutcome(false, false, ex.Message);
        }
        finally
        {
            if (op is not null)
                Marshal.ReleaseComObject(op);
        }
    }

    private static IShellItem CreateShellItem(string path)
    {
        int hr = SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItem).GUID, out var item);
        if (hr < 0) Marshal.ThrowExceptionForHR(hr); // throws COMException, caught by caller
        return item;
    }

    public void Dispose() => _sta.Dispose();

    // --- Operation flags ---
    private const uint FOF_ALLOWUNDO       = 0x0040;  // enables undo / Recycle Bin on delete
    private const uint FOF_NOCONFIRMMKDIR  = 0x0200;  // suppress mkdir confirmation dialog
    private const uint FOF_NOERRORUI       = 0x0400;  // suppress error dialogs (errors returned as HRESULT)

    // --- P/Invoke ---
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, in Guid riid, out IShellItem ppv);

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        void Advise(IntPtr pfops, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOperationFlags(uint dwOperationFlags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
        void SetProgressDialog(IntPtr popd);
        void SetProperties(IntPtr pproparray);
        void SetOwnerWindow(nint hwndOwner);
        void ApplyPropertiesToItem(IShellItem psiItem);
        void ApplyPropertiesToItems(object punkItems);
        void RenameItem(IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IntPtr pfopsItem);
        void RenameItems(object pUnkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        void MoveItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, IntPtr pfopsItem);
        void MoveItems(object punkItems, IShellItem psiDestinationFolder);
        void CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszCopyName, IntPtr pfopsItem);
        void CopyItems(object punkItems, IShellItem psiDestinationFolder);
        void DeleteItem(IShellItem psiItem, IntPtr pfopsItem);
        void DeleteItems(object punkItems);
        void NewItem(IShellItem psiDestinationFolder, uint dwFileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string pszName, [MarshalAs(UnmanagedType.LPWStr)] string? pszTemplateName, IntPtr pfopsItem);
        void PerformOperations();
        void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool pfAnyOperationsAborted);
    }
}

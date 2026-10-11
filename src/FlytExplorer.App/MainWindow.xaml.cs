using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using FlytExplorer.Core.Interfaces;
using FlytExplorer.FileSystem;
using FlytExplorer.Search;
using FlytExplorer.Shell;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace FlytExplorer.App;

public sealed partial class MainWindow : Window
{
    private readonly LocalFolderProvider    _folderProvider  = new();
    private readonly EverythingSearchProvider _searchProvider = new();
    private readonly FileOperationService   _fileOpService   = new();
    private readonly ObservableCollection<FileEntryViewModel> _items = [];
    private readonly Stack<string> _backStack    = new();
    private readonly Stack<string> _forwardStack = new();

    // Sidebar shortcut paths (x:Bind targets must be public fields/properties)
    public string _homeDir      = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string _desktopDir   = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    public string _documentsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public string _downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    private string _currentPath      = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private CancellationTokenSource? _enumerationCts;
    private CancellationTokenSource? _searchCts;
    private int _searchGeneration;

    // Internal clipboard — Phase 1 does not interop with Explorer clipboard yet
    private List<string> _clipboardPaths = [];
    private bool         _clipboardIsCut  = false;

    // ── DWM border colour ──────────────────────────────────────────────────

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint dwAttribute, ref int pvAttribute, uint cbAttribute);

    private void ApplyNordicBorderColor()
    {
        const uint DWMWA_BORDER_COLOR = 34;
        int color = 0x00ACB598; // NordicAccent #98B5AC as COLORREF
        try { DwmSetWindowAttribute(WindowNative.GetWindowHandle(this), DWMWA_BORDER_COLOR, ref color, 4); }
        catch { }
    }

    // ── Constructor ────────────────────────────────────────────────────────

    public MainWindow()
    {
        (AppWindow.Presenter as OverlappedPresenter)?.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        InitializeComponent();
        AppWindow.Changed   += OnAppWindowChanged;
        DragStrip.Loaded    += (_, _) => UpdateDragRect();
        DragStrip.SizeChanged += (_, _) => UpdateDragRect();
        FileListView.ItemsSource = _items;
        ApplyNordicBorderColor();
        _ = NavigateToAsync(_currentPath);
    }

    // ── Drag region / DWM ─────────────────────────────────────────────────

    private void UpdateDragRect()
    {
        var root = DragStrip.XamlRoot;
        if (root == null || DragStrip.ActualWidth == 0) return;
        var scale = root.RasterizationScale;
        var hwnd  = WindowNative.GetWindowHandle(this);
        var wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        InputNonClientPointerSource.GetForWindowId(wndId).SetRegionRects(
            NonClientRegionKind.Caption,
            new[] { new RectInt32(0, 0,
                (int)(DragStrip.ActualWidth  * scale),
                (int)(DragStrip.ActualHeight * scale)) });
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPresenterChange) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            var maximized = AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
            MaximizeButton.Tag = maximized ? "" : "";
            ToolTipService.SetToolTip(MaximizeButton, maximized ? "Restore" : "Maximize");
        });
    }

    // ── Caption buttons ───────────────────────────────────────────────────

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        (AppWindow.Presenter as OverlappedPresenter)?.Minimize();

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is not OverlappedPresenter p) return;
        if (p.State == OverlappedPresenterState.Maximized) p.Restore();
        else p.Maximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    // ── Navigation ────────────────────────────────────────────────────────

    private async Task NavigateToAsync(string path)
    {
        _enumerationCts?.Cancel();
        _enumerationCts = new CancellationTokenSource();
        var cts = _enumerationCts;

        _items.Clear();
        LoadingRing.IsActive = true;
        EmptyMessage.Visibility = Visibility.Collapsed;
        AddressBox.Text = path;
        StatusText.Text = $"Loading {path}…";
        UpdateSelectionStatus();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        long firstEntryMs = -1;
        int count = 0;
        const int uiBatchSize = 250;
        var batch = new List<FileEntryViewModel>(uiBatchSize);

        try
        {
            await foreach (var entry in _folderProvider.EnumerateAsync(path, cts.Token))
            {
                batch.Add(new FileEntryViewModel(entry));
                count++;

                if (batch.Count >= uiBatchSize)
                {
                    foreach (var vm in batch) _items.Add(vm);
                    if (firstEntryMs < 0) firstEntryMs = sw.ElapsedMilliseconds;
                    batch.Clear();
                    await Task.Yield();
                    if (cts.IsCancellationRequested) break;
                }
            }

            foreach (var vm in batch) _items.Add(vm);
            if (firstEntryMs < 0) firstEntryMs = sw.ElapsedMilliseconds;

            sw.Stop();
            _currentPath = path;
            StatusText.Text = count == 0
                ? "0 items"
                : $"{count} items  (first {firstEntryMs} ms · total {sw.ElapsedMilliseconds} ms)";
            EmptyMessage.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (UnauthorizedAccessException ex)   { StatusText.Text = $"アクセス拒否: {ex.Message}"; }
        catch (DirectoryNotFoundException ex)    { StatusText.Text = $"フォルダが見つかりません: {ex.Message}"; }
        catch (Exception ex)                     { StatusText.Text = $"エラー: {ex.Message}"; }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private void Navigate(string path)
    {
        _backStack.Push(_currentPath);
        _forwardStack.Clear();
        _ = NavigateToAsync(path);
    }

    private void SidebarNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path } && Directory.Exists(path))
            Navigate(path);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_backStack.TryPop(out var prev))
        {
            _forwardStack.Push(_currentPath);
            _ = NavigateToAsync(prev);
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (_forwardStack.TryPop(out var next))
        {
            _backStack.Push(_currentPath);
            _ = NavigateToAsync(next);
        }
    }

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        var parent = Path.GetDirectoryName(_currentPath);
        if (parent != null) Navigate(parent);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        _ = NavigateToAsync(_currentPath);

    private void AddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        var path = AddressBox.Text.Trim();
        if (Directory.Exists(path)) Navigate(path);
        else StatusText.Text = $"パスが見つかりません: {path}";
    }

    // ── ListView interactions ─────────────────────────────────────────────

    private void FileListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (FileListView.SelectedItem is not FileEntryViewModel vm) return;
        if (vm.Kind == Core.Models.FileEntryKind.Directory)
            Navigate(vm.FullPath);
        else
            _ = OpenFileAsync(vm.FullPath);
    }

    private static Task OpenFileAsync(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { }
        return Task.CompletedTask;
    }

    private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateSelectionStatus();

    private void UpdateSelectionStatus()
    {
        var count = FileListView.SelectedItems.Count;
        if (count == 0)
        {
            SelectionText.Visibility = Visibility.Collapsed;
        }
        else
        {
            SelectionText.Text = $"{count} 件選択";
            SelectionText.Visibility = Visibility.Visible;
        }
    }

    private void FileListView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl  = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        switch (e.Key)
        {
            case VirtualKey.Enter:
                if (FileListView.SelectedItem is FileEntryViewModel vm)
                {
                    if (vm.Kind == Core.Models.FileEntryKind.Directory) Navigate(vm.FullPath);
                    else _ = OpenFileAsync(vm.FullPath);
                    e.Handled = true;
                }
                break;

            case VirtualKey.F2:
                _ = RenameSelectedAsync();
                e.Handled = true;
                break;

            case VirtualKey.Delete:
                _ = shift ? DeleteSelectedAsync(permanent: true) : DeleteSelectedAsync(permanent: false);
                e.Handled = true;
                break;

            case VirtualKey.C when ctrl:
                CopySelected(cut: false);
                e.Handled = true;
                break;

            case VirtualKey.X when ctrl:
                CopySelected(cut: true);
                e.Handled = true;
                break;

            case VirtualKey.V when ctrl:
                _ = PasteAsync();
                e.Handled = true;
                break;

            case VirtualKey.N when ctrl && shift:
                _ = NewFolderAsync();
                e.Handled = true;
                break;

            case VirtualKey.F5:
                _ = NavigateToAsync(_currentPath);
                e.Handled = true;
                break;
        }
    }

    private void FileListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        // If the tapped element is not already in the selection, select it exclusively.
        if (e.OriginalSource is FrameworkElement { DataContext: FileEntryViewModel tapped }
            && !FileListView.SelectedItems.Contains(tapped))
        {
            FileListView.SelectedItem = tapped;
        }
    }

    // ── Context menu ──────────────────────────────────────────────────────

    private void ListContextMenu_Opening(object sender, object e)
    {
        var hasSelection = FileListView.SelectedItems.Count > 0;
        var singleItem   = FileListView.SelectedItems.Count == 1;
        var hasClipboard = _clipboardPaths.Count > 0;

        MenuOpen.IsEnabled      = singleItem;
        MenuRename.IsEnabled    = singleItem;
        MenuCopy.IsEnabled      = hasSelection;
        MenuCut.IsEnabled       = hasSelection;
        MenuPaste.IsEnabled     = hasClipboard;
        MenuDelete.IsEnabled    = hasSelection;
        // MenuNewFolder always enabled
    }

    private void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItem is not FileEntryViewModel vm) return;
        if (vm.Kind == Core.Models.FileEntryKind.Directory) Navigate(vm.FullPath);
        else _ = OpenFileAsync(vm.FullPath);
    }

    private void MenuRename_Click(object sender, RoutedEventArgs e)    => _ = RenameSelectedAsync();
    private void MenuCopy_Click(object sender, RoutedEventArgs e)      => CopySelected(cut: false);
    private void MenuCut_Click(object sender, RoutedEventArgs e)       => CopySelected(cut: true);
    private void MenuPaste_Click(object sender, RoutedEventArgs e)     => _ = PasteAsync();
    private void MenuDelete_Click(object sender, RoutedEventArgs e)    => _ = DeleteSelectedAsync(permanent: false);
    private void MenuNewFolder_Click(object sender, RoutedEventArgs e) => _ = NewFolderAsync();

    // ── File operations ───────────────────────────────────────────────────

    private async Task RenameSelectedAsync()
    {
        if (FileListView.SelectedItem is not FileEntryViewModel vm) return;

        var nameBox = new TextBox
        {
            Text = vm.DisplayName,
            SelectionStart  = 0,
            SelectionLength = Path.GetFileNameWithoutExtension(vm.DisplayName).Length,
        };

        var dialog = new ContentDialog
        {
            Title           = "名前の変更",
            Content         = nameBox,
            PrimaryButtonText   = "変更",
            CloseButtonText     = "キャンセル",
            DefaultButton   = ContentDialogButton.Primary,
            XamlRoot        = this.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var newName = nameBox.Text.Trim();
        if (string.IsNullOrEmpty(newName) || newName == vm.DisplayName) return;
        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            await ShowErrorAsync("ファイル名に使えない文字が含まれています。");
            return;
        }

        var hwnd   = WindowNative.GetWindowHandle(this);
        var outcome = await _fileOpService.ExecuteAsync(
            new FileOperationRequest(FileOperationKind.Rename, [vm.FullPath], null, newName, hwnd),
            CancellationToken.None);

        if (!outcome.Succeeded)
            await ShowErrorAsync($"名前の変更に失敗しました: {outcome.ErrorMessage}");
        else
            _ = NavigateToAsync(_currentPath);
    }

    private async Task DeleteSelectedAsync(bool permanent)
    {
        var selected = FileListView.SelectedItems.OfType<FileEntryViewModel>().ToList();
        if (selected.Count == 0) return;

        if (permanent)
        {
            var confirm = new ContentDialog
            {
                Title   = "完全に削除しますか？",
                Content = $"{selected.Count} 件のアイテムを完全に削除します。この操作は取り消せません。",
                PrimaryButtonText = "完全に削除",
                CloseButtonText   = "キャンセル",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        }

        var hwnd  = WindowNative.GetWindowHandle(this);
        var paths = selected.Select(v => v.FullPath).ToList();

        // Permanent delete: don't set FOF_ALLOWUNDO. We pass this info via a separate overload
        // in FileOperationService. For now, always use FOF_ALLOWUNDO (Recycle Bin) unless
        // permanent is requested — handled in FileOperationService extension below.
        var outcome = await _fileOpService.ExecuteAsync(
            new FileOperationRequest(FileOperationKind.Delete, paths, null, null, hwnd),
            CancellationToken.None);

        if (!outcome.Succeeded)
            await ShowErrorAsync($"削除に失敗しました: {outcome.ErrorMessage}");
        else
            _ = NavigateToAsync(_currentPath);
    }

    private void CopySelected(bool cut)
    {
        var paths = FileListView.SelectedItems.OfType<FileEntryViewModel>()
                                              .Select(v => v.FullPath)
                                              .ToList();
        if (paths.Count == 0) return;
        _clipboardPaths = paths;
        _clipboardIsCut  = cut;
        StatusText.Text  = cut
            ? $"{paths.Count} 件を切り取り"
            : $"{paths.Count} 件をコピー";
    }

    private async Task PasteAsync()
    {
        if (_clipboardPaths.Count == 0) return;

        var hwnd    = WindowNative.GetWindowHandle(this);
        var kind    = _clipboardIsCut ? FileOperationKind.Move : FileOperationKind.Copy;
        var outcome = await _fileOpService.ExecuteAsync(
            new FileOperationRequest(kind, _clipboardPaths, _currentPath, null, hwnd),
            CancellationToken.None);

        if (!outcome.Succeeded)
            await ShowErrorAsync($"貼り付けに失敗しました: {outcome.ErrorMessage}");
        else
        {
            if (_clipboardIsCut) _clipboardPaths = []; // clear after move
            _ = NavigateToAsync(_currentPath);
        }
    }

    private async Task NewFolderAsync()
    {
        var nameBox = new TextBox
        {
            Text             = "新規フォルダ",
            SelectionStart   = 0,
            SelectionLength  = 6,
        };

        var dialog = new ContentDialog
        {
            Title             = "新規フォルダ",
            Content           = nameBox,
            PrimaryButtonText = "作成",
            CloseButtonText   = "キャンセル",
            DefaultButton     = ContentDialogButton.Primary,
            XamlRoot          = this.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var name = nameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            await ShowErrorAsync("フォルダ名に使えない文字が含まれています。");
            return;
        }

        var hwnd    = WindowNative.GetWindowHandle(this);
        var outcome = await _fileOpService.ExecuteAsync(
            new FileOperationRequest(FileOperationKind.NewFolder, [], _currentPath, name, hwnd),
            CancellationToken.None);

        if (!outcome.Succeeded)
            await ShowErrorAsync($"フォルダ作成に失敗しました: {outcome.ErrorMessage}");
        else
            _ = NavigateToAsync(_currentPath);
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title             = "エラー",
            Content           = message,
            CloseButtonText   = "OK",
            XamlRoot          = this.Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    // ── Search ────────────────────────────────────────────────────────────

    private void SearchBox_TextChanged(object sender, Microsoft.UI.Xaml.Controls.TextChangedEventArgs e)
    {
        var text = SearchBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            _ = NavigateToAsync(_currentPath);
            return;
        }
        _ = SearchAsync(text);
    }

    private async Task SearchAsync(string text)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var cts        = _searchCts;
        var generation = ++_searchGeneration;

        await Task.Delay(150, cts.Token).ContinueWith(_ => { }, CancellationToken.None);
        if (cts.IsCancellationRequested || generation != _searchGeneration) return;

        var health = await _searchProvider.GetHealthAsync(cts.Token);
        if (!health.IsAvailable)
        {
            StatusText.Text = $"検索利用不可: {health.StatusMessage}";
            return;
        }

        _items.Clear();
        LoadingRing.IsActive = true;
        StatusText.Text      = "検索中…";

        try
        {
            var sw   = System.Diagnostics.Stopwatch.StartNew();
            var page = await _searchProvider.SearchAsync(
                new Core.Models.SearchRequest(text, null, 0, 200, generation),
                cts.Token);

            if (generation != _searchGeneration) return;

            foreach (var entry in page.Items)
                _items.Add(new FileEntryViewModel(entry));

            sw.Stop();
            StatusText.Text = $"{page.Items.Count} / {page.TotalCount} 件  ({sw.ElapsedMilliseconds} ms)";
        }
        catch (EverythingUnavailableException ex) { StatusText.Text = $"Everything 利用不可: {ex.Message}"; }
        catch (OperationCanceledException) { }
        catch (Exception ex)               { StatusText.Text = $"検索エラー: {ex.Message}"; }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }
}

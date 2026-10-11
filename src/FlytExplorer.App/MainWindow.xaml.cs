using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using FlytExplorer.FileSystem;
using FlytExplorer.Search;
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
    private readonly LocalFolderProvider _folderProvider = new();
    private readonly EverythingSearchProvider _searchProvider = new();
    private readonly ObservableCollection<FileEntryViewModel> _items = [];
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();

    // Sidebar shortcut paths (x:Bind targets must be public or internal fields/properties)
    public string _homeDir      = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string _desktopDir   = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    public string _documentsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public string _downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    private string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private CancellationTokenSource? _enumerationCts;
    private CancellationTokenSource? _searchCts;
    private int _searchGeneration;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint dwAttribute, ref int pvAttribute, uint cbAttribute);

    private void ApplyNordicBorderColor()
    {
        const uint DWMWA_BORDER_COLOR = 34; // Windows 11 Build 22000+
        // NordicAccent #98B5AC as COLORREF 0x00BBGGRR: R=0x98, G=0xB5, B=0xAC → 0x00ACB598
        int color = 0x00ACB598;
        try { DwmSetWindowAttribute(WindowNative.GetWindowHandle(this), DWMWA_BORDER_COLOR, ref color, 4); }
        catch { /* Graceful degradation on older OS */ }
    }

    public MainWindow()
    {
        // Remove the DWM title bar entirely so no system caption buttons are rendered at all.
        // InputNonClientPointerSource registers the DragStrip as the caption/drag region.
        (AppWindow.Presenter as OverlappedPresenter)?.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        InitializeComponent();
        AppWindow.Changed += OnAppWindowChanged;
        DragStrip.Loaded += (_, _) => UpdateDragRect();
        DragStrip.SizeChanged += (_, _) => UpdateDragRect();
        FileListView.ItemsSource = _items;
        ApplyNordicBorderColor();
        _ = NavigateToAsync(_currentPath);
    }

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
            MaximizeButton.Tag = maximized ? "\uE923" : "\uE922";
            ToolTipService.SetToolTip(MaximizeButton, maximized ? "Restore" : "Maximize");
        });
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        (AppWindow.Presenter as OverlappedPresenter)?.Minimize();

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is not OverlappedPresenter p) return;
        if (p.State == OverlappedPresenterState.Maximized) p.Restore();
        else p.Maximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

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
                    // Yield UI thread between batches so WinUI can render frames.
                    await Task.Yield();
                    if (cts.IsCancellationRequested) break;
                }
            }

            // Flush final partial batch
            foreach (var vm in batch) _items.Add(vm);
            if (firstEntryMs < 0) firstEntryMs = sw.ElapsedMilliseconds;

            sw.Stop();
            _currentPath = path;
            var totalMs = sw.ElapsedMilliseconds;
            StatusText.Text = count == 0
                ? "0 items"
                : $"{count} items  (first {firstEntryMs} ms · total {totalMs} ms)";
            EmptyMessage.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            // Navigation superseded — ignore
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusText.Text = $"Access denied: {ex.Message}";
        }
        catch (DirectoryNotFoundException ex)
        {
            StatusText.Text = $"Not found: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
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
        if (e.Key == VirtualKey.Enter)
        {
            var path = AddressBox.Text.Trim();
            if (Directory.Exists(path)) Navigate(path);
            else StatusText.Text = $"Path not found: {path}";
        }
    }

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
        var cts = _searchCts;
        var generation = ++_searchGeneration;

        await Task.Delay(150, cts.Token).ContinueWith(_ => { }, CancellationToken.None); // debounce
        if (cts.IsCancellationRequested || generation != _searchGeneration) return;

        var health = await _searchProvider.GetHealthAsync(cts.Token);
        if (!health.IsAvailable)
        {
            StatusText.Text = $"Search unavailable: {health.StatusMessage}";
            return;
        }

        _items.Clear();
        LoadingRing.IsActive = true;
        StatusText.Text = "Searching…";

        try
        {
            var page = await _searchProvider.SearchAsync(
                new Core.Models.SearchRequest(text, null, 0, 200, generation),
                cts.Token);

            if (generation != _searchGeneration) return;

            foreach (var entry in page.Items)
                _items.Add(new FileEntryViewModel(entry));

            StatusText.Text = $"{page.Items.Count} of {page.TotalCount} results";
        }
        catch (EverythingUnavailableException ex)
        {
            StatusText.Text = $"Everything unavailable: {ex.Message}";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusText.Text = $"Search error: {ex.Message}";
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }
}

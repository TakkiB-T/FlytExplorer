using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using FlytExplorer.Core.Interfaces;
using FlytExplorer.FileSystem;
using FlytExplorer.Search;
using FlytExplorer.Shell;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using WinRT.Interop;

namespace FlytExplorer.App;

public sealed partial class MainWindow : Window
{
    // ── Per-tab state ─────────────────────────────────────────────────────

    private sealed class TabData
    {
        public string           CurrentPath { get; set; }
        public ObservableCollection<FileEntryViewModel> Items { get; } = [];
        public Stack<string>    BackStack    { get; } = new();
        public Stack<string>    ForwardStack { get; } = new();
        public CancellationTokenSource? EnumerationCts { get; set; }
        public ListView         ListView     { get; set; } = null!;
        public ProgressRing     Loading      { get; set; } = null!;
        public TextBlock        Empty        { get; set; } = null!;

        public TabData(string initialPath) => CurrentPath = initialPath;

        public string Header =>
            Path.GetFileName(CurrentPath) is { Length: > 0 } n ? n : CurrentPath;
    }

    // ── Shared services ───────────────────────────────────────────────────

    private readonly LocalFolderProvider      _folderProvider = new();
    private readonly EverythingSearchProvider _searchProvider = new();
    private readonly FileOperationService     _fileOpService  = new();

    // ── Sidebar shortcut paths (x:Bind targets must be public) ───────────

    public string _homeDir      = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string _desktopDir   = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    public string _documentsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public string _downloadsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    // ── Tab state ─────────────────────────────────────────────────────────

    private readonly List<TabData> _tabs = [];
    private int _activeTabIndex;
    private TabData ActiveTab => _tabs[_activeTabIndex < _tabs.Count ? _activeTabIndex : 0];

    // ── Search state (per window, applies to active tab) ──────────────────

    private CancellationTokenSource? _searchCts;
    private int _searchGeneration;

    // ── Shared context menu (one instance, shared across all tab ListViews)

    private MenuFlyout?     _contextMenu;
    private MenuFlyoutItem? _menuOpen, _menuRename, _menuCopy, _menuCut,
                            _menuPaste, _menuDelete, _menuNewFolder, _menuProperties;

    // ── DWM border colour ─────────────────────────────────────────────────

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, uint dwAttribute, ref int pvAttribute, uint cbAttribute);

    // ── Win32 thread-local keyboard hook (Ctrl+T / Ctrl+W regardless of focus) ──

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, KeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool   UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern short GetKeyState(int nVirtKey);

    private IntPtr        _keyboardHook;
    private KeyboardProc? _keyboardHookDelegate; // keep alive: GC must not collect it

    private void InstallKeyboardHook()
    {
        _keyboardHookDelegate = KeyboardHookProc;
        _keyboardHook = SetWindowsHookEx(2 /* WH_KEYBOARD */, _keyboardHookDelegate, IntPtr.Zero, GetCurrentThreadId());
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // lParam bit 31: 1=key-up, 0=key-down. Only act on key-down.
        if (nCode >= 0 && ((int)lParam & unchecked((int)0x80000000)) == 0)
        {
            bool ctrl = (GetKeyState(0x11) & 0x8000) != 0; // VK_CONTROL
            if (ctrl)
            {
                switch (wParam.ToInt32())
                {
                    case 0x54: DispatcherQueue.TryEnqueue(AddTab);                          break; // T
                    case 0x57: DispatcherQueue.TryEnqueue(() => CloseTab(_activeTabIndex)); break; // W
                }
            }
        }
        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    // ── Empty-area right-click via PointerReleased ────────────────────────

    private void Content_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(null).Properties.PointerUpdateKind
                != Microsoft.UI.Input.PointerUpdateKind.RightButtonReleased) return;
        // Items are handled by ContextRequested; only empty-area falls through to here
        if (e.OriginalSource is FrameworkElement { DataContext: FileEntryViewModel }) return;

        var lv = GetActiveListView();
        lv.SelectedItem = null;
        var pos = e.GetCurrentPoint(lv).Position;
        try
        {
            GetOrCreateContextMenu().ShowAt(lv, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
            {
                Position = pos,
                ShowMode = Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowMode.Standard,
            });
        }
        catch { }
    }

    private void ApplyNordicBorderColor()
    {
        const uint DWMWA_BORDER_COLOR = 34;
        int color = 0x00ACB598; // NordicAccent as COLORREF (BGR)
        try { DwmSetWindowAttribute(WindowNative.GetWindowHandle(this), DWMWA_BORDER_COLOR, ref color, 4); }
        catch { }
    }

    // ── Constructor ───────────────────────────────────────────────────────

    public MainWindow()
    {
        (AppWindow.Presenter as OverlappedPresenter)?.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        InitializeComponent();
        // Ctrl+T/W are handled by the WH_KEYBOARD thread hook (installed below) so they
        // fire regardless of which element — if any — has WinUI 3 keyboard focus.
        InstallKeyboardHook();
        Closed += (_, _) =>
        {
            if (_keyboardHook != IntPtr.Zero) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; }
        };
        AppWindow.Changed     += OnAppWindowChanged;
        DragStrip.Loaded      += (_, _) => UpdateDragRect();
        DragStrip.SizeChanged += (_, _) => UpdateDragRect();
        ApplyNordicBorderColor();
        AddTab(_homeDir);
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
            [new RectInt32(0, 0,
                (int)(DragStrip.ActualWidth  * scale),
                (int)(DragStrip.ActualHeight * scale))]);
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPresenterChange) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            var maximized = AppWindow.Presenter is OverlappedPresenter
                { State: OverlappedPresenterState.Maximized };
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

    // ── Tab management ────────────────────────────────────────────────────

    private ResourceDictionary WindowResources =>
        ((FrameworkElement)this.Content).Resources;

    private void AddTab(string? path = null)
    {
        path ??= (_tabs.Count > 0 ? ActiveTab.CurrentPath : null) ?? _homeDir;

        var data = new TabData(path);

        var loadingRing = new ProgressRing
        {
            IsActive            = false,
            Width               = 48,
            Height              = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Foreground          = (SolidColorBrush)Application.Current.Resources["NordicAccentBrush"],
        };

        var emptyMsg = new TextBlock
        {
            Text                = "This folder is empty",
            Visibility          = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Foreground          = (SolidColorBrush)Application.Current.Resources["NordicTextMutedBrush"],
            FontFamily          = new FontFamily("Segoe UI Variable"),
        };

        var listView = new ListView
        {
            SelectionMode      = ListViewSelectionMode.Extended,
            FontFamily         = new FontFamily("Segoe UI Variable"),
            Foreground         = (SolidColorBrush)Application.Current.Resources["NordicTextPrimaryBrush"],
            ItemContainerStyle = (Style)WindowResources["FileListItemStyle"],
            ItemTemplate       = (DataTemplate)WindowResources["FileEntryTemplate"],
            Header             = BuildListHeader(),
            ItemsSource        = data.Items,
        };
        // Footer: transparent hit-testable area that fills the remaining empty space below items.
        // MinHeight is recalculated on layout/data changes so the footer never adds scroll space.
        var emptyFooter = new Grid { Background = new SolidColorBrush(Colors.Transparent), MinHeight = 1 };
        emptyFooter.AddHandler(UIElement.PointerReleasedEvent,
            new PointerEventHandler(Content_PointerReleased), handledEventsToo: true);
        // Clicking the blank footer returns focus to the ListView for ↑↓/Enter navigation.
        emptyFooter.Tapped += (_, _) => listView.Focus(FocusState.Programmatic);
        listView.Footer = emptyFooter;

        void UpdateFooterHeight()
        {
            if (listView.ActualHeight == 0) return;
            // Item height is fixed at 36 (FileListItemStyle Height="36").
            // Header height uses measured value once available, else 28 (12px text + 6+6 padding).
            double headerH = (listView.Header as FrameworkElement) is { ActualHeight: > 0 } hdr
                             ? hdr.ActualHeight : 28.0;
            double remaining = listView.ActualHeight - headerH - listView.Items.Count * 36.0;
            emptyFooter.MinHeight = Math.Max(1, remaining);
        }
        listView.Loaded      += (_, _) => UpdateFooterHeight();
        listView.SizeChanged += (_, _) => UpdateFooterHeight();
        data.Items.CollectionChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateFooterHeight);

        listView.DoubleTapped     += FileListView_DoubleTapped;
        listView.SelectionChanged += FileListView_SelectionChanged;
        listView.KeyDown          += FileListView_KeyDown;

        data.ListView = listView;
        data.Loading  = loadingRing;
        data.Empty    = emptyMsg;

        var content = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
        content.Children.Add(loadingRing);
        content.Children.Add(emptyMsg);
        content.Children.Add(listView);
        // ContextRequested bubbles from any hit-testable element (item OR empty area)
        // up through ListView to this Grid. handledEventsToo:true ensures we catch it
        // even if an internal element already set e.Handled = true.
        content.AddHandler(
            UIElement.ContextRequestedEvent,
            new TypedEventHandler<UIElement, ContextRequestedEventArgs>(FileListView_ContextRequested),
            handledEventsToo: true);
        // Diagnostic: catch ALL pointer releases to understand routing for empty-area right-click
        content.AddHandler(
            UIElement.PointerReleasedEvent,
            new PointerEventHandler(Content_PointerReleased),
            handledEventsToo: true);

        var tabItem = new TabViewItem
        {
            Header     = data.Header,
            Content    = content,
            IsClosable = true,
            Style      = (Style)WindowResources["FlatTabStyle"],
        };

        _tabs.Add(data);
        _activeTabIndex = _tabs.Count - 1;
        MainTabView.TabItems.Add(tabItem);
        MainTabView.SelectedIndex = _activeTabIndex;

        // First tab is not closable until a second one exists
        UpdateCloseability();
        _ = NavigateToAsync(path);
    }

    private void CloseTab(int index)
    {
        if (_tabs.Count <= 1) return;

        _tabs[index].EnumerationCts?.Cancel();
        _tabs.RemoveAt(index);
        MainTabView.TabItems.RemoveAt(index);

        _activeTabIndex = Math.Min(index, _tabs.Count - 1);
        MainTabView.SelectedIndex = _activeTabIndex;

        UpdateCloseability();
        SyncToolbarToActiveTab();
    }

    private void UpdateCloseability()
    {
        var closable = _tabs.Count > 1;
        foreach (var item in MainTabView.TabItems.OfType<TabViewItem>())
            item.IsClosable = closable;
    }

    private void SyncToolbarToActiveTab()
    {
        AddressBox.Text = ActiveTab.CurrentPath;
        UpdateSelectionStatus();
    }

    private void UpdateActiveTabHeader()
    {
        if (_activeTabIndex < MainTabView.TabItems.Count
            && MainTabView.TabItems[_activeTabIndex] is TabViewItem item)
            item.Header = ActiveTab.Header;
    }

    private void MainTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        var index = MainTabView.TabItems.IndexOf(args.Tab);
        if (index >= 0) CloseTab(index);
    }

    private void MainTabView_AddTabButtonClick(TabView sender, object args) => AddTab();

    private void MainTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var newIndex = MainTabView.SelectedIndex;
        if (newIndex < 0 || newIndex >= _tabs.Count) return;
        _activeTabIndex = newIndex;
        SyncToolbarToActiveTab();
        // Restore keyboard focus to the ListView so ↑↓/Enter navigation works immediately.
        var lv = _tabs[_activeTabIndex].ListView;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!lv.Focus(FocusState.Programmatic))
                MainTabView.Focus(FocusState.Programmatic);
        });
    }

    // ── Context menu (created once, shared across all tab ListViews) ──────

    private MenuFlyout GetOrCreateContextMenu()
    {
        if (_contextMenu != null) return _contextMenu;

        _menuOpen      = MakeMenuItem("開く",                        "", MenuOpen_Click);
        _menuRename    = MakeMenuItem("名前の変更 (F2)",              "", MenuRename_Click);
        _menuCopy      = MakeMenuItem("コピー (Ctrl+C)",              "", MenuCopy_Click);
        _menuCut       = MakeMenuItem("切り取り (Ctrl+X)",            "", MenuCut_Click);
        _menuPaste     = MakeMenuItem("貼り付け (Ctrl+V)",            "", MenuPaste_Click);
        _menuDelete    = MakeMenuItem("削除 (Del)",                   "", MenuDelete_Click);
        _menuNewFolder = MakeMenuItem("新規フォルダ (Ctrl+Shift+N)",  "", MenuNewFolder_Click);
        _menuProperties = MakeMenuItem("プロパティ",              "", MenuProperties_Click);

        var presenterStyle = new Style(typeof(MenuFlyoutPresenter));
        presenterStyle.Setters.Add(new Setter(Control.BackgroundProperty,
            new SolidColorBrush(Color.FromArgb(0xFF, 0x36, 0x36, 0x36))));
        presenterStyle.Setters.Add(new Setter(Control.BorderBrushProperty,
            new SolidColorBrush(Color.FromArgb(0xFF, 0x50, 0x50, 0x50))));
        presenterStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        presenterStyle.Setters.Add(new Setter(Control.CornerRadiusProperty,    new CornerRadius(8)));
        presenterStyle.Setters.Add(new Setter(Control.PaddingProperty,         new Thickness(4)));

        _contextMenu = new MenuFlyout { MenuFlyoutPresenterStyle = presenterStyle };
        _contextMenu.Opening += ListContextMenu_Opening;
        _contextMenu.Items.Add(_menuOpen);
        _contextMenu.Items.Add(_menuRename);
        _contextMenu.Items.Add(new MenuFlyoutSeparator());
        _contextMenu.Items.Add(_menuCopy);
        _contextMenu.Items.Add(_menuCut);
        _contextMenu.Items.Add(_menuPaste);
        _contextMenu.Items.Add(new MenuFlyoutSeparator());
        _contextMenu.Items.Add(_menuDelete);
        _contextMenu.Items.Add(new MenuFlyoutSeparator());
        _contextMenu.Items.Add(_menuNewFolder);
        _contextMenu.Items.Add(new MenuFlyoutSeparator());
        _contextMenu.Items.Add(_menuProperties);

        return _contextMenu;
    }

    private static MenuFlyoutItem MakeMenuItem(string text, string glyph, RoutedEventHandler handler)
    {
        var item = new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = glyph } };
        item.Click += handler;
        return item;
    }

    private ListView GetActiveListView() => ActiveTab.ListView;

    // ── List column header (built in code; shared per AddTab call) ────────

    private static UIElement BuildListHeader()
    {
        static SolidColorBrush Brush(string key) =>
            (SolidColorBrush)Application.Current.Resources[key];

        var header = new Grid { Padding = new Thickness(8, 6, 8, 6) };
        header.Background = Brush("NordicSurfaceBrush");
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });

        void AddHdr(string text, int col, HorizontalAlignment ha, Thickness margin)
        {
            var tb = new TextBlock
            {
                Text                = text,
                Foreground          = Brush("NordicTextMutedBrush"),
                FontSize            = 12,
                FontWeight          = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = ha,
                Margin              = margin,
            };
            Grid.SetColumn(tb, col);
            header.Children.Add(tb);
        }

        AddHdr("Name",          1, HorizontalAlignment.Left,  new Thickness(0));
        AddHdr("Size",          2, HorizontalAlignment.Right, new Thickness(0));
        AddHdr("Date modified", 3, HorizontalAlignment.Left,  new Thickness(8, 0, 0, 0));
        return header;
    }

    // ── Navigation ────────────────────────────────────────────────────────

    private async Task NavigateToAsync(string path)
    {
        var tab = ActiveTab;
        tab.EnumerationCts?.Cancel();
        tab.EnumerationCts = new CancellationTokenSource();
        var cts = tab.EnumerationCts;

        tab.Items.Clear();
        tab.Loading.IsActive = true;
        tab.Empty.Visibility = Visibility.Collapsed;
        AddressBox.Text      = path;
        StatusText.Text      = $"Loading {path}…";
        UpdateSelectionStatus();

        var sw       = System.Diagnostics.Stopwatch.StartNew();
        long firstMs = -1;
        int  count   = 0;
        const int batchSize = 250;
        var  buf     = new List<FileEntryViewModel>(batchSize);

        try
        {
            await foreach (var entry in _folderProvider.EnumerateAsync(path, cts.Token))
            {
                buf.Add(new FileEntryViewModel(entry));
                count++;

                if (buf.Count >= batchSize)
                {
                    foreach (var vm in buf) tab.Items.Add(vm);
                    if (firstMs < 0) firstMs = sw.ElapsedMilliseconds;
                    buf.Clear();
                    await Task.Yield();
                    if (cts.IsCancellationRequested) break;
                }
            }

            foreach (var vm in buf) tab.Items.Add(vm);
            if (firstMs < 0) firstMs = sw.ElapsedMilliseconds;

            sw.Stop();
            tab.CurrentPath      = path;
            AddressBox.Text      = path;
            StatusText.Text      = count == 0
                ? "0 items"
                : $"{count} items  (first {firstMs} ms · total {sw.ElapsedMilliseconds} ms)";
            tab.Empty.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateActiveTabHeader();
        }
        catch (OperationCanceledException) { }
        catch (UnauthorizedAccessException ex) { StatusText.Text = $"アクセス拒否: {ex.Message}"; }
        catch (DirectoryNotFoundException   ex) { StatusText.Text = $"フォルダが見つかりません: {ex.Message}"; }
        catch (Exception                    ex) { StatusText.Text = $"エラー: {ex.Message}"; }
        finally { tab.Loading.IsActive = false; }
    }

    private void Navigate(string path)
    {
        ActiveTab.BackStack.Push(ActiveTab.CurrentPath);
        ActiveTab.ForwardStack.Clear();
        _ = NavigateToAsync(path);
    }

    private void SidebarNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path } && Directory.Exists(path))
            Navigate(path);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab.BackStack.TryPop(out var prev))
        {
            ActiveTab.ForwardStack.Push(ActiveTab.CurrentPath);
            _ = NavigateToAsync(prev);
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab.ForwardStack.TryPop(out var next))
        {
            ActiveTab.BackStack.Push(ActiveTab.CurrentPath);
            _ = NavigateToAsync(next);
        }
    }

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        var parent = Path.GetDirectoryName(ActiveTab.CurrentPath);
        if (parent != null) Navigate(parent);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        _ = NavigateToAsync(ActiveTab.CurrentPath);

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
        var lv = (sender as ListView) ?? GetActiveListView();
        if (lv.SelectedItem is not FileEntryViewModel vm) return;
        if (vm.Kind == Core.Models.FileEntryKind.Directory) Navigate(vm.FullPath);
        else _ = OpenFileAsync(vm.FullPath);
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

    private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectionStatus();
        // Return focus to the ListView when selection is cleared so ↑↓/Enter still works.
        if (sender is ListView lv && lv.SelectedItems.Count == 0)
            lv.Focus(FocusState.Programmatic);
    }

    private void UpdateSelectionStatus()
    {
        if (_tabs.Count == 0) return;
        var count = GetActiveListView().SelectedItems.Count;
        if (count == 0)
        {
            SelectionText.Visibility = Visibility.Collapsed;
        }
        else
        {
            SelectionText.Text       = $"{count} 件選択";
            SelectionText.Visibility = Visibility.Visible;
        }
    }

    private void FileListView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl  = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        var lv = (sender as ListView) ?? GetActiveListView();

        switch (e.Key)
        {
            case VirtualKey.Enter:
                if (lv.SelectedItem is FileEntryViewModel vm)
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
                _ = NavigateToAsync(ActiveTab.CurrentPath);
                e.Handled = true;
                break;
        }
    }

    private void FileListView_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        var lv = GetActiveListView();

        // Adjust selection to right-clicked item
        if (e.TryGetPosition(lv, out _) && e.OriginalSource is FrameworkElement fe
            && fe.DataContext is FileEntryViewModel tapped
            && !lv.SelectedItems.Contains(tapped))
        {
            lv.SelectedItem = tapped;
        }

        Windows.Foundation.Point pos = default;
        e.TryGetPosition(lv, out pos);

        try
        {
            GetOrCreateContextMenu().ShowAt(lv,
                new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
                {
                    Position = pos,
                    ShowMode = Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowMode.Standard,
                });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Menu err: {ex.GetType().Name}";
            return;
        }
        e.Handled = true;
    }

    // ── Context menu ──────────────────────────────────────────────────────

    private void ListContextMenu_Opening(object? sender, object e)
    {
        var lv           = GetActiveListView();
        var hasSelection = lv.SelectedItems.Count > 0;
        var singleItem   = lv.SelectedItems.Count == 1;

        bool hasClipboard = false;
        try
        {
            var clipContent = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            hasClipboard = clipContent.Contains(
                Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems);
        }
        catch { }

        _menuOpen!.IsEnabled      = singleItem;
        _menuRename!.IsEnabled    = singleItem;
        _menuCopy!.IsEnabled      = hasSelection;
        _menuCut!.IsEnabled       = hasSelection;
        _menuPaste!.IsEnabled     = hasClipboard;
        _menuDelete!.IsEnabled    = hasSelection;
    }

    private void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        var lv = GetActiveListView();
        if (lv.SelectedItem is not FileEntryViewModel vm) return;
        if (vm.Kind == Core.Models.FileEntryKind.Directory) Navigate(vm.FullPath);
        else _ = OpenFileAsync(vm.FullPath);
    }

    private void MenuRename_Click(object sender, RoutedEventArgs e)    => _ = RenameSelectedAsync();
    private void MenuCopy_Click(object sender, RoutedEventArgs e)      => CopySelected(cut: false);
    private void MenuCut_Click(object sender, RoutedEventArgs e)       => CopySelected(cut: true);
    private void MenuPaste_Click(object sender, RoutedEventArgs e)     => _ = PasteAsync();
    private void MenuDelete_Click(object sender, RoutedEventArgs e)    => _ = DeleteSelectedAsync(permanent: false);
    private void MenuNewFolder_Click(object sender, RoutedEventArgs e)  => _ = NewFolderAsync();
    private void MenuProperties_Click(object sender, RoutedEventArgs e) => _ = ShowPropertiesAsync();

    // ── File operations ───────────────────────────────────────────────────

    private async Task RenameSelectedAsync()
    {
        var lv = GetActiveListView();
        if (lv.SelectedItem is not FileEntryViewModel vm) return;

        var nameBox = new TextBox
        {
            Text            = vm.DisplayName,
            SelectionStart  = 0,
            SelectionLength = Path.GetFileNameWithoutExtension(vm.DisplayName).Length,
        };
        var dialog = new ContentDialog
        {
            Title             = "名前の変更",
            Content           = nameBox,
            PrimaryButtonText = "変更",
            CloseButtonText   = "キャンセル",
            DefaultButton     = ContentDialogButton.Primary,
            XamlRoot          = this.Content.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var newName = nameBox.Text.Trim();
        if (string.IsNullOrEmpty(newName) || newName == vm.DisplayName) return;
        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            await ShowErrorAsync("ファイル名に使えない文字が含まれています。");
            return;
        }

        var hwnd    = WindowNative.GetWindowHandle(this);
        var outcome = await _fileOpService.ExecuteAsync(
            new FileOperationRequest(FileOperationKind.Rename, [vm.FullPath], null, newName, hwnd),
            CancellationToken.None);

        if (!outcome.Succeeded)
            await ShowErrorAsync($"名前の変更に失敗しました: {outcome.ErrorMessage}");
        else
            _ = NavigateToAsync(ActiveTab.CurrentPath);
    }

    private async Task DeleteSelectedAsync(bool permanent)
    {
        var lv       = GetActiveListView();
        var selected = lv.SelectedItems.OfType<FileEntryViewModel>().ToList();
        if (selected.Count == 0) return;

        if (permanent)
        {
            var confirm = new ContentDialog
            {
                Title             = "完全に削除しますか？",
                Content           = $"{selected.Count} 件のアイテムを完全に削除します。この操作は取り消せません。",
                PrimaryButtonText = "完全に削除",
                CloseButtonText   = "キャンセル",
                DefaultButton     = ContentDialogButton.Close,
                XamlRoot          = this.Content.XamlRoot,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        }

        var hwnd    = WindowNative.GetWindowHandle(this);
        var paths   = selected.Select(v => v.FullPath).ToList();
        var outcome = await _fileOpService.ExecuteAsync(
            new FileOperationRequest(FileOperationKind.Delete, paths, null, null, hwnd),
            CancellationToken.None);

        if (!outcome.Succeeded)
            await ShowErrorAsync($"削除に失敗しました: {outcome.ErrorMessage}");
        else
            _ = NavigateToAsync(ActiveTab.CurrentPath);
    }

    private async void CopySelected(bool cut)
    {
        var lv    = GetActiveListView();
        var vms   = lv.SelectedItems.OfType<FileEntryViewModel>().ToList();
        if (vms.Count == 0) return;

        var storageItems = new List<Windows.Storage.IStorageItem>();
        foreach (var vm in vms)
        {
            try
            {
                Windows.Storage.IStorageItem item = vm.Kind == Core.Models.FileEntryKind.Directory
                    ? await Windows.Storage.StorageFolder.GetFolderFromPathAsync(vm.FullPath)
                    : await Windows.Storage.StorageFile.GetFileFromPathAsync(vm.FullPath);
                storageItems.Add(item);
            }
            catch { /* skip inaccessible items */ }
        }
        if (storageItems.Count == 0) return;

        var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dp.RequestedOperation = cut
            ? Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move
            : Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        dp.SetStorageItems(storageItems);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        StatusText.Text = cut ? $"{storageItems.Count} 件を切り取り" : $"{storageItems.Count} 件をコピー";
    }


    private async Task PasteAsync()
    {
        var clipContent = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (!clipContent.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            return;

        IReadOnlyList<Windows.Storage.IStorageItem> storageItems;
        try { storageItems = await clipContent.GetStorageItemsAsync(); }
        catch { return; }

        var paths = storageItems
            .Select(i => i.Path)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
        if (paths.Count == 0) return;

        var isCut = clipContent.RequestedOperation.HasFlag(
            Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move);
        var kind = isCut ? FileOperationKind.Move : FileOperationKind.Copy;

        var hwnd    = WindowNative.GetWindowHandle(this);
        var outcome = await _fileOpService.ExecuteAsync(
            new FileOperationRequest(kind, paths, ActiveTab.CurrentPath, null, hwnd),
            CancellationToken.None);

        if (!outcome.Succeeded)
            await ShowErrorAsync($"貼り付けに失敗しました: {outcome.ErrorMessage}");
        else
            _ = NavigateToAsync(ActiveTab.CurrentPath);
    }


    private async Task NewFolderAsync()
    {
        var nameBox = new TextBox
        {
            Text            = "新規フォルダ",
            SelectionStart  = 0,
            SelectionLength = 6,
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
            new FileOperationRequest(FileOperationKind.NewFolder, [], ActiveTab.CurrentPath, name, hwnd),
            CancellationToken.None);

        if (!outcome.Succeeded)
            await ShowErrorAsync($"フォルダ作成に失敗しました: {outcome.ErrorMessage}");
        else
            _ = NavigateToAsync(ActiveTab.CurrentPath);
    }

    private async Task ShowPropertiesAsync()
    {
        var lv = GetActiveListView();
        if (lv.SelectedItem is not FileEntryViewModel vm) return;

        var info = new FileInfo(vm.FullPath);
        var isDir = vm.Kind == Core.Models.FileEntryKind.Directory;
        string sizeText;
        if (isDir)
        {
            sizeText = "—";
        }
        else
        {
            sizeText = info.Exists
                ? info.Length >= 1_048_576
                    ? $"{info.Length / 1_048_576.0:F1} MB  ({info.Length:N0} bytes)"
                    : info.Length >= 1024
                        ? $"{info.Length / 1024.0:F1} KB  ({info.Length:N0} bytes)"
                        : $"{info.Length:N0} bytes"
                : "—";
        }

        var panel = new StackPanel { Spacing = 6, MinWidth = 340 };
        void AddRow(string label, string value)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lbl = new TextBlock { Text = label, Foreground = (Brush)Application.Current.Resources["NordicTextMutedBrush"] };
            var val = new TextBlock { Text = value,  TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(val, 1);
            row.Children.Add(lbl);
            row.Children.Add(val);
            panel.Children.Add(row);
        }

        AddRow("名前",          vm.DisplayName);
        AddRow("場所",          Path.GetDirectoryName(vm.FullPath) ?? "");
        AddRow("種類",          isDir ? "フォルダ" : (Path.GetExtension(vm.FullPath).TrimStart('.').ToUpperInvariant() + " ファイル"));
        AddRow("サイズ",        sizeText);
        AddRow("作成日時",      (info.Exists ? info.CreationTime.ToString("yyyy/MM/dd HH:mm:ss") : "—"));
        AddRow("更新日時",      (info.Exists ? info.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss") : "—"));
        AddRow("アクセス日時",  (info.Exists ? info.LastAccessTime.ToString("yyyy/MM/dd HH:mm:ss") : "—"));
        if (!isDir && info.Exists)
            AddRow("属性", info.Attributes.ToString());

        var dialog = new ContentDialog
        {
            Title           = "プロパティ",
            Content         = panel,
            CloseButtonText = "閉じる",
            XamlRoot        = this.Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title           = "エラー",
            Content         = message,
            CloseButtonText = "OK",
            XamlRoot        = this.Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    // ── Search ────────────────────────────────────────────────────────────

    private void SearchBox_TextChanged(object sender, Microsoft.UI.Xaml.Controls.TextChangedEventArgs e)
    {
        var text = SearchBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            _ = NavigateToAsync(ActiveTab.CurrentPath);
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

        var tab = ActiveTab;
        tab.Items.Clear();
        tab.Loading.IsActive = true;
        StatusText.Text      = "検索中…";

        try
        {
            var sw   = System.Diagnostics.Stopwatch.StartNew();
            var page = await _searchProvider.SearchAsync(
                new Core.Models.SearchRequest(text, null, 0, 200, generation),
                cts.Token);

            if (generation != _searchGeneration) return;

            foreach (var entry in page.Items)
                tab.Items.Add(new FileEntryViewModel(entry));

            sw.Stop();
            StatusText.Text = $"{page.Items.Count} / {page.TotalCount} 件  ({sw.ElapsedMilliseconds} ms)";
        }
        catch (EverythingUnavailableException ex) { StatusText.Text = $"Everything 利用不可: {ex.Message}"; }
        catch (OperationCanceledException) { }
        catch (Exception ex)               { StatusText.Text = $"検索エラー: {ex.Message}"; }
        finally { tab.Loading.IsActive = false; }
    }
}

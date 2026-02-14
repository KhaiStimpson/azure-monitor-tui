using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

using AzureMonitorTui.Monitors;

namespace AzureMonitorTui.Ui;

/// <summary>
/// Expandable category node in the catalog tree (e.g. "Queues").
/// </summary>
public sealed class CategoryNode : TreeNode
{
    private string _text;

    public CategoryNode(string name, IReadOnlyList<CatalogItemNode> items)
    {
        _text = $"\uf1b3 {name}";

        foreach (var item in items)
        {
            Children.Add(item);
        }
    }

    public override string Text
    {
        get => _text;
        set => _text = value;
    }
}

/// <summary>
/// Loading placeholder node with animated spinner rune.
/// </summary>
public sealed class LoadingNode : TreeNode
{
    private static readonly char[] SpinnerFrames = new[] { '⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏' };
    private int _frameIndex;

    public void AdvanceFrame()
    {
        _frameIndex = (_frameIndex + 1) % SpinnerFrames.Length;
    }

    public override string Text
    {
        get => $"{SpinnerFrames[_frameIndex]}  Loading queues...";
        set { /* Text is computed from spinner frame */ }
    }
}

/// <summary>
/// Leaf node representing a single monitorable item with a checkbox toggle.
/// </summary>
public sealed class CatalogItemNode : TreeNode
{
    public bool IsEnabled { get; set; }

    public CatalogItem CatalogItem { get; }

    public CatalogItemNode(CatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        CatalogItem = item;
    }

    public override string Text
    {
        get => $"{(IsEnabled ? "\uf14a" : "\uf096")} {CatalogItem.Name}";
        set { /* Text is computed from IsEnabled + Name */ }
    }
}

/// <summary>
/// Event args for when a monitor item is toggled on or off in the catalog.
/// </summary>
public sealed class MonitorToggledEventArgs : EventArgs
{
    public CatalogItem Item { get; }

    public bool Enabled { get; }

    public MonitorToggledEventArgs(CatalogItem item, bool enabled)
    {
        Item = item;
        Enabled = enabled;
    }
}

/// <summary>
/// Left-pane tree view showing catalog categories with checkbox-togglable items.
/// Supports deferred loading with an animated spinner.
/// </summary>
public sealed class CatalogTreeView : TreeView, IDisposable
{
    private readonly IApplication _app;
    private readonly ErrorHandler? _errorHandler;
    private LoadingNode? _loadingNode;
    private object? _spinnerTimerToken;
    private bool _isLoading = true;
    private bool _disposed;

    public event EventHandler<MonitorToggledEventArgs>? MonitorToggled;

    public CatalogTreeView(IApplication app, ErrorHandler? errorHandler = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        _app = app;
        _errorHandler = errorHandler;

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        StartLoadingAnimation();

        ObjectActivated += OnObjectActivated;
    }

    /// <summary>
    /// Starts the loading spinner animation.
    /// </summary>
    private void StartLoadingAnimation()
    {
        _loadingNode = new LoadingNode();
        AddObject(_loadingNode);

        var interval = TimeSpan.FromMilliseconds(120);
        _spinnerTimerToken = _app.AddTimeout(interval, AdvanceSpinner);
    }

    /// <summary>
    /// Timer callback that advances the spinner frame.
    /// </summary>
    private bool AdvanceSpinner()
    {
        if (_disposed || !_isLoading)
        {
            return false;
        }

        _loadingNode?.AdvanceFrame();
        _app.Invoke(() => SetNeedsDraw());

        return true; // Continue timer
    }

    /// <summary>
    /// Loads catalog items asynchronously and replaces the loading placeholder.
    /// Call this after the TUI is initialized.
    /// </summary>
    public async Task LoadCatalogAsync(ICatalog catalog, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        try
        {
            var items = await Task.Run(() => catalog.GetAvailable().ToList(), ct);

            _app.Invoke(() =>
            {
                if (_disposed)
                {
                    return;
                }

                StopLoadingAnimation();

                var itemNodes = items
                    .Select(i => new CatalogItemNode(i))
                    .ToList();

                var categoryNode = new CategoryNode("Queues", itemNodes);
                AddObject(categoryNode);

                // Expand the category by default so items are visible
                Expand(categoryNode);

                SetNeedsDraw();
            });
        }
        catch (Exception ex)
        {
            _app.Invoke(() =>
            {
                if (_disposed)
                {
                    return;
                }

                StopLoadingAnimation();

                // Try to show error via handler; if no handler or debug disabled, show simple message
                if (_errorHandler?.Handle(ex, "Loading queue catalog") != true)
                {
                    var errorNode = new TreeNode("\uf071  Failed to load queues");
                    AddObject(errorNode);
                }

                SetNeedsDraw();
            });
        }
    }

    /// <summary>
    /// Stops the loading animation and removes the loading node.
    /// </summary>
    private void StopLoadingAnimation()
    {
        _isLoading = false;

        if (_spinnerTimerToken is not null)
        {
            _app.RemoveTimeout(_spinnerTimerToken);
            _spinnerTimerToken = null;
        }

        if (_loadingNode is not null)
        {
            ClearObjects();
            _loadingNode = null;
        }
    }

    private void OnObjectActivated(object? sender, ObjectActivatedEventArgs<ITreeNode> e)
    {
        // Ignore activation while loading
        if (_isLoading)
        {
            return;
        }

        if (e.ActivatedObject is CatalogItemNode itemNode)
        {
            ToggleMonitor(itemNode);
        }
    }

    protected override bool OnKeyDown(Key keyEvent)
    {
        // Handle Space key to toggle
        if (!_isLoading && keyEvent == Key.Space)
        {
            if (SelectedObject is CatalogItemNode itemNode)
            {
                ToggleMonitor(itemNode);
                return true;
            }
        }

        return base.OnKeyDown(keyEvent);
    }

    /// <summary>
    /// Toggles a monitor item and raises the MonitorToggled event.
    /// </summary>
    private void ToggleMonitor(CatalogItemNode itemNode)
    {
        itemNode.IsEnabled = !itemNode.IsEnabled;
        SetNeedsDraw();

        MonitorToggled?.Invoke(this, new MonitorToggledEventArgs(
            itemNode.CatalogItem,
            itemNode.IsEnabled));
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;

            if (disposing)
            {
                StopLoadingAnimation();
            }
        }

        base.Dispose(disposing);
    }
}

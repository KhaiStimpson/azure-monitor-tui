using System.Drawing;

using Terminal.Gui.App;
using Terminal.Gui.ViewBase;

using AzureMonitorTui.Monitors;

namespace AzureMonitorTui.Ui;

/// <summary>
/// Right-pane container that arranges <see cref="QueueMonitorCard"/> instances
/// in a responsive grid (1 or 2 columns based on width) with automatic reflow on add/remove.
/// </summary>
public sealed class MonitorDashboard : View
{
    private const int CardHeight = 14;
    private const int TwoColumnMinWidth = 100;

    private readonly IApplication _app;
    private readonly MonitorSettings _settings;
    private readonly Dictionary<string, QueueMonitorCard> _cards = new(StringComparer.OrdinalIgnoreCase);

    public MonitorDashboard(IApplication app, MonitorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(settings);

        _app = app;
        _settings = settings;

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;
    }

    /// <summary>
    /// Adds a monitor card to the dashboard and reflows the grid layout.
    /// </summary>
    public void AddMonitor(string name, IMonitor<int> monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);

        if (_cards.ContainsKey(name))
        {
            return;
        }

        var card = new QueueMonitorCard(_app, name, monitor, _settings);
        _cards[name] = card;
        Add(card);

        // Ensure the card is fully initialized when added to an already-initialized
        // parent, so that its Initialized event fires and polling starts.
        if (IsInitialized && !card.IsInitialized)
        {
            card.BeginInit();
            card.EndInit();
        }

        ReflowLayout();
    }

    /// <summary>
    /// Removes a monitor card from the dashboard and reflows the grid layout.
    /// </summary>
    public void RemoveMonitor(string name)
    {
        if (!_cards.Remove(name, out var card))
        {
            return;
        }

        Remove(card);
        card.Dispose();
        ReflowLayout();
    }

    /// <summary>
    /// Positions all cards in a responsive grid (1 or 2 columns based on width).
    /// Uses 1 column when viewport width is less than 100 characters, 2 columns otherwise.
    /// </summary>
    private void ReflowLayout()
    {
        // Determine column count based on viewport width
        var viewportWidth = Viewport.Size.Width;
        var columns = viewportWidth < TwoColumnMinWidth ? 1 : 2;

        var index = 0;

        foreach (var card in _cards.Values)
        {
            var col = index % columns;
            var row = index / columns;

            card.X = col == 0 ? 0 : Pos.Percent(50);
            card.Y = row * CardHeight;
            card.Width = columns == 1 ? Dim.Fill() : Dim.Percent(50);
            card.Height = CardHeight;

            index++;
        }

        UpdateContentSize();
        SetNeedsLayout();
        SetNeedsDraw();
    }

    /// <summary>
    /// Updates the content size to match the current viewport width and total card height.
    /// Called after layout so that <see cref="View.Viewport"/> dimensions are available.
    /// </summary>
    private void UpdateContentSize()
    {
        if (_cards.Count == 0)
        {
            return;
        }

        // Determine column count based on viewport width
        var viewportWidth = Viewport.Size.Width;
        var columns = viewportWidth < TwoColumnMinWidth ? 1 : 2;

        var totalRows = (_cards.Count + columns - 1) / columns;
        var contentHeight = totalRows * CardHeight;
        var width = viewportWidth;

        if (width <= 0)
        {
            width = 1;
        }

        SetContentSize(new Size(width, contentHeight));
    }

    /// <inheritdoc/>
    protected override void OnSubViewsLaidOut(LayoutEventArgs args)
    {
        base.OnSubViewsLaidOut(args);
        UpdateContentSize();
    }
}

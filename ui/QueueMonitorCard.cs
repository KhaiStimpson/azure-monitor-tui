using System.Drawing;
using System.Text;

using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

using AzureMonitorTui.Monitors;

using TgColor = Terminal.Gui.Drawing.Color;
using TgAttribute = Terminal.Gui.Drawing.Attribute;

namespace AzureMonitorTui.Ui;

/// <summary>
/// Custom annotation that draws connected S-shaped step lines with rounded corners.
/// For each value transition, the line goes: horizontal to midpoint X, then vertical
/// to the new Y, then horizontal to the end X. This creates a smooth S-curve with
/// two rounded corners per transition.
/// </summary>
internal sealed class ConnectedLineAnnotation : IAnnotation
{
    // Box-drawing characters for line segments
    private static readonly Rune H = new('─');
    private static readonly Rune V = new('│');

    // Rounded corner glyphs - named by which two directions they connect
    // ╭ connects → and ↓  (right and down)
    // ╮ connects ← and ↓  (left and down)
    // ╰ connects → and ↑  (right and up)
    // ╯ connects ← and ↑  (left and up)
    private static readonly Rune RightDown = new('╭');
    private static readonly Rune LeftDown = new('╮');
    private static readonly Rune RightUp = new('╰');
    private static readonly Rune LeftUp = new('╯');

    public List<PointF> Points { get; set; } = new();
    public TgAttribute LineAttribute { get; set; }
    public bool BeforeSeries => false;

    public void Render(GraphView graph)
    {
        if (Points.Count < 2)
        {
            return;
        }

        graph.SetAttribute(LineAttribute);

        for (var i = 0; i < Points.Count - 1; i++)
        {
            var s1 = graph.GraphSpaceToViewport(Points[i]);
            var s2 = graph.GraphSpaceToViewport(Points[i + 1]);

            DrawSStep(graph, s1, s2);
        }
    }

    /// <summary>
    /// Draws an S-shaped step between two points:
    ///   start ──── c1
    ///              │
    ///              c2 ──── end
    /// Using rounded corners at c1 and c2 for a smooth appearance.
    /// </summary>
    private void DrawSStep(GraphView graph, Point start, Point end)
    {
        // Same point
        if (start == end)
        {
            graph.AddRune(start.X, start.Y, H);
            return;
        }

        // Pure horizontal
        if (start.Y == end.Y)
        {
            graph.DrawLine(start, end, H);
            return;
        }

        // Pure vertical (unlikely for time-series but handle it)
        if (start.X == end.X)
        {
            graph.DrawLine(start, end, V);
            return;
        }

        // Calculate midpoint X for the S-curve
        var midX = (start.X + end.X) / 2;

        // If the points are too close horizontally for an S-curve, fall back to L-step
        if (Math.Abs(end.X - start.X) < 3)
        {
            DrawLStep(graph, start, end);
            return;
        }

        // Two corner points
        var c1 = new Point(midX, start.Y);
        var c2 = new Point(midX, end.Y);

        // Determine directions in screen coords (Y increases downward)
        var goingRight = end.X > start.X;
        var goingDown = end.Y > start.Y;

        // Segment 1: horizontal from start to c1
        graph.DrawLine(start, c1, H);

        // Segment 2: vertical from c1 to c2
        graph.DrawLine(c1, c2, V);

        // Segment 3: horizontal from c2 to end
        graph.DrawLine(c2, end, H);

        // Corner 1 at c1: horizontal arrives, vertical departs
        //   going right + down: line from left, goes down → ╮
        //   going right + up:   line from left, goes up   → ╯
        //   going left  + down: line from right, goes down → ╭
        //   going left  + up:   line from right, goes up   → ╰
        var c1Rune = (goingRight, goingDown) switch
        {
            (true, true) => LeftDown,   // ╮
            (true, false) => LeftUp,    // ╯
            (false, true) => RightDown, // ╭
            (false, false) => RightUp,  // ╰
        };
        graph.AddRune(c1.X, c1.Y, c1Rune);

        // Corner 2 at c2: vertical arrives, horizontal departs
        //   going right + down: line from above, goes right → ╰
        //   going right + up:   line from below, goes right → ╭
        //   going left  + down: line from above, goes left  → ╯
        //   going left  + up:   line from below, goes left  → ╮
        var c2Rune = (goingRight, goingDown) switch
        {
            (true, true) => RightUp,    // ╰
            (true, false) => RightDown, // ╭
            (false, true) => LeftUp,    // ╯
            (false, false) => LeftDown, // ╮
        };
        graph.AddRune(c2.X, c2.Y, c2Rune);
    }

    /// <summary>
    /// Fallback L-shaped step for points too close horizontally for an S-curve.
    /// </summary>
    private void DrawLStep(GraphView graph, Point start, Point end)
    {
        var corner = new Point(end.X, start.Y);
        var goingRight = end.X > start.X;
        var goingDown = end.Y > start.Y;

        graph.DrawLine(start, corner, H);
        graph.DrawLine(corner, end, V);

        var cornerRune = (goingRight, goingDown) switch
        {
            (true, true) => LeftDown,   // ╮
            (true, false) => LeftUp,    // ╯
            (false, true) => RightDown, // ╭
            (false, false) => RightUp,  // ╰
        };
        graph.AddRune(corner.X, corner.Y, cornerRune);
    }
}

/// <summary>
/// A framed card displaying a live-updating line chart for a single queue monitor.
/// Polls the monitor on a timer and renders the message count history as a graph.
/// </summary>
public sealed class QueueMonitorCard : FrameView, IDisposable
{
    private readonly IApplication _app;
    private readonly IMonitor<int> _monitor;
    private readonly MonitorSettings _settings;
    private readonly List<(DateTime timestamp, double value)> _dataPoints = new();
    private readonly GraphView _graph;
    private readonly Label _countLabel;
    private readonly ScatterSeries _series;
    private readonly ConnectedLineAnnotation _line;
    private object? _timerToken;
    private bool _disposed;

    public QueueMonitorCard(
        IApplication app,
        string queueName,
        IMonitor<int> monitor,
        MonitorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentNullException.ThrowIfNull(settings);

        _app = app;
        _monitor = monitor;
        _settings = settings;

        Title = $"\uf201 {queueName}";
        BorderStyle = LineStyle.Rounded;

        _countLabel = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Text = "Count: --"
        };

        _series = new ScatterSeries();
        _series.Fill = new GraphCellToRender(new Rune('\u25CF'));

        _line = new ConnectedLineAnnotation
        {
            LineAttribute = ThemeLoader.GetGraphLineColor()
        };

        _graph = new GraphView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CellSize = new PointF(1, 1),
            MarginLeft = 6,
            MarginBottom = 1
        };

        _graph.AxisX.Increment = 10;
        _graph.AxisX.ShowLabelsEvery = 1;
        _graph.AxisX.Visible = true;

        _graph.AxisY.Increment = 1;
        _graph.AxisY.ShowLabelsEvery = 1;
        _graph.AxisY.Visible = true;

        _graph.Annotations.Add(_line);
        _graph.Series.Add(_series);

        Add(_countLabel, _graph);

        // Start polling after the view is initialized.
        // If added to an already-initialized parent, Initialized may fire
        // immediately during Add() or may not fire at all, so we also check
        // IsInitialized and start polling directly if needed.
        Initialized += OnInitialized;

        // Subscribe to theme changes to update graph colors
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        // Update graph colors when theme changes
        _line.LineAttribute = ThemeLoader.GetGraphLineColor();
        _graph.SetNeedsDraw();
    }

    private void OnInitialized(object? sender, EventArgs e)
    {
        if (_timerToken is null)
        {
            StartPolling();
        }
    }

    private void StartPolling()
    {
        var interval = TimeSpan.FromSeconds(_settings.PollIntervalSeconds);

        _timerToken = _app.AddTimeout(interval, PollMonitor);

        // Do an immediate first poll
        _ = Task.Run(async () =>
        {
            await PollAsync();
        });
    }

    private bool PollMonitor()
    {
        if (_disposed)
        {
            return false;
        }

        _ = Task.Run(async () =>
        {
            await PollAsync();
        });

        return true; // Continue timer
    }

    private async Task PollAsync()
    {
        try
        {
            var count = await _monitor.Out();
            _app.Invoke(() => UpdateChart(count));
        }
        catch (Exception)
        {
            // Silently skip failed polls; the chart just won't update
        }
    }

    private void UpdateChart(int count)
    {
        if (_disposed)
        {
            return;
        }

        var now = DateTime.Now;
        _dataPoints.Add((now, count));

        // Trim to max data points
        while (_dataPoints.Count > _settings.MaxDataPoints)
        {
            _dataPoints.RemoveAt(0);
        }

        if (_dataPoints.Count == 0)
        {
            return;
        }

        // Determine time range for the visible window
        var graphWidth = (int)Math.Max(1, _graph.Frame.Width - _graph.MarginLeft);
        var visibleCount = Math.Min(_dataPoints.Count, graphWidth);
        var startIndex = _dataPoints.Count - visibleCount;

        var firstTimestamp = _dataPoints[startIndex].timestamp;
        var lastTimestamp = _dataPoints[^1].timestamp;
        var timeSpanSeconds = (lastTimestamp - firstTimestamp).TotalSeconds;

        // For single point or very small time ranges, use a minimum span for display
        var displayTimeSpan = Math.Max(timeSpanSeconds, 60.0); // At least 60 seconds range

        // Build point lists for the series and line, using seconds since first timestamp as X
        var seriesPoints = new List<PointF>();
        var linePoints = new List<PointF>();

        for (var i = startIndex; i < _dataPoints.Count; i++)
        {
            var secondsSinceStart = (float)(_dataPoints[i].timestamp - firstTimestamp).TotalSeconds;
            var point = new PointF(secondsSinceStart, (float)_dataPoints[i].value);
            seriesPoints.Add(point);
            linePoints.Add(point);
        }

        _series.Points = seriesPoints;
        _line.Points = linePoints;

        // Auto-scale Y axis with proper bounds (integer-aligned for queue counts)
        var visibleValues = _dataPoints.Skip(startIndex).Select(p => p.value).ToList();
        var minVal = visibleValues.Min();
        var maxVal = visibleValues.Max();

        // Floor the min value to the nearest integer, ceiling the max
        var dataYMin = Math.Floor(Math.Max(0, minVal));
        var dataYMax = Math.Ceiling(maxVal);

        // Ensure we have at least a range of 2 for visibility
        if (dataYMax - dataYMin < 2)
        {
            dataYMax = dataYMin + 2;
        }

        // Add integer padding for better visibility
        dataYMin = Math.Max(0, dataYMin - 1);
        dataYMax = dataYMax + 1;

        // Calculate a nice increment (try to get ~5 labels)
        var dataRange = dataYMax - dataYMin;
        var yIncrement = Math.Max(1, (int)Math.Ceiling(dataRange / 5.0));

        // Round yMin down to the nearest multiple of yIncrement for even spacing
        var yMin = Math.Floor(dataYMin / yIncrement) * yIncrement;
        var yMax = Math.Ceiling(dataYMax / yIncrement) * yIncrement;

        // Calculate cell size so the graph fits the viewport
        var graphHeight = (int)Math.Max(1, _graph.Frame.Height - _graph.MarginBottom);

        // X axis: use display time span for consistent scaling
        var xCellSize = (float)(displayTimeSpan / Math.Max(1, graphWidth));
        var yCellSize = (float)((yMax - yMin) / Math.Max(1, graphHeight));
        _graph.CellSize = new PointF(xCellSize, yCellSize);

        // Set ScrollOffset to position the graph so the bottom-left corner shows (0, yMin)
        // This ensures the data range is visible in the viewport
        _graph.ScrollOffset = new PointF(0, (float)yMin);

        // Configure Y axis with integer increments
        _graph.AxisY.Increment = yIncrement;
        _graph.AxisY.ShowLabelsEvery = 1;

        // Configure X axis to show time labels
        // Show increment every ~10 seconds or so
        var xIncrement = Math.Max(5, (float)Math.Ceiling(displayTimeSpan / 10));
        _graph.AxisX.Increment = xIncrement;
        _graph.AxisX.ShowLabelsEvery = 1;

        // Custom label formatter for time on X axis
        _graph.AxisX.LabelGetter = (axisIncrement) =>
        {
            var timestamp = firstTimestamp.AddSeconds(axisIncrement.Value);
            return timestamp.ToString("HH:mm:ss");
        };

        _countLabel.Text = $"\uf292  {count:N0}";

        _graph.SetNeedsDraw();
        _countLabel.SetNeedsDraw();
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;

            if (disposing)
            {
                if (_timerToken is not null)
                {
                    _app.RemoveTimeout(_timerToken);
                    _timerToken = null;
                }

                // Unsubscribe from theme changes
                ThemeManager.ThemeChanged -= OnThemeChanged;
            }
        }

        base.Dispose(disposing);
    }
}

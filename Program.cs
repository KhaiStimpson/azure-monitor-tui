using Azure.Storage.Queues;

using Microsoft.Extensions.Configuration;

using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

using AzureMonitorTui;
using AzureMonitorTui.Monitors;
using AzureMonitorTui.Ui;

// ── Configuration ────────────────────────────────────────────────────────────

var configurationBuilder = new ConfigurationBuilder()
    .SetBasePath(ConfigPaths.BaseConfigDirectory)
    .AddJsonFile(ConfigPaths.GetLocalPath("appsettings.json"), optional: false, reloadOnChange: false);

// In Release builds, layer user config on top of bundled defaults
if (ConfigPaths.UseUserOverrides)
{
    configurationBuilder.AddJsonFile(
        ConfigPaths.GetUserConfigPath("config.json"),
        optional: true,
        reloadOnChange: false);
}

var configuration = configurationBuilder.Build();

var azureConfig = new AzureStorageConfig();
configuration.GetSection("AzureStorage").Bind(azureConfig);

var monitorSettings = new MonitorSettings();
configuration.GetSection("Monitor").Bind(monitorSettings);

if (string.IsNullOrWhiteSpace(azureConfig.ConnectionString))
{
    Console.Error.WriteLine("Error: AzureStorage:ConnectionString is not configured.");
    return 1;
}

Console.WriteLine("Azure Monitor TUI - Starting...");
Console.WriteLine();

// Track active monitors for cleanup
var activeMonitors = new Dictionary<string, IMonitor<int>>(StringComparer.OrdinalIgnoreCase);

// ── Terminal.Gui application ─────────────────────────────────────────────────

// Load theme configuration
ThemeLoader.Load();

using IApplication app = Application.Create();
app.Init();

// Create error handler
var errorHandler = new ErrorHandler(app, monitorSettings);

using var window = new Window
{
    Title = "Azure Monitor TUI",
    BorderStyle = LineStyle.None
};

// Header with app title and separator
var header = new Label
{
    X = 1,
    Y = 0,
    Width = Dim.Fill(),
    Height = 1,
    Text = "\uf7d9  Azure Monitor TUI"
};

var headerSeparator = new Line
{
    X = 0,
    Y = 1,
    Width = Dim.Fill(),
    Orientation = Orientation.Horizontal
};

// Left pane: catalog tree
var leftPane = new FrameView
{
    Title = "Catalogs",
    X = 0,
    Y = 2,
    Width = Dim.Percent(25),
    Height = Dim.Fill(1),
    BorderStyle = LineStyle.Rounded
};

var catalogTree = new CatalogTreeView(app, errorHandler);
leftPane.Add(catalogTree);

// Right pane: monitor dashboard
var rightPane = new FrameView
{
    Title = "",
    X = Pos.Right(leftPane),
    Y = 2,
    Width = Dim.Fill(),
    Height = Dim.Fill(1),
    BorderStyle = LineStyle.None
};

var dashboard = new MonitorDashboard(app, monitorSettings);
rightPane.Add(dashboard);

// ── Event wiring ─────────────────────────────────────────────────────────────

// Declare QueueServiceClient and catalog here so they're available to event handlers
QueueServiceClient? queueServiceClient = null;
ICatalog? catalog = null;

catalogTree.MonitorToggled += (_, e) =>
{
    if (queueServiceClient is null)
    {
        errorHandler.Handle(new InvalidOperationException("Azure connection not ready"), "Monitor toggle");
        return;
    }

    var name = e.Item.Name;

    if (e.Enabled)
    {
        try
        {
            // Create and start a new monitor
            var config = new StorageAccountConfig { Name = name };
            var monitor = new StorageMonitor(config, queueServiceClient);

            if (activeMonitors.TryAdd(name, monitor))
            {
                dashboard.AddMonitor(name, monitor);
            }
        }
        catch (Exception ex)
        {
            errorHandler.Handle(ex, $"Starting monitor for '{name}'");
        }
    }
    else
    {
        try
        {
            // Remove and dispose the monitor
            dashboard.RemoveMonitor(name);

            if (activeMonitors.Remove(name, out var monitor))
            {
                monitor.Dispose();
            }
        }
        catch (Exception ex)
        {
            errorHandler.Handle(ex, $"Stopping monitor for '{name}'");
        }
    }
};

// ── Status bar ───────────────────────────────────────────────────────────────

var statusBar = new StatusBar
{
    Y = Pos.Bottom(window) - 1,
};

var quitShortcut = new Shortcut
{
    Title = "\uf00d  Quit",
    Key = Key.Q.WithCtrl
};
quitShortcut.Accepting += (_, _) => app.RequestStop();

var refreshShortcut = new Shortcut
{
    Title = "\uf021  Refresh",
    Key = Key.F5
};
refreshShortcut.Accepting += async (_, e) =>
{
    if (catalog is not null)
    {
        await catalogTree.LoadCatalogAsync(catalog);
    }
    e.Handled = true;
};

var themeShortcut = new Shortcut
{
    Title = "\uf53f  Theme",
    Key = Key.T.WithCtrl
};
themeShortcut.Accepting += (_, e) =>
{
    ThemeLoader.CycleTheme();
    window.SetNeedsDraw();
    e.Handled = true;
};

statusBar.Add(quitShortcut, refreshShortcut, themeShortcut);

window.Add(header, headerSeparator, leftPane, rightPane, statusBar);

// ── Test monitor for graph testing ──────────────────────────────────────────
// Add a test monitor after UI initializes when enabled in config
if (monitorSettings.EnableTestMonitor)
{
    window.Initialized += (_, _) =>
    {
        var testMonitor = new TestMonitor();
        activeMonitors.TryAdd("test-queue", testMonitor);
        dashboard.AddMonitor("test-queue", testMonitor);
    };
}

// ── Background initialization ────────────────────────────────────────────────
// Load Azure connection and catalog data in background after TUI is shown

_ = Task.Run(async () =>
{
    try
    {
        // Initialize Azure client
        queueServiceClient = new QueueServiceClient(azureConfig.ConnectionString);
        catalog = new StorageMonitorCatalog(queueServiceClient);

        // Load catalog data
        await catalogTree.LoadCatalogAsync(catalog);
    }
    catch (Exception ex)
    {
        app.Invoke(() => errorHandler.Handle(ex, "Initializing Azure connection"));
    }
});

app.Run(window);

// ── Cleanup ──────────────────────────────────────────────────────────────────

foreach (var monitor in activeMonitors.Values)
{
    monitor.Dispose();
}

activeMonitors.Clear();

return 0;

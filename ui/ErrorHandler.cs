using System.Text;

using Terminal.Gui.App;
using Terminal.Gui.Views;

using AzureMonitorTui.Monitors;

namespace AzureMonitorTui.Ui;

/// <summary>
/// Global error handler that displays exceptions in popovers when debug mode is enabled.
/// </summary>
public sealed class ErrorHandler
{
    private readonly IApplication _app;
    private readonly MonitorSettings _settings;
    private static readonly string[] buttons = new[] { "OK", "Copy to Clipboard" };

    public ErrorHandler(IApplication app, MonitorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(settings);

        _app = app;
        _settings = settings;
    }

    /// <summary>
    /// Handles an exception by showing a debug popover if ShowDebugErrors is enabled.
    /// Returns true if the error was shown, false if suppressed.
    /// </summary>
    public bool Handle(Exception exception, string context = "Operation")
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (!_settings.ShowDebugErrors)
        {
            return false;
        }

        _app.Invoke(() =>
        {
            ShowErrorDialog(exception, context);
        });

        return true;
    }

    /// <summary>
    /// Handles an exception asynchronously by showing a debug popover if enabled.
    /// </summary>
    public Task<bool> HandleAsync(Exception exception, string context = "Operation")
    {
        return Task.FromResult(Handle(exception, context));
    }

    private void ShowErrorDialog(Exception exception, string context)
    {
        var message = BuildErrorMessage(exception, context);

        MessageBox.ErrorQuery(
            app: _app,
            title: "\uf188  Error",
            message: message,
            defaultButton: 0,
            buttons: buttons);
    }

    private static string BuildErrorMessage(Exception exception, string context)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Context: {context}");
        sb.AppendLine();
        sb.AppendLine($"Exception: {exception.GetType().Name}");
        sb.AppendLine($"Message: {exception.Message}");

        if (exception.InnerException is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"Inner Exception: {exception.InnerException.GetType().Name}");
            sb.AppendLine($"Inner Message: {exception.InnerException.Message}");
        }

        if (!string.IsNullOrWhiteSpace(exception.StackTrace))
        {
            sb.AppendLine();
            sb.AppendLine("Stack Trace:");

            var lines = exception.StackTrace.Split('\n');
            var topLines = lines.Take(5);

            foreach (var line in topLines)
            {
                sb.AppendLine($"  {line.Trim()}");
            }

            if (lines.Length > 5)
            {
                sb.AppendLine($"  ... ({lines.Length - 5} more lines)");
            }
        }

        return sb.ToString();
    }
}

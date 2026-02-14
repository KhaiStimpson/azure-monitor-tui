using AzureMonitorTui.Monitors;

namespace AzureMonitorTui.Ui;

/// <summary>
/// Test monitor that generates random data for graph testing.
/// </summary>
public sealed class TestMonitor : IMonitor<int>
{
    private readonly Random _random = new();
    private int _currentValue = 10;

    public Task<bool> TryBegin(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    public Task<int> Out(CancellationToken ct = default)
    {
        // Generate realistic-looking queue count changes
        var change = _random.Next(-3, 5); // Slightly more likely to grow
        _currentValue = Math.Max(0, _currentValue + change);
        
        return Task.FromResult(_currentValue);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}

namespace AzureMonitorTui.Monitors;

public interface IMonitor<T> : IDisposable
{
    Task<bool> TryBegin(CancellationToken ct = default);
    Task<T> Out(CancellationToken ct = default);
}

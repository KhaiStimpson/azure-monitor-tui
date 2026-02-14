using Azure.Storage.Queues;

namespace AzureMonitorTui.Monitors;

public interface ICatalog
{
    IEnumerable<CatalogItem> GetAvailable();
}

public sealed class CatalogItem
{
    public required string Name { get; set; }

    public required Type Type { get; set; }

    public string JsonSerializedConfig { get; set; } = string.Empty;
}

public sealed class StorageMonitorCatalog : ICatalog
{
    private readonly QueueServiceClient _queueServiceClient;

    public StorageMonitorCatalog(QueueServiceClient queueServiceClient)
    {
        ArgumentNullException.ThrowIfNull(queueServiceClient);
        _queueServiceClient = queueServiceClient;
    }

    public IEnumerable<CatalogItem> GetAvailable()
    {
        var queues = _queueServiceClient.GetQueues();

        return queues.Select(q => new CatalogItem
        {
            Name = q.Name,
            Type = typeof(StorageAccountConfig)
        });
    }
}

public sealed class StorageMonitor : IMonitor<int>
{
    private readonly StorageAccountConfig _config;
    private readonly QueueClient _queueClient;

    public StorageMonitor(StorageAccountConfig config, QueueServiceClient queueServiceClient)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(queueServiceClient);

        _config = config;
        _queueClient = queueServiceClient.GetQueueClient(config.Name);
    }

    public async Task<bool> TryBegin(CancellationToken ct = default)
    {
        var response = await _queueClient.ExistsAsync(ct);
        return response?.Value ?? false;
    }

    public async Task<int> Out(CancellationToken ct = default)
    {
        var properties = await _queueClient.GetPropertiesAsync(ct);
        return properties.Value.ApproximateMessagesCount;
    }

    public void Dispose()
    {
        // QueueClient does not require disposal
    }
}

public sealed class StorageAccountConfig
{
    public string Name { get; set; } = string.Empty;
}

namespace AzureMonitorTui.Monitors;

public sealed class AzureStorageConfig
{
    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class MonitorSettings
{
    public int PollIntervalSeconds { get; set; } = 10;

    public int MaxDataPoints { get; set; } = 200;

    public bool ShowDebugErrors { get; set; } = false;
}

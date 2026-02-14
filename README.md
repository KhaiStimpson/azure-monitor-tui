# Azure Monitor TUI

A Terminal User Interface (TUI) application for monitoring Azure Queue Storage built with .NET 10 and Terminal.Gui v2.

## Overview

Azure Monitor TUI provides a real-time, terminal-based interface for monitoring Azure Queue Storage queues. It displays queue statistics, message counts, and activity in a responsive, keyboard-navigable dashboard.

## Features

- **Real-time Queue Monitoring**: Track queue statistics and message counts in real-time
- **Terminal User Interface**: Rich TUI built with Terminal.Gui v2 for responsive keyboard and mouse interaction
- **Theme Support**: Built-in themes (Tokyo Night, Rose Pine) with customizable color schemes
- **Error Handling**: Comprehensive error dialog system with debug mode support
- **Async Architecture**: Non-blocking async/await patterns throughout
- **Configuration**: JSON-based configuration for easy customization

## Prerequisites

- .NET 10 SDK
- Azure Storage Account (or local Azurite for development)

## Getting Started

### Installation

```bash
git clone <repository-url>
cd ire-monitor
dotnet restore
dotnet build
```

### Running Locally

```bash
# With Azurite (local Azure Storage emulator)
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite mcr.microsoft.com/azure-storage/azurite
dotnet run
```

### Configuration

Configuration is managed through `appsettings.json`:

```json
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=...;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;"
  },
  "Monitoring": {
    "PollIntervalMs": 5000
  },
  "ShowDebugErrors": true
}
```

Themes are loaded from `~/.config/azure-monitor-tui.json` or fall back to local `tui-config.json`.

## Project Structure

```
.
├── monitors/              # Domain logic and Azure integration
│   ├── IMonitor.cs       # Monitor interface
│   ├── StorageMonitor.cs # Azure Queue Storage monitoring
│   └── MonitorConfig.cs  # Configuration POCOs
├── ui/                   # Terminal.Gui views and components
│   ├── MonitorDashboard.cs    # Main dashboard
│   ├── QueueMonitorCard.cs    # Queue status card
│   ├── CatalogTreeView.cs     # Queue catalog tree view
│   ├── Theme.cs               # Theme management
│   ├── ErrorHandler.cs        # Error dialog handling
│   └── TestMonitor.cs         # Test monitor for development
├── Program.cs            # Entry point
├── appsettings.json      # Application configuration
└── tui-config.json       # Terminal.Gui theme configuration
```

## Development

### Building

```bash
dotnet build              # Debug build
dotnet build -c Release   # Release build
```

### Running Tests

```bash
dotnet test               # Run all tests
```

### Code Style

This project follows strict C# conventions:
- **Nullable reference types**: Enabled with null safety guards
- **Formatting**: Allman-style braces, 4-space indentation
- **Naming**: PascalCase for types/methods, camelCase for locals
- **Async patterns**: Async/await with `CancellationToken` throughout
- **Resource management**: Proper disposal and event cleanup

For detailed guidelines, see [AGENTS.md](AGENTS.md).

## Architecture Notes

### Terminal.Gui v2 Integration

- Uses instance methods on `IApplication` (never static `Application.*`)
- Timers managed via `_app.AddTimeout()` and `RemoveTimeout()`
- UI updates from background threads via `_app.Invoke()`
- Post-initialization work handled via `Initialized` event

### Monitoring Pattern

The `IMonitor<T>` interface provides a pluggable architecture for different monitoring backends:

```csharp
public interface IMonitor<T>
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    event EventHandler<MonitorEventArgs<T>>? DataUpdated;
}
```

### Error Handling

Errors are handled through the `ErrorHandler` class:
- User-facing errors show a dialog when `ShowDebugErrors: true`
- Background errors logged and displayed in UI
- Secrets never leaked in error messages

## Publishing

```bash
dotnet publish -c Release
```

The published application can be run on any system with .NET 10 runtime.

## License

[Add your license here]

## Contributing

[Add contribution guidelines here]

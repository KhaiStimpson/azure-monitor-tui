# AGENTS

Guidance for coding agents working in this repository.
Follow existing conventions. Keep changes minimal and consistent.

## Overview

- **Language**: C# (.NET), targeting `net10.0`
- **Project**: Single project (`azure-monitor-tui.csproj`), no solution file
- **Entry point**: Top-level statements in `Program.cs`
- **Structure**: `monitors/` (domain/Azure logic, `AzureMonitorTui.Monitors`), `ui/` (Terminal.Gui views, `AzureMonitorTui.Ui`)
- **Key settings**: Nullable reference types enabled, implicit usings enabled

## Important Packages

- **Terminal.Gui** `2.0.0-alpha.4170` — v2 alpha API; use instance methods on `IApplication`, never static `Application.*`
- **Azure.Storage.Queues** `12.25.0` — Azure Queue Storage SDK
- **Microsoft.Extensions.Configuration.Json/Binder** `9.0.4` — JSON config loading and POCO binding

## Build and Run

```bash
dotnet restore          # Restore packages
dotnet build            # Build
dotnet run              # Run (uses appsettings.json)
dotnet clean            # Clean
dotnet publish -c Release   # Publish
```

**Local dev with Azurite:**
```bash
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite mcr.microsoft.com/azure-storage/azurite
dotnet run
```

## Testing

No test project exists yet. When added:
```bash
dotnet test                                              # All tests
dotnet test --filter "Name~TestName"                     # By name substring
dotnet test --filter "FullyQualifiedName~Namespace.Class.Method"  # By FQN (recommended)
dotnet test --list-tests                                 # List all
```

## Code Style

### Imports and Namespaces

```csharp
// 1. System usings
using System.Drawing;
using System.Text;

// 2. Third-party packages
using Terminal.Gui.App;
using Terminal.Gui.Views;

// 3. Project usings
using AzureMonitorTui.Monitors;

// 4. Type aliases (last, after all usings)
using TgColor = Terminal.Gui.Drawing.Color;
using TgAttribute = Terminal.Gui.Drawing.Attribute;

namespace AzureMonitorTui.Ui; // File-scoped namespaces only
```

- Group with blank lines between groups; remove unused usings

### Formatting

- **Indentation**: 4 spaces, no tabs
- **Braces**: Allman style (opening brace on new line)
- **Spacing**: One blank line between type members
- **EOF**: Trailing newline
- **Dead code**: Delete, don't comment out

### Naming

| Category | Convention | Examples |
|---|---|---|
| Types, methods, properties | `PascalCase` | `StorageMonitor`, `PollAsync()` |
| Interfaces | `IPrefix` | `IMonitor<T>`, `ICatalog` |
| Private fields | `_camelCase` | `_app`, `_timerToken`, `_disposed` |
| Locals, parameters | `camelCase` | `azureConfig`, `count`, `ct` |
| Constants, static readonly | `PascalCase` | `CardHeight`, `SpinnerFrames` |

### Types and Nullability

- Use `?` for nullable references; guard with `ArgumentNullException.ThrowIfNull(param)`
- Prefer `is not null` / `is null` over `!= null` / `== null`
- Use `var` when RHS type is obvious; explicit types for unclear/nullable declarations
- Mark classes `sealed` unless designed for inheritance; use `internal` for assembly-only types
- Never return `null` for collections — return `Array.Empty<T>()` or empty list

### Async and Cancellation

- Async methods return `Task`/`Task<T>` with `Async` suffix (e.g., `PollAsync()`)
- Pass `CancellationToken ct = default` through call chains
- Never use `.Result` or `.Wait()` — use `await` throughout
- Fire-and-forget: `_ = Task.Run(async () => { await PollAsync(); });`
- Avoid `async void` except for event handlers

### Error Handling

- Use `ErrorHandler` for user-facing errors (shows dialog when `ShowDebugErrors: true`)
- Background errors: `_app.Invoke(() => errorHandler.Handle(ex, "context"))`
- Silent swallow only for non-critical poll failures
- Never leak secrets in error messages
```csharp
try { /* risky operation */ }
catch (Exception ex) { errorHandler.Handle(ex, "Context of failure"); }
```

### Terminal.Gui v2 Patterns

- Obtain `IApplication` from `Application.Create()`; pass through constructors as `_app`
- **Timers**: `_timerToken = _app.AddTimeout(interval, callback)` — callback returns `bool` (true=continue)
- **UI thread**: `_app.Invoke(() => { /* UI updates */ })` from background threads
- **Exit**: `app.RequestStop()` (instance method, not static)
- **Post-init work**: Subscribe to `Initialized` event; guard with `IsInitialized` check

### Resource Management

```csharp
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
            ThemeManager.ThemeChanged -= OnThemeChanged; // Unsubscribe events
        }
    }
    base.Dispose(disposing);
}
```

- Reuse Azure clients (`QueueServiceClient`) — create once, store in field
- Use `using` declarations for short-lived resources
- Always null timer tokens after removal

### Configuration

- `appsettings.json` for app settings; bind to sealed POCOs with default values
- `tui-config.json` for Terminal.Gui themes (Tokyo Night, Rose Pine)
- Theme loading: `~/.config/azure-monitor-tui.json` with fallback to local `tui-config.json`
- Config binding: `configuration.GetSection("Key").Bind(pocoInstance)`
- Never hardcode secrets; use Azurite connection string for local dev

### Organization

- `monitors/` — domain logic, Azure integration, config POCOs (`*Config`/`*Settings`)
- `ui/` — Terminal.Gui views, theme utilities, error handling
- One primary type per file; small related helpers (node types, event args) OK in same file
- XML `<summary>` doc comments on all public types and important methods
- Never edit `obj/` or `bin/`

## Cursor/Copilot Rules

No `.cursorrules`, `.cursor/rules/`, or `.github/copilot-instructions.md` detected.

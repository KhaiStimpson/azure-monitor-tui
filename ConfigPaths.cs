using System.Diagnostics;

namespace AzureMonitorTui;

/// <summary>
/// Provides config file path resolution based on build configuration.
/// Debug builds (or when a debugger is attached) use the current working directory.
/// Release builds use bundled defaults from the app directory, overridden by
/// user config files in ~/.config/az-monitor/.
/// </summary>
internal static class ConfigPaths
{
    private static readonly string UserConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "az-monitor");

#if DEBUG
    private const bool IsDebugBuild = true;
#else
    private const bool IsDebugBuild = false;
#endif

    private static readonly bool _useLocalConfig = Debugger.IsAttached || IsDebugBuild;

    /// <summary>
    /// The base directory for bundled config files.
    /// Debug: current working directory. Release: application base directory.
    /// </summary>
    public static string BaseConfigDirectory => _useLocalConfig
        ? Directory.GetCurrentDirectory()
        : AppContext.BaseDirectory;

    /// <summary>
    /// Whether user-level config overrides should be loaded (~/.config/az-monitor/).
    /// Only enabled in Release builds without a debugger attached.
    /// </summary>
    public static bool UseUserOverrides => !_useLocalConfig;

    /// <summary>
    /// Returns the full path to a config file in the base config directory.
    /// </summary>
    public static string GetLocalPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return Path.Combine(BaseConfigDirectory, fileName);
    }

    /// <summary>
    /// Returns the full path to a config file in the user config directory.
    /// </summary>
    public static string GetUserConfigPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return Path.Combine(UserConfigDirectory, fileName);
    }
}

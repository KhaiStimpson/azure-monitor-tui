using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;

using TgColor = Terminal.Gui.Drawing.Color;
using TgAttribute = Terminal.Gui.Drawing.Attribute;

namespace AzureMonitorTui.Ui;

/// <summary>
/// Handles theme loading from custom config paths and theme cycling.
/// Provides theme-aware color utilities for graph visualization.
/// </summary>
public static class ThemeLoader
{
    private static readonly string UserConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "azure-monitor-tui.json");

    private static readonly string LocalConfigPath = Path.Combine(
        AppContext.BaseDirectory,
        "tui-config.json");

    /// <summary>
    /// Loads theme configuration from ~/.config/azure-monitor-tui.json if it exists,
    /// otherwise falls back to tui-config.json in the application directory.
    /// Enables ConfigurationManager with the loaded config.
    /// </summary>
    public static void Load()
    {
        string? configPath = null;

        // Try user config first
        if (File.Exists(UserConfigPath))
        {
            configPath = UserConfigPath;
        }
        // Fall back to local config
        else if (File.Exists(LocalConfigPath))
        {
            configPath = LocalConfigPath;
        }

        if (configPath is not null)
        {
            try
            {
                var configJson = File.ReadAllText(configPath);
                ConfigurationManager.RuntimeConfig = configJson;
                ConfigurationManager.Enable(ConfigLocations.Runtime);
            }
            catch (Exception ex)
            {
                // If config loading fails, Terminal.Gui will use built-in defaults
                Console.Error.WriteLine($"Warning: Failed to load theme config from {configPath}: {ex.Message}");
            }
        }
        else
        {
            // No config file found, Terminal.Gui will use built-in defaults
            Console.WriteLine("No theme config found, using Terminal.Gui defaults");
        }
    }

    /// <summary>
    /// Cycles to the next theme in the list of available themes.
    /// If only one theme or no themes are available, does nothing.
    /// </summary>
    public static void CycleTheme()
    {
        var themeNames = ThemeManager.GetThemeNames();

        if (themeNames.Count <= 1)
        {
            return;
        }

        var currentTheme = ThemeManager.Theme;
        var currentIndex = themeNames.IndexOf(currentTheme);

        // Move to next theme, wrapping around to the first if needed
        var nextIndex = (currentIndex + 1) % themeNames.Count;
        var nextTheme = themeNames[nextIndex];

        ThemeManager.Theme = nextTheme;
        ConfigurationManager.Apply();
    }

    /// <summary>
    /// Gets the theme-appropriate color for graph path lines.
    /// Tokyo Night uses bright blue, Rose Pine uses foam.
    /// </summary>
    public static TgAttribute GetGraphLineColor()
    {
        var currentTheme = ThemeManager.Theme;

        return currentTheme switch
        {
            "Tokyo Night" => new TgAttribute(new TgColor(0x7a, 0xa2, 0xf7), new TgColor(0x1a, 0x1b, 0x26)),
            "Rose Pine" => new TgAttribute(new TgColor(0x9c, 0xcf, 0xd8), new TgColor(0x19, 0x17, 0x24)),
            _ => new TgAttribute(TgColor.BrightCyan, TgColor.Black) // Fallback
        };
    }

    /// <summary>
    /// Gets the theme-appropriate color for graph scatter dots.
    /// Tokyo Night uses cyan, Rose Pine uses pine green.
    /// </summary>
    public static TgAttribute GetGraphDotColor()
    {
        var currentTheme = ThemeManager.Theme;

        return currentTheme switch
        {
            "Tokyo Night" => new TgAttribute(new TgColor(0x7d, 0xcf, 0xff), new TgColor(0x1a, 0x1b, 0x26)),
            "Rose Pine" => new TgAttribute(new TgColor(0x31, 0x74, 0x8f), new TgColor(0x19, 0x17, 0x24)),
            _ => new TgAttribute(TgColor.Cyan, TgColor.Black) // Fallback
        };
    }

    /// <summary>
    /// Gets the theme-appropriate color for the count label accent.
    /// Tokyo Night uses yellow, Rose Pine uses gold.
    /// </summary>
    public static TgAttribute GetCountLabelColor()
    {
        var currentTheme = ThemeManager.Theme;

        return currentTheme switch
        {
            "Tokyo Night" => new TgAttribute(new TgColor(0xe0, 0xaf, 0x68), new TgColor(0x16, 0x16, 0x1e)),
            "Rose Pine" => new TgAttribute(new TgColor(0xf6, 0xc1, 0x77), new TgColor(0x1f, 0x1d, 0x2e)),
            _ => new TgAttribute(TgColor.Yellow, TgColor.Black) // Fallback
        };
    }
}

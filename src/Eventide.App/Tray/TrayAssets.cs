using System.Drawing;
using System.Windows.Forms;
using Eventide.Platform.Windows;

namespace Eventide.App.Tray;

public enum TrayIconState
{
    On,
    Off,
    Paused,
}

/// <summary>Embedded tray icons (per state and taskbar theme) and menu images.</summary>
internal sealed class TrayAssets : IDisposable
{
    private readonly Dictionary<string, Icon> _icons = [];
    private readonly Dictionary<string, Image> _images = [];

    public Icon TrayIcon(TrayIconState state)
    {
        var theme = SystemState.IsTaskbarLight() ? "light" : "dark";
        var name = $"tray-{state.ToString().ToLowerInvariant()}-{theme}.ico";
        if (!_icons.TryGetValue(name, out var icon))
        {
            using var stream = Open(name);

            // Pick the frame matching the small icon size of the current DPI (16, 20, 24, 32 …).
            icon = new Icon(stream, SystemInformation.SmallIconSize);
            _icons[name] = icon;
        }

        return icon;
    }

    /// <summary>Menu image (32 px PNG, drawn scaled by the renderer); <paramref name="accent"/> = accent colour variant.</summary>
    public Image MenuImage(string name, bool accent = false)
    {
        var key = accent ? $"menu.{name}-accent.png" : $"menu.{name}.png";
        if (!_images.TryGetValue(key, out var image))
        {
            using var stream = Open(key);
            using var loaded = Image.FromStream(stream);
            image = new Bitmap(loaded);
            _images[key] = image;
        }

        return image;
    }

    /// <summary>Theme or DPI changed: reload the icons on next use.</summary>
    public void ClearIcons()
    {
        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }

        _icons.Clear();
    }

    public void Dispose()
    {
        ClearIcons();
        foreach (var image in _images.Values)
        {
            image.Dispose();
        }

        _images.Clear();
    }

    private static Stream Open(string name) =>
        typeof(TrayAssets).Assembly.GetManifestResourceStream("Eventide.Assets." + name)
        ?? throw new InvalidOperationException("Missing embedded resource: " + name);
}

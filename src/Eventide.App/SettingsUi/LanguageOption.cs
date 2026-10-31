using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using Eventide.Core.Localization;

namespace Eventide.App.SettingsUi;

/// <summary>Entry of the language selection: flat 2D flag (none for "Automatic", which shows a globe) and name.</summary>
public sealed class LanguageOption : ObservableObject
{
    private readonly string _key;

    public LanguageOption(string key, string? flagUri)
    {
        _key = key;
        if (flagUri is not null)
        {
            using var stream = AssetLoader.Open(new Uri(flagUri));
            Flag = new Bitmap(stream);
        }
    }

    public string Name => Loc.T(_key);

    public Bitmap? Flag { get; }

    public bool HasFlag => Flag is not null;

    public bool IsAuto => Flag is null;

    /// <summary>UI language changed ("Automatic (Windows)" is translated).</summary>
    public void Refresh() => OnPropertyChanged(nameof(Name));
}

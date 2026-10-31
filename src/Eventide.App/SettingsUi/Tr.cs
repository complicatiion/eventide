using System.ComponentModel;
using Eventide.Core.Localization;

namespace Eventide.App.SettingsUi;

/// <summary>
/// Localised strings for XAML: <c>{Binding [Key], Source={x:Static ui:Tr.I}}</c>. Switching the language refreshes
/// every binding without a restart.
/// </summary>
public sealed class Tr : INotifyPropertyChanged
{
    public static Tr I { get; } = new();

    private Tr()
    {
        Loc.Changed += () =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => Loc.T(key);
}

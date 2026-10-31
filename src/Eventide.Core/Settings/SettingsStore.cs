using System.Text.Json;
using System.Text.Json.Serialization;

namespace Eventide.Core.Settings;

/// <summary>
/// Loads and saves <see cref="EventideSettings"/>.
/// Location: <c>%AppData%\Eventide\settings.json</c>; portable mode (file <c>Eventide.portable</c> next to the EXE):
/// <c>data\settings.json</c> next to the EXE.
/// Tray and settings window run as separate processes of the same EXE, so every change is a read-modify-write
/// under a named mutex (<see cref="Update"/>): each side only changes the values it owns.
/// </summary>
public sealed class SettingsStore
{
    public const string PortableMarker = "Eventide.portable";
    private const string MutexName = @"Local\Eventide.SettingsFile";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public SettingsStore(string? directory = null)
    {
        Directory = directory ?? DefaultDirectory();
        FilePath = Path.Combine(Directory, "settings.json");
    }

    public string Directory { get; }

    public string FilePath { get; }

    public static bool IsPortable => File.Exists(Path.Combine(AppContext.BaseDirectory, PortableMarker));

    public static string DefaultDirectory() => IsPortable
        ? Path.Combine(AppContext.BaseDirectory, "data")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Eventide");

    public EventideSettings Load() => WithLock(LoadUnlocked);

    /// <summary>Reads the current file, applies <paramref name="change"/> and writes it back – atomically for both processes.</summary>
    public EventideSettings Update(Action<EventideSettings> change) => WithLock(() =>
    {
        var settings = LoadUnlocked();
        change(settings);
        settings.Normalize();
        SaveUnlocked(settings);
        return settings;
    });

    private EventideSettings LoadUnlocked()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<EventideSettings>(json, Json);
                if (loaded is not null)
                {
                    return loaded.Normalize();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            // Unreadable or damaged file: start with defaults, keep a copy for the user.
            TryBackupDamaged();
        }

        return new EventideSettings().Normalize();
    }

    private void SaveUnlocked(EventideSettings settings)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
        File.Move(temp, FilePath, overwrite: true);
    }

    private void TryBackupDamaged()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Copy(FilePath, FilePath + ".damaged", overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best effort
        }
    }

    private static T WithLock<T>(Func<T> action)
    {
        using var mutex = new Mutex(false, MutexName);
        var owned = false;
        try
        {
            try
            {
                owned = mutex.WaitOne(TimeSpan.FromSeconds(3));
            }
            catch (AbandonedMutexException)
            {
                owned = true;
            }

            return action();
        }
        finally
        {
            if (owned)
            {
                mutex.ReleaseMutex();
            }
        }
    }
}

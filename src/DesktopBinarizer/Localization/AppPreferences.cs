using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopBinarizer;

public sealed record AppPreferences
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public AppLanguage Language { get; init; } = AppLanguage.ChineseSimplified;
}

public static class AppPreferencesFile
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopBinarizer",
        "settings.json");

    public static AppPreferences LoadOrDefault(string? path = null)
    {
        string resolvedPath = path ?? DefaultPath;
        if (!File.Exists(resolvedPath))
        {
            return new AppPreferences();
        }

        try
        {
            return Deserialize(File.ReadAllText(resolvedPath));
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                JsonException or
                InvalidDataException or
                ArgumentException)
        {
            return new AppPreferences();
        }
    }

    public static void Save(AppPreferences preferences, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        Validate(preferences);

        string resolvedPath = path ?? DefaultPath;
        string? directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(resolvedPath, Serialize(preferences));
    }

    public static string Serialize(AppPreferences preferences)
    {
        Validate(preferences);
        return JsonSerializer.Serialize(preferences, JsonOptions);
    }

    public static AppPreferences Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        AppPreferences preferences = JsonSerializer.Deserialize<AppPreferences>(json, JsonOptions)
            ?? throw new InvalidDataException("The preferences file is empty.");
        Validate(preferences);
        return preferences;
    }

    private static void Validate(AppPreferences preferences)
    {
        if (preferences.Version != AppPreferences.CurrentVersion ||
            !Enum.IsDefined(preferences.Language))
        {
            throw new InvalidDataException("Unsupported preferences file.");
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

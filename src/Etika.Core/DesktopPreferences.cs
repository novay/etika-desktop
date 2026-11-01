using System.Text.Json;

namespace Etika.Core;

public sealed record DesktopPreferences
{
    public const string DefaultServer = "https://etika.kukarkab.com";
    public string Server { get; init; } = DefaultServer;
    public int Theme { get; init; }
}

public sealed class PreferencesStore(string path)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true
    };

    public DesktopPreferences Load()
    {
        try
        {
            var value = JsonSerializer.Deserialize<DesktopPreferences>(File.ReadAllText(path), Json) ?? new();
            return value with
            {
                Server = string.IsNullOrWhiteSpace(value.Server) ? DesktopPreferences.DefaultServer : value.Server,
                Theme = value.Theme is >= 0 and <= 2 ? value.Theme : 0
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public bool Save(DesktopPreferences value)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, Json));
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}

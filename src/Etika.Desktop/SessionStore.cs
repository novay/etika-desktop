using System.Text.Json;
using Etika.Core;
using Windows.Security.Credentials;

namespace Etika.Desktop;

internal sealed record SavedSession(string Token, DateTimeOffset ExpiresAt);

internal static class SessionStore
{
    private static string Resource(string server) => "ETIKA.Desktop:" + server.TrimEnd('/');
    private static string SettingsPath
    {
        get
        {
#if DEBUG
            if (Environment.GetEnvironmentVariable("ETIKA_TEST_DATA_DIR") is { Length: > 0 } testDirectory)
                return Path.Combine(testDirectory, "settings.json");
#endif
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ETIKA", "Desktop", "settings.json");
        }
    }
    public static DesktopPreferences Preferences => new PreferencesStore(SettingsPath).Load();
    public static bool SaveServer(string server) => new PreferencesStore(SettingsPath).Save(Preferences with { Server = server });
    public static bool SaveTheme(int theme) => new PreferencesStore(SettingsPath).Save(Preferences with { Theme = theme });

    public static SavedSession? Load(string server)
    {
        try
        {
            var credential = new PasswordVault().Retrieve(Resource(server), "session");
            credential.RetrievePassword();
            var session = JsonSerializer.Deserialize<SavedSession>(credential.Password);
            if (session is not null && !string.IsNullOrWhiteSpace(session.Token) && session.ExpiresAt > DateTimeOffset.UtcNow) return session;
            Clear(server);
        }
        catch { /* unavailable, missing, expired, or malformed credential: require sign-in */ }
        return null;
    }
    public static bool Save(string server, SavedSession session)
    {
        try
        {
            Clear(server);
            new PasswordVault().Add(new PasswordCredential(Resource(server), "session", JsonSerializer.Serialize(session)));
            return true;
        }
        catch { return false; }
    }
    public static void Clear(string server)
    {
        try
        {
            var vault = new PasswordVault();
            vault.Remove(vault.Retrieve(Resource(server), "session"));
        }
        catch { /* already absent, or Windows credential storage is unavailable */ }
    }
}

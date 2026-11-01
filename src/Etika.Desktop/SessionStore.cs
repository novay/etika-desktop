using System.Text.Json;
using Windows.Security.Credentials;

namespace Etika.Desktop;

internal sealed record SavedSession(string Token, DateTimeOffset ExpiresAt);

internal static class SessionStore
{
    private static string Resource(string server) => "ETIKA.Desktop:" + server.TrimEnd('/');
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ETIKA", "Desktop", "settings.json");

    public static string LoadServer()
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SettingsPath))?["server"]
            ?? "https://etika.kukarkab.com"; }
        catch { return "https://etika.kukarkab.com"; }
    }

    public static void SaveServer(string server)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { server }));
    }

    public static SavedSession? Load(string server)
    {
        try
        {
            var credential = new PasswordVault().Retrieve(Resource(server), "session");
            credential.RetrievePassword();
            var session = JsonSerializer.Deserialize<SavedSession>(credential.Password);
            if (session?.ExpiresAt > DateTimeOffset.UtcNow) return session;
            Clear(server);
        }
        catch { /* no credential, or the operating system cannot unlock it */ }
        return null;
    }

    public static void Save(string server, SavedSession session)
    {
        Clear(server);
        new PasswordVault().Add(new PasswordCredential(Resource(server), "session", JsonSerializer.Serialize(session)));
    }

    public static void Clear(string server)
    {
        var vault = new PasswordVault();
        try { vault.Remove(vault.Retrieve(Resource(server), "session")); }
        catch (System.Runtime.InteropServices.COMException) { /* already absent */ }
    }
}

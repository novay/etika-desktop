namespace Etika.Desktop;

internal static class StartupDiagnostics
{
    internal static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ETIKA", "Desktop", "Logs", "startup.log");

    internal static void Write(string stage, Exception error)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var entry = $"{DateTimeOffset.Now:O} [{stage}] {error.GetType().FullName} "
                + $"HRESULT=0x{error.HResult:X8}{Environment.NewLine}{error}{Environment.NewLine}";
            File.AppendAllText(LogPath, entry);
        }
        catch
        {
            // Diagnostics must not replace or hide the original startup exception.
        }
    }
}

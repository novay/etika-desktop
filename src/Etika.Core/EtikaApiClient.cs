using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Etika.Core;

public sealed class EtikaApiClient : IDisposable
{
    public const int MaxPdfBytes = 25 * 1024 * 1024;
    private readonly HttpClient _http;
    private readonly Uri _base;
    private string? _token;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
    public bool HasToken => !string.IsNullOrEmpty(_token);
    public string Server => _base.GetLeftPart(UriPartial.Authority) + _base.AbsolutePath[..^8].TrimEnd('/');

    public EtikaApiClient(string server, HttpClient? http = null)
    {
        if (!Uri.TryCreate(server.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Gunakan alamat server HTTPS, misalnya https://etika.kukarkab.com.");

        _base = new Uri(uri, uri.AbsolutePath.EndsWith("/api/v1/", StringComparison.Ordinal)
            ? "./" : "api/v1/");
        _http = http ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    public void SetToken(string? token) => _token = token;
    public void ClearToken() => _token = null;

    public Task<CaptchaChallenge> ChallengeAsync(CancellationToken ct = default) =>
        DataAsync<CaptchaChallenge>(HttpMethod.Get, "auth/challenge", null, ct);

    public async Task<LoginResult> LoginAsync(string email, string password, string captchaKey,
        string captcha, string deviceName, CancellationToken ct = default)
    {
        ClearToken();
        if (string.IsNullOrWhiteSpace(captchaKey) || string.IsNullOrWhiteSpace(captcha))
            throw new ApiException(422, "Muat dan isi kode verifikasi sebelum masuk.");
        var result = await DataAsync<LoginResult>(HttpMethod.Post, "auth/login", new
        {
            email, password, captcha_key = captchaKey, captcha, device_name = deviceName
        }, ct);
        if (string.IsNullOrWhiteSpace(result.Token) || result.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ApiException(502, "Server mengirim sesi yang tidak valid.");
        SetToken(result.Token);
        return result;
    }

    public Task<Identity> MeAsync(CancellationToken ct = default) =>
        DataAsync<Identity>(HttpMethod.Get, "me", null, ct);
    public Task<ServerContext> ContextAsync(CancellationToken ct = default) =>
        DataAsync<ServerContext>(HttpMethod.Get, "context", null, ct);
    public Task<DocumentDetail> DocumentAsync(string id, CancellationToken ct = default) =>
        DataAsync<DocumentDetail>(HttpMethod.Get, $"dokumen/{Uri.EscapeDataString(id)}", null, ct);
    public Task<PageResult<DocumentSummary>> DocumentsAsync(int year, int page, string search,
        CancellationToken ct = default) => JsonAsync<PageResult<DocumentSummary>>(
            HttpMethod.Get, $"dokumen?tahun={year}&page={page}&per_page=20&q={Uri.EscapeDataString(search)}",
            null, ct);

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        try
        {
            if (HasToken)
            {
                using var response = await SendAsync(HttpMethod.Post, "auth/logout", null, ct);
            }
        }
        finally { ClearToken(); }
    }

    public async Task<byte[]> PdfAsync(string attachmentId, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        ct = timeout.Token;
        using var response = await SendAsync(HttpMethod.Get,
            $"lampiran/{Uri.EscapeDataString(attachmentId)}/download", null, ct);
        if (response.Content.Headers.ContentType?.MediaType != "application/pdf")
            throw new ApiException(415, "Server tidak mengirim berkas PDF.");
        if (response.Content.Headers.ContentLength > MaxPdfBytes)
            throw new ApiException(413, "Ukuran PDF melebihi 25 MB.");
        var bytes = await ReadBoundedAsync(response.Content, MaxPdfBytes, ct);
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new ApiException(415, "Berkas PDF tidak valid.");
        return bytes;
    }

    private async Task<T> DataAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct) =>
        (await JsonAsync<DataEnvelope<T>>(method, path, body, ct)).Data
            ?? throw new ApiException(502, "Respons data server kosong.");

    private async Task<T> JsonAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        ct = timeout.Token;
        using var response = await SendAsync(method, path, body, ct);
        if (response.Content.Headers.ContentType?.MediaType != "application/json")
            throw new ApiException(502, "API belum tersedia atau server mengirim respons yang tidak sesuai.");
        var bytes = await ReadBoundedAsync(response.Content, 2 * 1024 * 1024, ct);
        try { return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new JsonException(); }
        catch (JsonException) { throw new ApiException(502, "Format respons API tidak sesuai."); }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body,
        CancellationToken ct)
    {
        // All request paths are constructed by this client; the server supplies no redirect/download URL.
        using var request = new HttpRequestMessage(method, new Uri(_base, path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (HasToken) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.IsSuccessStatusCode) return response;

        try
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized) ClearToken();
            if (response.StatusCode == HttpStatusCode.NotFound
                && path is "auth/challenge" or "auth/login")
                throw new ApiException(404,
                    "Layanan login desktop belum tersedia pada server ini. Periksa alamat server atau hubungi pengelola ETIKA.");
            string message = (int) response.StatusCode switch
            {
                401 => "Sesi berakhir. Silakan masuk kembali.",
                403 => "Akun atau hak akses tidak mengizinkan tindakan ini.",
                404 => "Data tidak ditemukan atau API desktop belum diaktifkan.",
                429 => "Terlalu banyak permintaan. Coba kembali nanti.",
                >= 300 and < 400 => "Server mengalihkan permintaan. Periksa alamat API.",
                _ => "Permintaan ke server belum berhasil."
            };
            if (response.Content.Headers.ContentType?.MediaType == "application/json")
            {
                var bytes = await ReadBoundedAsync(response.Content, 64 * 1024, ct);
                try
                {
                    using var json = JsonDocument.Parse(bytes);
                    if (json.RootElement.TryGetProperty("message", out var msg)
                        && msg.ValueKind == JsonValueKind.String && msg.GetString() is string value)
                        message = value[..Math.Min(value.Length, 500)];
                }
                catch (JsonException) { /* retain the status-specific message */ }
            }
            throw new ApiException((int) response.StatusCode, message);
        }
        finally { response.Dispose(); }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int limit, CancellationToken ct)
    {
        await using var input = await content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + read > limit) throw new ApiException(413, "Respons server melebihi batas ukuran.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    public void Dispose()
    {
        ClearToken();
        _http.Dispose();
    }
}

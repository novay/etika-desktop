using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Etika.Core;

int passed = 0;
async Task Check(string name, Func<Task> test)
{
    await test();
    Console.WriteLine($"PASS {name}");
    passed++;
}
void Assert(bool value, string message = "Assertion failed")
{
    if (!value) throw new Exception(message);
}
async Task Throws(int status, Func<Task> test)
{
    try { await test(); } catch (ApiException e) { Assert(e.StatusCode == status); return; }
    throw new Exception($"Expected API error {status}");
}
HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
    new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
EtikaApiClient Client(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
    new("https://etika.example.test", new HttpClient(new StubHandler(handler)));

await Check("rejects cleartext remote server", () =>
{
    try { using var bad = new EtikaApiClient("http://example.test"); }
    catch (ArgumentException) { return Task.CompletedTask; }
    throw new Exception("Accepted remote HTTP");
});
await Check("normalizes API prefix and escapes search", async () =>
{
    using var api = Client(r =>
    {
        Assert(r.RequestUri!.AbsolutePath == "/api/v1/dokumen");
        Assert(r.RequestUri.Query.Contains("q=a%26b%20%2F%20c"));
        return Json("""{"data":[],"meta":{"current_page":2,"last_page":3,"total":45,"per_page":20}}""");
    });
    var page = await api.DocumentsAsync(2026, 2, "a&b / c");
    Assert(page.Meta.Total == 45);
});
await Check("passes existing API base without duplication", async () =>
{
    using var api = new EtikaApiClient("https://example.test/api/v1",
        new HttpClient(new StubHandler(r =>
        {
            Assert(r.RequestUri!.AbsolutePath == "/api/v1/me");
            return Json("""{"data":{"id":"1","name":"Tester","email":"test@example.test","level":"SKPD"}}""");
        })));
    await api.MeAsync();
});
await Check("only attaches bearer to configured API", async () =>
{
    using var api = Client(r =>
    {
        Assert(r.RequestUri!.Host == "etika.example.test");
        Assert(r.Headers.Authorization?.Parameter == "secret");
        return Json("""{"data":{"id":"1","name":"Tester","email":"test@example.test","level":"SKPD"}}""");
    });
    api.SetToken("secret");
    await api.MeAsync();
});
await Check("401 clears local token", async () =>
{
    using var api = Client(_ => Json("""{"message":"Expired"}""", HttpStatusCode.Unauthorized));
    api.SetToken("old");
    await Throws(401, async () => await api.MeAsync());
    Assert(!api.HasToken);
});
await Check("logout failure still clears local token", async () =>
{
    using var api = Client(_ => throw new HttpRequestException("Offline"));
    api.SetToken("old");
    try { await api.LogoutAsync(); } catch (HttpRequestException) { }
    Assert(!api.HasToken);
});
await Check("preserves large IDs and exact rupiah values", async () =>
{
    using var api = Client(_ => Json("""
        {"data":{"id":"9007199254740993","tahun":2026,"pagu_ppas":"9007199254740993",
        "pagu_rka":"1250000","lampiran":[]}}
        """));
    var doc = await api.DocumentAsync("9007199254740993");
    Assert(doc.Id == "9007199254740993" && doc.PaguPpas == 9007199254740993m);
});
await Check("rejects redirects", async () =>
{
    using var api = Client(_ => new HttpResponseMessage(HttpStatusCode.Redirect)
    {
        Headers = { Location = new Uri("https://other.example.test") }
    });
    await Throws(302, async () => await api.MeAsync());
});
await Check("rejects HTML masquerading as PDF", async () =>
{
    using var api = Client(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("<html>login</html>", Encoding.UTF8, "application/pdf")
    });
    await Throws(415, async () => await api.PdfAsync("1"));
});
await Check("bounds PDF download before allocation", async () =>
{
    using var api = Client(_ =>
    {
        var content = new ByteArrayContent("%PDF-test"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Headers.ContentLength = EtikaApiClient.MaxPdfBytes + 1L;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    });
    await Throws(413, async () => await api.PdfAsync("1"));
});
await Check("accepts authenticated PDF bytes", async () =>
{
    using var api = Client(_ =>
    {
        var content = new ByteArrayContent("%PDF-1.7\nexample"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    });
    Assert((await api.PdfAsync("1")).Length > 5);
});
await Check("rejects malformed API JSON", async () =>
{
    using var api = Client(_ => Json("{bad"));
    await Throws(502, async () => await api.MeAsync());
});
await Check("login sends credentials only in JSON and replaces previous token", async () =>
{
    string? body = null;
    using var handler = new AsyncStubHandler(async request =>
    {
        Assert(request.RequestUri!.AbsolutePath == "/api/v1/auth/login");
        Assert(request.RequestUri.Query == "");
        Assert(request.Headers.Authorization is null);
        body = await request.Content!.ReadAsStringAsync();
        return Json("""{"data":{"token":"new-token","expires_at":"2099-01-01T00:00:00Z","user":{"id":"1","name":"Test","email":"user@example.test","level":"SKPD"}}}""");
    });
    using var api = new EtikaApiClient("https://etika.example.test", new HttpClient(handler));
    api.SetToken("previous");
    await api.LoginAsync("user@example.test", "secret-password", "key", "code", "Test PC");
    Assert(api.HasToken && body!.Contains("\"captcha_key\":\"key\"")
        && body.Contains("\"device_name\":\"Test PC\""));
});
await Check("failed login does not retain previous token", async () =>
{
    using var api = Client(_ => Json("""{"message":"Invalid credentials"}""", HttpStatusCode.UnprocessableEntity));
    api.SetToken("previous");
    await Throws(422, async () => await api.LoginAsync("user", "bad", "key", "code", "PC"));
    Assert(!api.HasToken);
});
await Check("restores canonical root for subdirectory deployments", () =>
{
    using var api = Client(_ => Json("{}"));
    Assert(api.Server == "https://etika.example.test");
    using var nested = new EtikaApiClient("https://example.test/etika/api/v1");
    Assert(nested.Server == "https://example.test/etika");
    return Task.CompletedTask;
});
await Check("does not transmit credentials before a verification challenge is ready", async () =>
{
    bool called = false;
    using var api = Client(_ =>
    {
        called = true;
        return Json("{}");
    });
    await Throws(422, async () => await api.LoginAsync("user@example.test", "password", "", "", "PC"));
    Assert(!called && !api.HasToken);
});
await Check("shows only the supported document views for each single role", () =>
{
    Assert(WorkspaceProfile.ForLevel("SKPD").Documents.Select(x => x.Key).SequenceEqual(["own"]));
    Assert(WorkspaceProfile.ForLevel("Berkas").Documents.Single().Status == 1);
    Assert(WorkspaceProfile.ForLevel("TAPD").Documents.Single().Status == 3);
    Assert(WorkspaceProfile.ForLevel("Eksekutif").Documents.Single().Key == "all");
    Assert(WorkspaceProfile.ForLevel("Admin").Documents.Select(x => x.Key).SequenceEqual(["all", "berkas", "tapd"]));
    return Task.CompletedTask;
});
await Check("normalizes multiple roles without advertising an unsupported ownership filter", () =>
{
    var profile = WorkspaceProfile.ForLevel(" skpd | TaPd,SKPD ");
    Assert(profile.RoleLabel == "SKPD · TAPD");
    Assert(profile.Documents.Select(x => x.Key).SequenceEqual(["accessible", "tapd"]));
    Assert(profile.Documents[0].Status is null && profile.Documents[1].Status == 3);
    Assert(WorkspaceProfile.ForLevel("ADMIN|SKPD").Documents.All(x => x.Key != "own"));
    return Task.CompletedTask;
});
await Check("unknown roles do not receive document navigation", () =>
{
    Assert(WorkspaceProfile.ForLevel("unknown").Documents.Count == 0);
    Assert(WorkspaceProfile.ForLevel(null).Documents.Count == 0);
    return Task.CompletedTask;
});
await Check("sends stage filters together with search and pagination to the API", async () =>
{
    using var api = Client(request =>
    {
        var query = request.RequestUri!.Query;
        Assert(query.Contains("status=3") && query.Contains("page=2") && query.Contains("tahun=2026"));
        Assert(query.Contains("q=a%26b"));
        return Json("""{"data":[],"meta":{"current_page":2,"last_page":2,"total":21,"per_page":20}}""");
    });
    await api.DocumentsAsync(2026, 2, "a&b", WorkspaceProfile.ForLevel("TAPD").Documents.Single().Status);
});
await Check("server failures do not expose database diagnostics", async () =>
{
    using var api = Client(_ => Json("""{"message":"SQLSTATE private diagnostic"}""", HttpStatusCode.ServiceUnavailable));
    try { await api.MeAsync(); }
    catch (ApiException error)
    {
        Assert(error.StatusCode == 503 && !error.Message.Contains("SQLSTATE") && error.Message.Contains("gangguan"));
        return;
    }
    throw new Exception("Expected service error");
});
await Check("theme and server settings preserve each other and legacy settings remain readable", () =>
{
    string folder = Path.Combine(Path.GetTempPath(), "etika-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    try
    {
        string path = Path.Combine(folder, "settings.json");
        File.WriteAllText(path, """{"server":"https://legacy.example.test"}""");
        var store = new PreferencesStore(path);
        Assert(store.Load().Server == "https://legacy.example.test" && store.Load().Theme == 0);
        Assert(store.Save(store.Load() with { Theme = 2 }));
        Assert(store.Save(store.Load() with { Server = "https://new.example.test" }));
        Assert(store.Load().Theme == 2 && store.Load().Server == "https://new.example.test");
        Assert(Directory.GetFiles(folder).Length == 1);
    }
    finally { Directory.Delete(folder, recursive: true); }
    return Task.CompletedTask;
});
await Check("corrupt settings and unsupported themes recover to usable defaults", () =>
{
    string file = Path.GetTempFileName();
    try
    {
        var store = new PreferencesStore(file);
        File.WriteAllText(file, "{bad");
        Assert(store.Load().Server == DesktopPreferences.DefaultServer);
        File.WriteAllText(file, """{"server":"","theme":-5}""");
        Assert(store.Load().Theme == 0 && store.Load().Server == DesktopPreferences.DefaultServer);
    }
    finally { File.Delete(file); }
    return Task.CompletedTask;
});
await Check("PDF raster dimensions remain bounded for normal and extreme page shapes", () =>
{
    foreach (var (width, height) in new[] { (595d, 842d), (1d, 1_000_000d), (1_000_000d, 1d), (1e300, 1e-300) })
    {
        var size = PdfRenderSize.Calculate(width, height, 2.5);
        Assert(size.Width is >= 1 and <= 4096 && size.Height is >= 1 and <= 4096);
    }
    var normal = PdfRenderSize.Calculate(595, 842, 1);
    Assert(Math.Abs(normal.Width / (double)normal.Height - 595d / 842d) < .002);
    try { PdfRenderSize.Calculate(double.NaN, 10, 1); }
    catch (ArgumentException) { return Task.CompletedTask; }
    throw new Exception("Accepted invalid page size");
});
await Check("bounds streamed PDF bodies even without a Content-Length header", async () =>
{
    using var api = Client(_ =>
    {
        var content = new UnknownLengthContent(new byte[EtikaApiClient.MaxPdfBytes + 1]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    });
    await Throws(413, async () => await api.PdfAsync("oversized"));
});
await Check("caller cancellation reaches an active API request", async () =>
{
    using var api = new EtikaApiClient("https://etika.example.test", new HttpClient(new CancellableHandler()));
    using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
    try { await api.ContextAsync(cancelled.Token); }
    catch (OperationCanceledException) { Assert(cancelled.IsCancellationRequested); return; }
    throw new Exception("Ignored caller cancellation");
});
await Check("rupiah input preserves integer amounts and rejects ambiguous decimals", () =>
{
    Assert(SubmissionValidation.Rupiah("1.250.000") == "1250000");
    Assert(SubmissionValidation.Rupiah("999999999999999") == "999999999999999");
    foreach (var invalid in new[] { "-1", "1.25", "1,25", "1e6", "1.2345", "1000000000000000", "" })
    {
        bool rejected = false;
        try { SubmissionValidation.Rupiah(invalid); } catch (ArgumentException) { rejected = true; }
        Assert(rejected, "Accepted " + invalid);
    }
    return Task.CompletedTask;
});
await Check("upload validation bounds each PDF and rejects disguised files", () =>
{
    SubmissionValidation.Files([new("1", "good.pdf", "%PDF-valid"u8.ToArray())]);
    foreach (var file in new[] { new UploadFile("1", "bad.pdf", "<html>bad"u8.ToArray()),
        new UploadFile("1", "bad.exe", "%PDF-bad"u8.ToArray()), new UploadFile("1", "big.pdf", "%PDF-"u8.ToArray().Concat(new byte[SubmissionValidation.MaxFileBytes]).ToArray()) })
    {
        bool rejected = false;
        try { SubmissionValidation.Files([file]); } catch (ArgumentException) { rejected = true; }
        Assert(rejected);
    }
    return Task.CompletedTask;
});
await Check("multipart submission carries exact fields bearer and PDF without local paths", async () =>
{
    using var api = new EtikaApiClient("https://etika.example.test", new HttpClient(new AsyncStubHandler(async request =>
    {
        Assert(request.RequestUri!.AbsolutePath == "/api/v1/pengajuan");
        Assert(request.Headers.Authorization?.Parameter == "test-token");
        Assert(request.Content!.Headers.ContentType!.MediaType == "multipart/form-data");
        var body = await request.Content.ReadAsStringAsync();
        Assert(body.Contains("request_id") && body.Contains("stable-request-id") && body.Contains("999999999999999"));
        Assert(body.Contains("file[5]") && body.Contains("%PDF-test") && !body.Contains("private-folder"));
        return Json("""{"data":{"id":"123","message":"Terkirim"}}""");
    })));
    api.SetToken("test-token");
    var input = new SubmissionInput("stable-request-id",2026,"11",null,"submit","2026-11-02","08:30","Belanja",null,"999999999999999", "Catatan");
    var result = await api.SubmitAsync(null, input, [new("5", "C:/private-folder/file.pdf", "%PDF-test"u8.ToArray())]);
    Assert(result.Id == "123");
});
await Check("submission validation errors display field messages", async () =>
{
    using var api = Client(_ => Json("""{"message":"Validation failed","errors":{"file.1":["Unggah KAK"],"pagu_rka":["Pagu tidak valid"]}}""", HttpStatusCode.UnprocessableEntity));
    try { await api.SubmissionOptionsAsync(2026, 1, ""); }
    catch (ApiException ex) { Assert(ex.Message.Contains("Unggah KAK") && ex.Message.Contains("Pagu tidak valid")); return; }
    throw new Exception("Expected validation failure");
});
await Check("read-only servers omit submission capabilities safely", async () =>
{
    using var api = Client(_ => Json("""{"data":{"tahun_aktif":2026,"tahun":[]}}"""));
    Assert(!(await api.ContextAsync()).PengajuanEnabled);
});
Console.WriteLine($"{passed} core client checks passed.");

sealed class UnknownLengthContent(byte[] bytes) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    protected override bool TryComputeLength(out long length) { length = 0; return false; }
}
sealed class CancellableHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}

sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken) => Task.FromResult(respond(request));
}

sealed class AsyncStubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken) => respond(request);
}

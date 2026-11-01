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
Console.WriteLine($"{passed} core client checks passed.");

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

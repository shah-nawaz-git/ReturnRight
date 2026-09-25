using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ReturnRight.IntegrationTests;

/// <summary>Minimal valid fixture bytes — enough to satisfy magic-byte checks.</summary>
public static class TestFiles
{
    public static byte[] Pdf { get; } =
        "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n"u8.ToArray();

    public static byte[] Png { get; } =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
        0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
        0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
    ];

    public static byte[] Jpeg { get; } =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
        0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9,
    ];

    public static byte[] ExeBytes { get; } =
        "MZ\x90\x00\x03\x00\x00\x00\x04\x00\x00\x00"u8.ToArray();

    public static ByteArrayContent FormFile(byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return content;
    }
}

public static class JsonRead
{
    public static async Task<JsonDocument> ReadDocument(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    public static async Task<JsonElement> ReadElement(HttpResponseMessage response) =>
        (await ReadDocument(response)).RootElement;

    public static string? ProblemTitle(JsonElement body) =>
        body.TryGetProperty("title", out var title) ? title.GetString() : null;

    public static string? ProblemCode(JsonElement body) =>
        body.TryGetProperty("code", out var code) ? code.GetString() : null;
}

/// <summary>
/// An authenticated test user: its own cookie jar + CSRF token handling.
/// Register/login refresh the CSRF token because antiforgery tokens are bound
/// to the authenticated identity.
/// </summary>
public sealed class TestClient : IDisposable
{
    public const string Password = "CorrectHorse99!";

    public HttpClient Http { get; }
    public string Email { get; }
    public string CsrfToken { get; private set; } = string.Empty;

    private TestClient(HttpClient http, string email)
    {
        Http = http;
        Email = email;
    }

    /// <summary>Creates a cookie-aware client and registers a fresh user.</summary>
    public static async Task<TestClient> RegisterNewUserAsync(ApiFactory factory)
    {
        var http = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        var client = new TestClient(http, $"user_{Guid.NewGuid():N}@test.local");

        await client.RefreshCsrfAsync();
        var response = await client.PostJsonAsync(
            "/api/auth/register", new { email = client.Email, password = Password });
        response.EnsureSuccessStatusCode();
        await client.RefreshCsrfAsync();
        return client;
    }

    public async Task<string> RefreshCsrfAsync()
    {
        using var response = await Http.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        using var document = await JsonRead.ReadDocument(response);
        CsrfToken = document.RootElement.GetProperty("token").GetString()!;
        return CsrfToken;
    }

    public async Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        var response = await PostJsonAsync("/api/auth/login", new { email, password });
        if (response.IsSuccessStatusCode)
        {
            await RefreshCsrfAsync();
        }
        return response;
    }

    public Task<HttpResponseMessage> PostJsonAsync<T>(string url, T body) =>
        SendAsync(HttpMethod.Post, url, JsonContent.Create(body));

    public Task<HttpResponseMessage> PatchJsonAsync<T>(string url, T body) =>
        SendAsync(HttpMethod.Patch, url, JsonContent.Create(body));

    public Task<HttpResponseMessage> PutJsonAsync<T>(string url, T body) =>
        SendAsync(HttpMethod.Put, url, JsonContent.Create(body));

    public Task<HttpResponseMessage> DeleteAsync(string url) =>
        SendAsync(HttpMethod.Delete, url);

    public Task<HttpResponseMessage> DeleteJsonAsync<T>(string url, T body) =>
        SendAsync(HttpMethod.Delete, url, JsonContent.Create(body));

    public Task<HttpResponseMessage> PostFileAsync(
        string url, byte[] bytes, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        form.Add(TestFiles.FormFile(bytes, contentType), "file", fileName);
        return SendAsync(HttpMethod.Post, url, form);
    }

    public Task<HttpResponseMessage> PostFormAsync(
        string url, byte[] bytes, string fileName, string contentType,
        IEnumerable<KeyValuePair<string, string>> fields)
    {
        var form = new MultipartFormDataContent();
        form.Add(TestFiles.FormFile(bytes, contentType), "file", fileName);
        foreach (var (name, value) in fields)
        {
            form.Add(new StringContent(value), name);
        }
        return SendAsync(HttpMethod.Post, url, form);
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (!HttpMethods.IsGet(method.Method) && !HttpMethods.IsHead(method.Method))
        {
            request.Headers.Add("X-CSRF-TOKEN", CsrfToken);
        }
        return await Http.SendAsync(request);
    }

    public void Dispose() => Http.Dispose();
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class AuthTests(ApiFactory factory)
{
    [Fact]
    public async Task Register_signs_in_and_me_returns_user()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);

        var me = await client.Http.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await JsonRead.ReadElement(me);
        Assert.Equal(client.Email, body.GetProperty("email").GetString());
        Assert.True(body.GetProperty("emailRemindersEnabled").GetBoolean());
        Assert.True(body.GetProperty("inAppRemindersEnabled").GetBoolean());
        Assert.Equal(0, body.GetProperty("unreadNotifications").GetInt32());
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);

        var response = await client.LoginAsync(client.Email, "WrongPassword123");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await JsonRead.ReadElement(response);
        Assert.Equal("Email or password is incorrect.", JsonRead.ProblemTitle(body));
    }

    [Fact]
    public async Task Logout_invalidates_session()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);

        var logout = await client.PostJsonAsync("/api/auth/logout", new { });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var me = await client.Http.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Auth_cookie_has_expected_flags()
    {
        var http = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        var email = $"user_{Guid.NewGuid():N}@test.local";

        using var csrfResponse = await http.GetAsync("/api/auth/csrf");
        var csrf = (await JsonRead.ReadElement(csrfResponse))
            .GetProperty("token").GetString();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new { email, password = TestClient.Password }),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        using var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cookies = response.Headers.GetValues("Set-Cookie");
        var auth = Assert.Single(cookies, c => c.StartsWith("rr.auth=", StringComparison.Ordinal));
        Assert.Contains("httponly", auth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", auth, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Duplicate_email_returns_409()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);

        var response = await client.PostJsonAsync(
            "/api/auth/register", new { email = client.Email, password = TestClient.Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await JsonRead.ReadElement(response);
        Assert.Equal(
            "An account with this email already exists. Try logging in instead.",
            JsonRead.ProblemTitle(body));
    }

    [Fact]
    public async Task Weak_password_returns_400()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);

        var response = await client.PostJsonAsync(
            "/api/auth/register",
            new { email = $"user_{Guid.NewGuid():N}@test.local", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await JsonRead.ReadElement(response);
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.Contains("10 characters", errors.ToString());
    }

    [Fact]
    public async Task Me_without_cookie_returns_401()
    {
        using var http = factory.CreateClient();
        var response = await http.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

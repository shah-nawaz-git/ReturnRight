using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class CsrfTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_without_csrf_header_returns_csrf_invalid()
    {
        var http = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        using var csrfResponse = await http.GetAsync("/api/auth/csrf"); // sets rr.csrf cookie

        using var response = await http.PostAsJsonAsync(
            "/api/auth/login", new { email = "a@b.local", password = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await JsonRead.ReadElement(response);
        Assert.Equal("csrf_invalid", JsonRead.ProblemCode(body));
        Assert.Equal(
            "Your session needs to be refreshed. Please try again.",
            JsonRead.ProblemTitle(body));
    }

    [Fact]
    public async Task Login_with_csrf_header_succeeds()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);

        var response = await client.PostJsonAsync(
            "/api/auth/login", new { email = client.Email, password = TestClient.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Token_bound_to_another_session_fails()
    {
        var httpA = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        var httpB = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });

        using var csrfA = await httpA.GetAsync("/api/auth/csrf");
        var tokenA = (await JsonRead.ReadElement(csrfA)).GetProperty("token").GetString();
        using var csrfB = await httpB.GetAsync("/api/auth/csrf"); // give B its own cookie

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new
            {
                email = $"user_{Guid.NewGuid():N}@test.local",
                password = TestClient.Password,
            }),
        };
        request.Headers.Add("X-CSRF-TOKEN", tokenA);
        using var response = await httpB.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await JsonRead.ReadElement(response);
        Assert.Equal("csrf_invalid", JsonRead.ProblemCode(body));
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ReturnRight.IntegrationTests;

[Collection("ApiRateLimit")]
public sealed class RateLimitTests(RateLimitedApiFactory factory)
{
    [Fact]
    public async Task Third_login_attempt_is_rate_limited()
    {
        var http = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        using var csrfResponse = await http.GetAsync("/api/auth/csrf");
        var csrf = (await JsonRead.ReadElement(csrfResponse)).GetProperty("token").GetString();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "nobody@test.local", password = "x" }),
            };
            request.Headers.Add("X-CSRF-TOKEN", csrf);
            using var response = await http.SendAsync(request);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, statuses[0]);
        Assert.Equal(HttpStatusCode.Unauthorized, statuses[1]);
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[2]);
    }
}

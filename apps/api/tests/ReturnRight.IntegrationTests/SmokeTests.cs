using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class SmokeTests
{
    private readonly HttpClient _client;

    public SmokeTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ping_ReturnsPong()
    {
        var response = await _client.GetAsync("/api/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("pong").GetBoolean());
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", json.GetProperty("status").GetString());
    }
}

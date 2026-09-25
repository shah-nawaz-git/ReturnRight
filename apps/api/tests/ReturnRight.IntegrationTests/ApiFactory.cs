using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReturnRight.Api.Email;
using ReturnRight.Api.Extraction;

namespace ReturnRight.IntegrationTests;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();

    public FakeExtractorClient Extractor { get; } = new();

    public FakeEmailSender EmailSender { get; } = new();

    public TestTimeProvider Time { get; } = new();

    public string StoragePath { get; } =
        Path.Combine(Path.GetTempPath(), $"rr_storage_{Guid.NewGuid():N}");

    /// <summary>Extra configuration for specialized factories (e.g. rate limits).</summary>
    protected virtual IReadOnlyDictionary<string, string?>? ConfigOverrides => null;

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        Directory.CreateDirectory(StoragePath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.ConnectionString,
                ["Database:MigrateOnStartup"] = "true",
                ["Seed:Enabled"] = "false",
                ["Storage:RootPath"] = StoragePath,
                // High enough that tests never trip the limiter; RateLimitTests
                // uses its own factory with a tiny permit limit.
                ["RateLimiting:AuthPermitLimit"] = "100000",
                ["RateLimiting:UploadsPermitLimit"] = "100000",
                // The background worker stays off — tests drive
                // /api/dev/reminders/process explicitly.
                ["Reminders:Enabled"] = "false",
                ["Dev:EnableTestEndpoints"] = "true",
            };
            if (ConfigOverrides is not null)
            {
                foreach (var pair in ConfigOverrides)
                {
                    values[pair.Key] = pair.Value;
                }
            }
            config.AddInMemoryCollection(values);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IExtractorClient>();
            services.AddSingleton<IExtractorClient>(Extractor);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    /// <summary>Count of files currently under the test storage root.</summary>
    public int StoredFileCount() =>
        Directory.Exists(StoragePath)
            ? Directory.EnumerateFiles(StoragePath, "*", SearchOption.AllDirectories).Count()
            : 0;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();

        try
        {
            if (Directory.Exists(StoragePath))
            {
                Directory.Delete(StoragePath, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
    }
}

public sealed class RateLimitedApiFactory : ApiFactory
{
    protected override IReadOnlyDictionary<string, string?>? ConfigOverrides { get; } =
        new Dictionary<string, string?> { ["RateLimiting:AuthPermitLimit"] = "2" };
}

[CollectionDefinition("ApiRateLimit")]
public sealed class RateLimitCollection : ICollectionFixture<RateLimitedApiFactory>;

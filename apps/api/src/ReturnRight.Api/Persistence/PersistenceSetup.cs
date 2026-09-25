using Microsoft.EntityFrameworkCore;

namespace ReturnRight.Api.Persistence;

public static class PersistenceSetup
{
    public static WebApplicationBuilder AddReturnRightPersistence(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.")));

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>();

        return builder;
    }

    /// <summary>Run after build: migrate the database (opt-in) and seed demo data (opt-in).</summary>
    public static async Task ApplyStartupTasksAsync(
        this IServiceProvider services, IHostEnvironment environment, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (configuration.GetValue("Database:MigrateOnStartup", false))
        {
            await db.Database.MigrateAsync();
        }
        // DevSeeder itself checks Seed:Enabled and requires Seed:DemoPassword to
        // be set, so it's a no-op unless explicitly configured (e.g. compose).
        await DevSeeder.SeedAsync(scope.ServiceProvider);
    }
}

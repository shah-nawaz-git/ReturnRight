namespace ReturnRight.Api.Features.Intakes;

public static class IntakeSetup
{
    public static IServiceCollection AddReturnRightIntakes(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IntakeCleanupService>();
        if (configuration.GetValue("Intakes:CleanupEnabled", true))
        {
            services.AddHostedService<IntakeCleanupWorker>();
        }
        return services;
    }
}

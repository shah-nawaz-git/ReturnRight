namespace ReturnRight.Api.Features.Reminders;

public static class ReminderSetup
{
    public static IServiceCollection AddReturnRightReminders(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ReminderOptions>(configuration.GetSection("Reminders"));
        services.AddScoped<ReminderScheduler>();
        services.AddScoped<ReminderProcessor>();
        if (configuration.GetValue("Reminders:Enabled", true))
        {
            services.AddHostedService<ReminderWorker>();
        }
        return services;
    }
}

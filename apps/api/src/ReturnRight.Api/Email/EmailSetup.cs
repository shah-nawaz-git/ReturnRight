namespace ReturnRight.Api.Email;

public static class EmailSetup
{
    public static IServiceCollection AddReturnRightEmail(this IServiceCollection services)
    {
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        return services;
    }
}

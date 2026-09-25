namespace ReturnRight.Api.Storage;

public static class StorageSetup
{
    public static IServiceCollection AddReturnRightStorage(this IServiceCollection services)
    {
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        return services;
    }
}

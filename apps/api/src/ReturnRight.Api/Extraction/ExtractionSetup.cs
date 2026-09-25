namespace ReturnRight.Api.Extraction;

public static class ExtractionSetup
{
    public static IServiceCollection AddReturnRightExtraction(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IExtractorClient, HttpExtractorClient>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["Extractor:BaseUrl"] ?? "http://localhost:8000");
            client.Timeout = TimeSpan.FromSeconds(
                configuration.GetValue("Extractor:TimeoutSeconds", 30));
            var sharedSecret = configuration["Extractor:SharedSecret"];
            if (!string.IsNullOrEmpty(sharedSecret))
            {
                client.DefaultRequestHeaders.Add("X-Extractor-Key", sharedSecret);
            }
        });
        return services;
    }
}

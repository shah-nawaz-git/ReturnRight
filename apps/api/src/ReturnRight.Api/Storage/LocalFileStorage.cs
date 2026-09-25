namespace ReturnRight.Api.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration configuration)
    {
        var root = configuration["Storage:RootPath"] ?? "./storage";
        _root = Path.GetFullPath(root);
    }

    public string ResolvePath(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(full, _root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage key escapes the storage root.");
        }
        return full;
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
        return key;
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = ResolvePath(key);
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }
}

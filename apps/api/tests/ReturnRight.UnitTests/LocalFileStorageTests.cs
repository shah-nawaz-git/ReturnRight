using Microsoft.Extensions.Configuration;
using ReturnRight.Api.Storage;

namespace ReturnRight.UnitTests;

public class LocalFileStorageTests : IDisposable
{
    private readonly string _root;

    public LocalFileStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"rr-test-{Guid.NewGuid():N}");
    }

    private LocalFileStorage CreateStorage()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:RootPath"] = _root,
            })
            .Build();
        return new LocalFileStorage(configuration);
    }

    [Fact]
    public async Task Save_open_delete_round_trip()
    {
        var storage = CreateStorage();
        var bytes = "%PDF-1.4 fake"u8.ToArray();

        string key;
        await using (var input = new MemoryStream(bytes))
        {
            key = await storage.SaveAsync(input, ".pdf", CancellationToken.None);
        }

        Assert.EndsWith(".pdf", key);
        Assert.True(File.Exists(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar))));

        await using (var output = await storage.OpenReadAsync(key, CancellationToken.None))
        {
            var read = new byte[bytes.Length];
            var count = await output.ReadAsync(read);
            Assert.Equal(bytes.Length, count);
            Assert.Equal(bytes, read);
        }

        await storage.DeleteAsync(key, CancellationToken.None);
        Assert.False(File.Exists(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar))));

        // Deleting a missing key is a no-op.
        await storage.DeleteAsync(key, CancellationToken.None);
    }

    [Fact]
    public async Task Traversal_key_is_rejected()
    {
        var storage = CreateStorage();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.OpenReadAsync("../outside.pdf", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.DeleteAsync("../outside.pdf", CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

using ZuLms.Infrastructure.Files;

namespace ZuLms.Infrastructure.Tests;

public sealed class DiskFileStorageTests
{
    [Fact]
    public async Task StoresOutsideWebRootAndRejectsTraversal()
    {
        string root = Path.Combine(Path.GetTempPath(), "zu-lms-files-test", Guid.NewGuid().ToString("N"));
        string? previous = Environment.GetEnvironmentVariable("ZU_STORAGE_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("ZU_STORAGE_ROOT", root);
            var storage = new DiskFileStorage();
            byte[] bytes = "%PDF-1.4\n%%EOF"u8.ToArray();
            string relative = await storage.WriteAsync(".pdf", bytes,
                new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero),
                CancellationToken.None);
            Assert.StartsWith("2026/10/", relative);
            Assert.True(File.Exists(Path.Combine(root, relative)));
            await using (var read = await storage.OpenReadAsync(relative, CancellationToken.None))
            {
                using var result = new MemoryStream();
                await read.CopyToAsync(result);
                Assert.Equal(bytes, result.ToArray());
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                storage.OpenReadAsync("../escape.pdf", CancellationToken.None));
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                storage.OpenReadAsync("2026/10/missing.pdf", CancellationToken.None));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ZU_STORAGE_ROOT", previous);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SchoolOS.Application.Common;

namespace SchoolOS.Infrastructure.Storage;

/// <summary>
/// Local-disk <see cref="IFileStorage"/> implementation. The configured root is resolved
/// against the content root when relative; every path is validated to stay inside the root.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options, IHostEnvironment environment)
    {
        var root = options.Value.Root;
        _root = Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(environment.ContentRootPath, root));
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(Stream content, string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var target = File.Create(fullPath);
        await content.CopyToAsync(target, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(relativePath);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string relativePath)
    {
        var combined = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!string.Equals(combined, _root, StringComparison.OrdinalIgnoreCase)
            && !combined.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage path escapes the configured root.");
        }

        return combined;
    }
}

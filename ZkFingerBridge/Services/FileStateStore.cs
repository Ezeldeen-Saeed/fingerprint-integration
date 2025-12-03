using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZkFingerBridge.Configuration;
using ZkFingerBridge.Models;

namespace ZkFingerBridge.Services;

public sealed class FileStateStore : IStateStore
{
    private readonly ILogger<FileStateStore> _logger;
    private readonly string _path;

    public FileStateStore(IHostEnvironment env, IOptions<SyncOptions> options, ILogger<FileStateStore> logger)
    {
        _logger = logger;
        _path = options.Value.StateFilePath
                ?? Path.Combine(env.ContentRootPath, "sync-state.json");
    }

    public async Task<SyncState> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return SyncState.Empty;
        }

        await using var stream = File.OpenRead(_path);
        var state = await JsonSerializer.DeserializeAsync<SyncState>(stream, cancellationToken: cancellationToken);
        return state ?? SyncState.Empty;
    }

    public async Task WriteAsync(SyncState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, state, cancellationToken: cancellationToken);
        _logger.LogInformation("Persisted sync state to {Path}", _path);
    }
}

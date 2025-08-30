using Microsoft.Extensions.Hosting;

namespace FileServer;

public class FileRetentionService : BackgroundService
{
    private readonly Config _config;
    private readonly ILogger<FileRetentionService> _logger;
    private readonly Timer _timer;

    public FileRetentionService(Config config, ILogger<FileRetentionService> logger)
    {
        _config = config;
        _logger = logger;
        
        var intervalMs = TimeSpan.FromHours(_config.Retention.CleanupIntervalHours).TotalMilliseconds;
        _timer = new Timer(ExecuteCleanup, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(intervalMs));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(_config.Retention.CleanupIntervalHours), stoppingToken);
        }
    }

    private async void ExecuteCleanup(object? state)
    {
        try
        {
            await CleanupFilesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file cleanup");
        }
    }

    private async Task CleanupFilesAsync()
    {
        if (!Directory.Exists(_config.DirectoryPath))
        {
            _logger.LogWarning("Directory {DirectoryPath} does not exist, skipping cleanup", _config.DirectoryPath);
            return;
        }

        var now = DateTime.UtcNow;
        var maxAge = TimeSpan.FromDays(_config.Retention.MaxAgeDays);
        var maxSizeBytes = _config.Retention.MaxSizeMB * 1024L * 1024L;

        var files = new List<FileCleanupInfo>();
        long totalSize = 0;

        // Collect file information
        await Task.Run(() =>
        {
            try
            {
                foreach (var filePath in Directory.EnumerateFiles(_config.DirectoryPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        var age = now - fileInfo.LastWriteTimeUtc;
                        
                        files.Add(new FileCleanupInfo
                        {
                            Path = filePath,
                            Size = fileInfo.Length,
                            Age = age,
                            LastModified = fileInfo.LastWriteTimeUtc
                        });
                        
                        totalSize += fileInfo.Length;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get file info for {FilePath}", filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enumerate files in {DirectoryPath}", _config.DirectoryPath);
            }
        });

        // Clean up old files
        var filesToDelete = files.Where(f => f.Age > maxAge).ToArray();
        foreach (var file in filesToDelete)
        {
            await DeleteFileAsync(file.Path);
            totalSize -= file.Size;
        }

        if (filesToDelete.Length > 0)
        {
            _logger.LogInformation("Deleted {Count} files older than {Days} days", 
                filesToDelete.Length, _config.Retention.MaxAgeDays);
        }

        // Clean up files if total size exceeds limit
        if (totalSize > maxSizeBytes)
        {
            var remainingFiles = files.Except(filesToDelete).OrderBy(f => f.LastModified).ToArray();
            var sizeToRemove = totalSize - maxSizeBytes;
            var sizeRemoved = 0L;
            var countRemoved = 0;

            foreach (var file in remainingFiles)
            {
                if (sizeRemoved >= sizeToRemove) break;
                
                await DeleteFileAsync(file.Path);
                sizeRemoved += file.Size;
                countRemoved++;
            }

            if (countRemoved > 0)
            {
                _logger.LogInformation("Deleted {Count} files to reduce directory size by {SizeMB}MB", 
                    countRemoved, sizeRemoved / (1024L * 1024L));
            }
        }
    }

    private async Task DeleteFileAsync(string filePath)
    {
        try
        {
            await Task.Run(() => File.Delete(filePath));
            _logger.LogDebug("Deleted file: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file: {FilePath}", filePath);
        }
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }
}

public record FileCleanupInfo
{
    public required string Path { get; init; }
    public required long Size { get; init; }
    public required TimeSpan Age { get; init; }
    public required DateTime LastModified { get; init; }
}
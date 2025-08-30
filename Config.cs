namespace FileServer;

public class Config
{
    public string DirectoryPath { get; set; } = "/tmp";
    public int Port { get; set; } = 8080;
    public bool EnableHttps { get; set; } = false;
    public string? CertificatePath { get; set; }
    public string? CertificatePassword { get; set; }
    public RetentionSettings Retention { get; set; } = new();
    public UploadSettings Upload { get; set; } = new();
}

public class RetentionSettings
{
    public int MaxAgeDays { get; set; } = 30;
    public int MaxSizeMB { get; set; } = 1000;
    public int CleanupIntervalHours { get; set; } = 1;
}

public class UploadSettings
{
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB
    public string[] AllowedExtensions { get; set; } = [".txt", ".jpg", ".png", ".pdf"];
    public bool EnableUpload { get; set; } = true;
}
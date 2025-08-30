using System.Runtime;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.FileProviders;
using Serilog;
using FileServer;

var builder = WebApplication.CreateBuilder(args);

// Configure configuration sources
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddCommandLine(args);

// Configure Serilog for high-performance logging
builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "logs/fileserver-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            fileSizeLimitBytes: 100 * 1024 * 1024, // 100MB per file
            rollOnFileSizeLimit: true,
            shared: true,
            flushToDiskInterval: TimeSpan.FromSeconds(1)
        );
});

// Bind configuration
var config = new Config();
builder.Configuration.Bind(config);
builder.Services.AddSingleton(config);

// Add services
builder.Services.AddSingleton<DirectoryBrowserService>();
builder.Services.AddHostedService<FileRetentionService>();

// Configure Kestrel for performance
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.ListenAnyIP(config.Port, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        
        if (config.EnableHttps && !string.IsNullOrEmpty(config.CertificatePath))
        {
            listenOptions.UseHttps(config.CertificatePath, config.CertificatePassword);
        }
    });
});

// Set GC for server scenarios
GCSettings.LatencyMode = GCLatencyMode.LowLatency;

// Build the application
var app = builder.Build();

// Validate directory path
if (!Directory.Exists(config.DirectoryPath))
{
    app.Logger.LogError("Directory path does not exist: {DirectoryPath}", config.DirectoryPath);
    return;
}

// Configure static file serving with zero-copy operations
var fileProvider = new PhysicalFileProvider(config.DirectoryPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = fileProvider,
    RequestPath = "/files",
    ServeUnknownFileTypes = true,
    OnPrepareResponse = context =>
    {
        // Enable caching for better performance
        var headers = context.Context.Response.Headers;
        headers.CacheControl = "public,max-age=3600";
        headers.Expires = DateTimeOffset.UtcNow.AddHours(1).ToString("R");
    }
});

// File upload endpoint
app.MapPost("/upload", async (HttpContext context, Config config, ILogger<Program> logger) =>
{
    var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    logger.LogInformation("Upload request from {ClientIP}", clientIp);
    
    if (!config.Upload.EnableUpload)
    {
        logger.LogWarning("Upload attempt rejected - uploads disabled. Client: {ClientIP}", clientIp);
        return Results.BadRequest("File upload is disabled");
    }

    if (!context.Request.HasFormContentType)
    {
        logger.LogWarning("Upload attempt with invalid content type from {ClientIP}", clientIp);
        return Results.BadRequest("Invalid content type");
    }

    var form = await context.Request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    
    if (file == null)
    {
        logger.LogWarning("Upload request without file from {ClientIP}", clientIp);
        return Results.BadRequest("No file provided");
    }

    // Validate file size
    if (file.Length > config.Upload.MaxFileSizeBytes)
    {
        logger.LogWarning("Upload rejected - file too large. File: {FileName}, Size: {FileSize} bytes, Client: {ClientIP}", 
            file.FileName, file.Length, clientIp);
        return Results.BadRequest($"File size exceeds maximum allowed size of {config.Upload.MaxFileSizeBytes / (1024 * 1024)} MB");
    }

    // Validate file extension
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!config.Upload.AllowedExtensions.Contains(extension))
    {
        logger.LogWarning("Upload rejected - invalid file type. File: {FileName}, Extension: {Extension}, Client: {ClientIP}", 
            file.FileName, extension, clientIp);
        return Results.BadRequest($"File type '{extension}' is not allowed");
    }

    // Sanitize filename
    var fileName = Path.GetFileName(file.FileName);
    if (string.IsNullOrWhiteSpace(fileName))
    {
        return Results.BadRequest("Invalid filename");
    }

    var filePath = Path.Combine(config.DirectoryPath, fileName);
    
    // Prevent overwriting existing files
    if (File.Exists(filePath))
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var counter = 1;
        
        do
        {
            fileName = $"{nameWithoutExt}_{counter}{ext}";
            filePath = Path.Combine(config.DirectoryPath, fileName);
            counter++;
        } while (File.Exists(filePath));
    }

    try
    {
        // Zero-copy streaming to disk using pipelines
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await file.CopyToAsync(fileStream);
        
        logger.LogInformation("File uploaded successfully: {FileName} ({Size} bytes)", fileName, file.Length);
        
        return Results.Text($@"{{
  ""FileName"": ""{fileName}"",
  ""Size"": {file.Length},
  ""DownloadUrl"": ""/files/{fileName}""
}}", "application/json", statusCode: 201);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to upload file: {FileName}", fileName);
        return Results.Problem("Failed to save file");
    }
});

// Directory browser endpoint
app.MapGet("/browse/{**path}", async (HttpContext context, DirectoryBrowserService browserService, string? path) =>
{
    var sort = context.Request.Query["sort"].ToString();
    var order = context.Request.Query["order"].ToString();
    
    var html = await browserService.GetHtmlAsync(path ?? "/", sort, order);
    
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(html);
});

app.MapGet("/browse", async (HttpContext context, DirectoryBrowserService browserService) =>
{
    var sort = context.Request.Query["sort"].ToString();
    var order = context.Request.Query["order"].ToString();
    
    var html = await browserService.GetHtmlAsync("/", sort, order);
    
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(html);
});

// Health check endpoint
app.MapGet("/", () => Results.Text($@"{{
  ""Status"": ""Running"",
  ""Directory"": ""{config.DirectoryPath}"",
  ""Port"": {config.Port},
  ""StaticFilesPath"": ""/files"",
  ""BrowsePath"": ""/browse""
}}", "application/json"));

// Log startup info
app.Logger.LogInformation("FileServer starting on port {Port}, serving files from {DirectoryPath}", 
    config.Port, config.DirectoryPath);

app.Run();

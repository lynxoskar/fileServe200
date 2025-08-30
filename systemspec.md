# SystemSpec.md

## Project Overview

This document outlines the system specification for a high-performance webserver built using .NET 10 with Native Ahead-of-Time (AOT) compilation. The server is designed to host files from disk, emphasizing zero-copy operations, low latency, and minimal memory allocations for optimal performance. It will be configurable via CLI and configuration files, deployed as a self-contained native executable without requiring the .NET runtime.

Key performance best practices incorporated (based on .NET 10 guidelines from Microsoft documentation and community resources as of August 2025):
- **Zero-Copy Operations**: Leverage `Span<T>`, `Memory<T>`, and `ReadOnlyMemory<byte>` for buffer handling in I/O operations, avoiding unnecessary data copies. Use `System.IO.Pipelines` for efficient streaming in file serving and uploads.
- **Low Latency**: Optimize Kestrel server with HTTP/2 multiplexing, connection pooling, and minimal middleware. Enable response caching for static files and directory listings where applicable. Use stack-allocated buffers and avoid synchronous I/O.
- **Minimal Memory Allocations**: Employ `ArrayPool<byte>.Shared` for reusable buffers, value types over reference types where possible, and trim unused code via AOT. Avoid LINQ in hot paths; prefer loops with spans. Monitor with `dotnet-trace` and `dotnet-counters` during development.
- **AOT Compatibility**: Ensure all code is AOT-friendly by avoiding heavy reflection, dynamic code generation, or incompatible libraries. Use `<PublishAot>true</PublishAot>` in the project file and test with `dotnet publish -r <rid> --self-contained`.
- **General Optimizations**: Run on Kestrel with thread pool tuning (e.g., via `KestrelServerOptions.ThreadCount`). Use `GCSettings.LatencyMode = GCLatencyMode.LowLatency` for server scenarios. Profile with BenchmarkDotNet for hotspots.

The server will use ASP.NET Core Minimal APIs for routing, with middleware for static files and custom endpoints. Configuration via `Microsoft.Extensions.Configuration` (JSON files and CLI args). Logging with `Microsoft.Extensions.Logging` to file sinks.

## Functional Requirements

1. **Static File Serving**:
   - Serve files from a configurable directory (e.g., via `--dir /path/to/files` or appsettings.json).
   - Support HTTP/1.1, HTTP/2, and HTTPS (with self-signed or provided certs).
   - Zero-copy file transmission using `PhysicalFileProvider` and `SendFileAsync` with pre-allocated buffers.

2. **HTML Directory Browser Endpoint**:
   - Endpoint (e.g., `/browse`) rendering an HTML view of directories.
   - Display file metadata (name, size, created/modified dates, type).
   - Support sorting by name, size, date (query params like `?sort=name&order=asc`).
   - Clickable links to download files or navigate subdirectories.
   - Low-allocation rendering using string builders and spans for HTML generation.

3. **File Retention Configuration**:
   - Configurable policies for automatic file cleanup (e.g., delete files older than X days, or when directory exceeds Y size).
   - Retention rules via config (JSON): e.g., `{ "Retention": { "MaxAgeDays": 30, "MaxSizeMB": 1000 } }`.
   - Background task for periodic cleanup, using `BackgroundService` with timer to minimize allocations.

4. **Instrumentation and Logging**:
   - Use `Microsoft.Extensions.Telemetry` for metrics (e.g., request latency, throughput).
   - Logging to file using `Serilog` or built-in `ILogger` with file sink, configurable levels (e.g., Info for requests, Error for failures).
   - Low-overhead logging: Avoid string interpolations in hot paths; use structured logging.

5. **File Upload via POST**:
   - Endpoint (e.g., `/upload`) accepting multipart/form-data for file creation/addition.
   - Validate uploads (size limits, file types via config).
   - Zero-copy streaming to disk using `IFormFile.CopyToAsync` with pipelines.

Non-Functional Requirements:
- Performance: <10ms latency for file serves, <50MB memory footprint.
- Security: HTTPS enforcement option, directory traversal prevention.
- Configurability: CLI args override config files.
- Deployment: AOT-compiled native executable for Windows/Linux/macOS.

## Architecture

- **Core Framework**: ASP.NET Core 10 WebApplication with Minimal APIs.
- **Server**: Kestrel with HTTP/2 enabled by default.
- **File Handling**: `IFileProvider` for abstraction, `PhysicalFileProvider` for disk access.
- **Configuration**: `IConfiguration` with CommandLine and Json providers.
- **Logging/Metrics**: Integrated via DI.
- **Background Tasks**: `IHostedService` for retention cleanup.
- **AOT Considerations**: All dependencies must be AOT-compatible (e.g., no System.Reflection.Emit).

High-Level Components:
- Program.cs: Entry point, builder setup.
- Config.cs: Model for configurations.
- Services: FileRetentionService (background), DirectoryBrowserService (HTML generation).
- Endpoints: Minimal API routes for browse/upload.

## Implementation Plan

The plan is divided into phases, with larger tasks broken into subtasks. Each phase includes estimated effort (low/medium/high) and dependencies. Use Git for version control, with branches per feature.

### Phase 1: Project Setup and Configuration (Effort: Low)
1. Create a new .NET 10 console app: `dotnet new web -o FileServer`.
2. Enable AOT: Add `<PublishAot>true</PublishAot>` and `<TrimmerDefaultAction>link</TrimmerDefaultAction>` to .csproj.
3. Add dependencies: `dotnet add package Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.CommandLine`, `Microsoft.Extensions.Configuration.Json`, `Microsoft.Extensions.Logging.Console` (for initial logging; switch to file later).
4. Implement basic configuration:
   - Subtask: Define Config class with properties (e.g., string DirectoryPath, int Port, RetentionSettings).
   - Subtask: In Program.cs, use WebApplicationFactory, bind config from args and appsettings.json.
5. Set up Kestrel: Configure HTTP/1.1, HTTP/2, HTTPS (use KestrelServerOptions.Listen with UseHttps).
6. Test: Run `dotnet run --dir /temp` and verify config loads.

### Phase 2: Static File Serving (Effort: Medium, Dep: Phase 1)
1. Add static file middleware: `app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(config.DirectoryPath) })`.
2. Optimize for performance:
   - Subtask: Enable response compression if needed, but focus on zero-copy with `app.Use(async (ctx, next) => { await ctx.Response.SendFileAsync(filePath); })` for custom routes.
   - Subtask: Configure HTTP/2: `options.Protocols = HttpProtocols.Http1AndHttp2;`.
   - Subtask: HTTPS setup: Load cert from config or generate self-signed.
3. Handle configurable directory: Validate path existence on startup.
4. Test Cases:
   - Unit: Mock IFileProvider, assert file exists and serves with 200 OK.
   - Integration: Deploy AOT binary, curl http://localhost:port/file.txt, verify content and latency <10ms.
   - Edge: Non-existent file (404), large file (zero-copy streaming), HTTP/1 vs HTTP/2 throughput.

### Phase 3: HTML Directory Browser Endpoint (Effort: High, Dep: Phase 2)
1. Create DirectoryBrowserService:
   - Subtask: Method to list files/directories using Directory.EnumerateFileSystemEntries with spans for paths.
   - Subtask: Fetch metadata: Use FileInfo for name, size, created/modified (avoid allocations by caching in arrays).
2. Implement sorting:
   - Subtask: Query param parsing (e.g., ctx.Request.Query["sort"]), use switch for name/size/date.
   - Subtask: Sort with Array.Sort on value types to minimize GC.
3. HTML generation:
   - Subtask: Use StringBuilder with pooled arrays for low-allocation HTML table (columns: Name, Size, Created, Download Link).
   - Subtask: Navigation: Breadcrumbs, subdir links.
   - Subtask: Endpoint: `app.MapGet("/browse/{*path}", async (HttpContext ctx, string? path) => { var html = await browserService.GetHtmlAsync(path ?? "/"); ctx.Response.ContentType = "text/html"; await ctx.Response.WriteAsync(html); });`.
4. Optimize: Cache frequent listings if directory is read-only; use MemoryPool for buffers.
5. Test Cases:
   - Unit: Mock file system, assert sorted list matches expected.
   - Integration: Browse /browse, sort by date, click download (redirects to static file).
   - Edge: Empty dir, deep nesting, sort descending, invalid query params.

### Phase 4: File Retention Configuration (Effort: Medium, Dep: Phase 1)
1. Define RetentionSettings class (MaxAgeDays, MaxSizeMB).
2. Implement FileRetentionService as BackgroundService:
   - Subtask: Timer-based ExecuteAsync (e.g., every hour).
   - Subtask: Enumerate files, check age/size with FileInfo.LastWriteTime, delete if exceeds (use async File.DeleteAsync).
   - Subtask: Log deletions, handle concurrency with locks if needed.
3. Integrate: Add service to DI, configure timer interval via config.
4. Test Cases:
   - Unit: Mock timer/files, assert old files deleted.
   - Integration: Create temp files with old dates, run service, verify cleanup.
   - Edge: No files, max size exceeded, permission errors (log but continue).

### Phase 5: Instrumentation and Logging (Effort: Low, Dep: Phase 1)
1. Add logging: `builder.Logging.AddFile("logs.txt")` (use Serilog.Sinks.File for production).
2. Instrumentation: Add Meter for custom metrics (e.g., requests_served, latency_ms).
3. Integrate: Log requests via middleware, errors in catch blocks.
4. Optimize: Use LoggerMessage.Define for allocation-free logging.
5. Test Cases:
   - Unit: Assert log entries on simulated requests.
   - Integration: Trigger requests, check log file for entries; monitor metrics with dotnet-counters.

### Phase 6: File Upload POST Endpoint (Effort: Medium, Dep: Phase 2)
1. Enable form options: `app.Use(async (ctx, next) => { ctx.Request.EnableBuffering(); await next(); });`.
2. Endpoint: `app.MapPost("/upload", async (HttpContext ctx, IFormFile file) => { using var stream = File.Create(Path.Combine(config.DirectoryPath, file.FileName)); await file.CopyToAsync(stream); return Results.Created(); });`.
3. Optimize: Use pipelines for zero-copy: `await file.OpenReadStream().CopyToAsync(stream, bufferSize: 81920);`.
4. Validate: Configurable max size, file types; reject with 400.
5. Test Cases:
   - Unit: Mock IFormFile, assert file saved.
   - Integration: POST with curl -F "file=@test.txt", verify file exists.
   - Edge: Oversize file (413), invalid type, concurrent uploads.

### Phase 7: Full Integration, AOT Testing, and Optimization (Effort: High, Dep: All)
1. Wire everything in Program.cs.
2. Profile: Use BenchmarkDotNet for endpoints, dotnet-trace for allocations.
3. AOT Publish: `dotnet publish -c Release -r linux-x64 --self-contained -p:PublishAot=true`.
4. Subtask: Fix any AOT warnings (e.g., trim analysis).
5. End-to-End Tests: Deploy binary, test all features via curl/Postman.
6. Optimization Iterations: Tune based on profiles (e.g., reduce GC with pools).

## Test Strategy

- **Unit Tests**: xUnit for services (e.g., mock IFileProvider).
- **Integration Tests**: WebApplicationFactory for endpoints.
- **Performance Tests**: BenchmarkDotNet for latency/allocations.
- **AOT Validation**: Run published binary on clean machine, assert no runtime deps.
- Coverage Goal: 80%+ with dotnet test --collect:"XPlat Code Coverage".

## Risks and Mitigations

- AOT Incompatibilities: Test early, fallback to JIT if needed.
- Performance Bottlenecks: Continuous profiling.
- Security: Sanitize paths, limit uploads.

This spec will evolve during implementation. Next: Prototype Phase 1.

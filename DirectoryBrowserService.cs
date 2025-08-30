using System.Buffers;
using System.Text;

namespace FileServer;

public class DirectoryBrowserService
{
    private static readonly ArrayPool<char> CharPool = ArrayPool<char>.Shared;
    private readonly Config _config;

    public DirectoryBrowserService(Config config)
    {
        _config = config;
    }
    
    public Task<string> GetHtmlAsync(string relativePath, string? sort = null, string? order = null)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_config.DirectoryPath, relativePath.TrimStart('/')));
        
        if (!Directory.Exists(fullPath) || !fullPath.StartsWith(_config.DirectoryPath))
        {
            return Task.FromResult(GenerateErrorPage("Directory not found or access denied"));
        }

        var entries = GetDirectoryEntries(fullPath, sort, order);
        return Task.FromResult(GenerateHtml(relativePath, entries, sort, order));
    }

    private DirectoryEntry[] GetDirectoryEntries(string directoryPath, string? sort, string? order)
    {
        var entries = new List<DirectoryEntry>();
        
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directoryPath))
            {
                var info = new FileInfo(entry);
                var isDirectory = (info.Attributes & FileAttributes.Directory) != 0;
                
                entries.Add(new DirectoryEntry
                {
                    Name = Path.GetFileName(entry),
                    FullPath = entry,
                    Size = isDirectory ? -1 : info.Length,
                    LastModified = info.LastWriteTime,
                    IsDirectory = isDirectory
                });
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip inaccessible entries
        }

        SortEntries(entries, sort, order);
        return entries.ToArray();
    }

    private static void SortEntries(List<DirectoryEntry> entries, string? sort, string? order)
    {
        var ascending = !string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
        
        switch (sort?.ToLowerInvariant())
        {
            case "size":
                entries.Sort((a, b) => ascending ? a.Size.CompareTo(b.Size) : b.Size.CompareTo(a.Size));
                break;
            case "date":
                entries.Sort((a, b) => ascending ? a.LastModified.CompareTo(b.LastModified) : b.LastModified.CompareTo(a.LastModified));
                break;
            case "name":
            default:
                entries.Sort((a, b) => ascending ? string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase) : string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase));
                break;
        }
        
        // Always show directories first
        entries.Sort((a, b) => b.IsDirectory.CompareTo(a.IsDirectory));
    }

    private string GenerateHtml(string relativePath, DirectoryEntry[] entries, string? sort, string? order)
    {
        var buffer = CharPool.Rent(4096);
        try
        {
            var html = new StringBuilder(2048);
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head>");
            html.AppendLine("<title>File Browser</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("table { width: 100%; border-collapse: collapse; }");
            html.AppendLine("th, td { padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }");
            html.AppendLine("th { background-color: #f2f2f2; }");
            html.AppendLine("a { text-decoration: none; color: #0066cc; }");
            html.AppendLine("a:hover { text-decoration: underline; }");
            html.AppendLine(".dir { font-weight: bold; }");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");
            
            // Breadcrumbs
            html.AppendLine("<h1>File Browser</h1>");
            GenerateBreadcrumbs(html, relativePath);
            
            // Table header with sorting links
            html.AppendLine("<table>");
            html.AppendLine("<tr>");
            AppendSortableHeader(html, "Name", "name", relativePath, sort, order);
            AppendSortableHeader(html, "Size", "size", relativePath, sort, order);
            AppendSortableHeader(html, "Modified", "date", relativePath, sort, order);
            html.AppendLine("<th>Action</th>");
            html.AppendLine("</tr>");
            
            // Entries
            foreach (var entry in entries)
            {
                html.AppendLine("<tr>");
                
                // Name column
                html.Append("<td>");
                if (entry.IsDirectory)
                {
                    html.Append($"<a href='/browse{relativePath.TrimEnd('/')}/{entry.Name}' class='dir'>📁 {entry.Name}</a>");
                }
                else
                {
                    html.Append($"📄 {entry.Name}");
                }
                html.AppendLine("</td>");
                
                // Size column
                html.Append("<td>");
                if (!entry.IsDirectory)
                {
                    html.Append(FormatSize(entry.Size));
                }
                html.AppendLine("</td>");
                
                // Modified column
                html.AppendLine($"<td>{entry.LastModified:yyyy-MM-dd HH:mm}</td>");
                
                // Action column
                html.Append("<td>");
                if (!entry.IsDirectory)
                {
                    html.Append($"<a href='/files{relativePath.TrimEnd('/')}/{entry.Name}'>Download</a>");
                }
                html.AppendLine("</td>");
                
                html.AppendLine("</tr>");
            }
            
            html.AppendLine("</table>");
            
            // Upload form
            html.AppendLine("<br><h3>Upload File</h3>");
            html.AppendLine("<form action='/upload' method='post' enctype='multipart/form-data'>");
            html.AppendLine("<input type='file' name='file' required>");
            html.AppendLine("<button type='submit'>Upload</button>");
            html.AppendLine("</form>");
            
            html.AppendLine("</body></html>");
            
            return html.ToString();
        }
        finally
        {
            CharPool.Return(buffer);
        }
    }

    private static void GenerateBreadcrumbs(StringBuilder html, string relativePath)
    {
        html.Append("<nav><a href='/browse/'>Root</a>");
        
        var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var currentPath = "";
        
        foreach (var part in parts)
        {
            currentPath += "/" + part;
            html.Append($" / <a href='/browse{currentPath}'>{part}</a>");
        }
        
        html.AppendLine("</nav><br>");
    }

    private static void AppendSortableHeader(StringBuilder html, string displayName, string sortField, string relativePath, string? currentSort, string? currentOrder)
    {
        var newOrder = currentSort == sortField && currentOrder != "desc" ? "desc" : "asc";
        var arrow = currentSort == sortField ? (currentOrder == "desc" ? " ↓" : " ↑") : "";
        
        html.AppendLine($"<th><a href='/browse{relativePath}?sort={sortField}&order={newOrder}'>{displayName}{arrow}</a></th>");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024)} MB";
        return $"{bytes / (1024 * 1024 * 1024)} GB";
    }

    private static string GenerateErrorPage(string message)
    {
        return $@"<!DOCTYPE html>
<html><head><title>Error</title></head>
<body>
<h1>Error</h1>
<p>{message}</p>
<a href='/browse/'>Back to Root</a>
</body></html>";
    }
}

public record DirectoryEntry
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required long Size { get; init; }
    public required DateTime LastModified { get; init; }
    public required bool IsDirectory { get; init; }
}
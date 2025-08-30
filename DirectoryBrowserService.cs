using System.Text;

namespace FileServer;

public class DirectoryBrowserService
{
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
        var html = new StringBuilder(4096);
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head>");
            html.AppendLine("<title>FileServer - Terminal File Browser</title>");
            html.AppendLine("<meta charset=\"UTF-8\">");
            html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine("<style>");
            
            // Clean Terminal CSS Theme
            html.AppendLine(@"
                @import url('https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;600&display=swap');
                
                * {
                    margin: 0;
                    padding: 0;
                    box-sizing: border-box;
                }
                
                body {
                    font-family: 'JetBrains Mono', monospace;
                    background: linear-gradient(135deg, #0a0a0a 0%, #1a1a1a 100%);
                    color: #00ff00;
                    min-height: 100vh;
                    padding: 20px;
                    line-height: 1.6;
                }
                
                h1 {
                    text-align: center;
                    font-size: 1.8rem;
                    font-weight: 600;
                    margin-bottom: 30px;
                    color: #00ff00;
                    border-bottom: 1px solid #333;
                    padding-bottom: 10px;
                }
                
                nav {
                    background: rgba(0, 0, 0, 0.6);
                    padding: 10px 15px;
                    border: 1px solid #333;
                    margin-bottom: 20px;
                    font-size: 0.9rem;
                }
                
                nav a {
                    color: #00ff00;
                    text-decoration: none;
                }
                
                nav a:hover {
                    color: #00ff88;
                }
                
                nav a:not(:last-child)::after {
                    content: ' / ';
                    color: #666;
                    margin: 0 4px;
                }
                
                table {
                    width: 100%;
                    border-collapse: collapse;
                    background: rgba(0, 0, 0, 0.4);
                    border: 1px solid #333;
                }
                
                th {
                    background: #1a1a1a;
                    color: #00ff00;
                    padding: 12px 15px;
                    font-weight: 600;
                    text-align: left;
                    border-bottom: 1px solid #333;
                    font-size: 0.9rem;
                }
                
                th a {
                    color: #00ff00;
                    text-decoration: none;
                    display: block;
                }
                
                th a:hover {
                    color: #00ff88;
                }
                
                td {
                    padding: 10px 15px;
                    border-bottom: 1px solid #222;
                    font-size: 0.9rem;
                }
                
                tr:hover td {
                    background: rgba(0, 255, 0, 0.05);
                }
                
                tr:nth-child(even) td {
                    background: rgba(0, 0, 0, 0.2);
                }
                
                tr:nth-child(even):hover td {
                    background: rgba(0, 255, 0, 0.08);
                }
                
                .dir {
                    font-weight: 600;
                    color: #00ccff !important;
                }
                
                a {
                    color: #00ff00;
                    text-decoration: none;
                }
                
                a:hover {
                    color: #00ff88;
                }
                
                h3 {
                    color: #00ff00;
                    margin: 30px 0 15px 0;
                    font-size: 1.1rem;
                    font-weight: 600;
                    border-bottom: 1px solid #333;
                    padding-bottom: 5px;
                }
                
                form {
                    background: rgba(0, 0, 0, 0.4);
                    padding: 20px;
                    border: 1px solid #333;
                    margin-top: 20px;
                }
                
                input[type='file'] {
                    background: #000;
                    border: 1px solid #333;
                    color: #00ff00;
                    padding: 8px 12px;
                    font-family: 'JetBrains Mono', monospace;
                    margin-right: 15px;
                    font-size: 0.9rem;
                }
                
                input[type='file']:hover {
                    border-color: #00ff00;
                }
                
                button {
                    background: #1a1a1a;
                    border: 1px solid #333;
                    color: #00ff00;
                    padding: 8px 16px;
                    font-family: 'JetBrains Mono', monospace;
                    font-weight: 400;
                    cursor: pointer;
                    font-size: 0.9rem;
                }
                
                button:hover {
                    background: #333;
                    border-color: #00ff00;
                }
                
                /* Responsive design */
                @media (max-width: 768px) {
                    body { padding: 10px; }
                    h1 { font-size: 1.5rem; }
                    table { font-size: 0.8rem; }
                    th, td { padding: 8px; }
                    form { padding: 15px; }
                    button, input[type='file'] { padding: 6px 12px; }
                }
                
                /* Scrollbar styling */
                ::-webkit-scrollbar {
                    width: 8px;
                }
                
                ::-webkit-scrollbar-track {
                    background: #000;
                }
                
                ::-webkit-scrollbar-thumb {
                    background: #333;
                }
                
                ::-webkit-scrollbar-thumb:hover {
                    background: #555;
                }
            ");
            
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
            html.AppendLine($"<input type='hidden' name='path' value='{relativePath}'>");
            html.AppendLine("<input type='file' name='file' required>");
            html.AppendLine("<button type='submit'>Upload</button>");
            html.AppendLine("</form>");
            
            html.AppendLine("</body></html>");
            
            return html.ToString();
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
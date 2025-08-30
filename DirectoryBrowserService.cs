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
            html.AppendLine("<title>FileServer 📁 Synthwave Explorer</title>");
            html.AppendLine("<meta charset=\"UTF-8\">");
            html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine("<style>");
            
            // Synthwave CSS Theme
            html.AppendLine(@"
                @import url('https://fonts.googleapis.com/css2?family=Orbitron:wght@400;700;900&display=swap');
                
                * {
                    margin: 0;
                    padding: 0;
                    box-sizing: border-box;
                }
                
                body {
                    font-family: 'Orbitron', monospace;
                    background: linear-gradient(135deg, #0d0221 0%, #1a0845 25%, #2d1b69 50%, #0d0221 100%);
                    background-size: 400% 400%;
                    animation: synthwaveGradient 15s ease infinite;
                    color: #00ffff;
                    min-height: 100vh;
                    padding: 20px;
                    position: relative;
                    overflow-x: auto;
                }
                
                body::before {
                    content: '';
                    position: fixed;
                    top: 0;
                    left: 0;
                    right: 0;
                    bottom: 0;
                    background: 
                        radial-gradient(circle at 20% 80%, rgba(255, 0, 255, 0.1) 0%, transparent 50%),
                        radial-gradient(circle at 80% 20%, rgba(0, 255, 255, 0.1) 0%, transparent 50%),
                        radial-gradient(circle at 40% 40%, rgba(255, 20, 147, 0.05) 0%, transparent 50%);
                    pointer-events: none;
                    z-index: -1;
                }
                
                @keyframes synthwaveGradient {
                    0% { background-position: 0% 50%; }
                    50% { background-position: 100% 50%; }
                    100% { background-position: 0% 50%; }
                }
                
                h1 {
                    text-align: center;
                    font-size: 2.5rem;
                    font-weight: 900;
                    margin-bottom: 30px;
                    background: linear-gradient(45deg, #ff0080, #00ffff, #ff0080);
                    background-size: 200% 200%;
                    background-clip: text;
                    -webkit-background-clip: text;
                    -webkit-text-fill-color: transparent;
                    animation: synthwaveText 3s ease-in-out infinite;
                    text-shadow: 0 0 30px rgba(255, 0, 128, 0.5);
                }
                
                @keyframes synthwaveText {
                    0%, 100% { background-position: 0% 50%; }
                    50% { background-position: 100% 50%; }
                }
                
                nav {
                    background: rgba(0, 0, 0, 0.4);
                    padding: 15px;
                    border-radius: 10px;
                    border: 1px solid #ff0080;
                    margin-bottom: 25px;
                    box-shadow: 0 0 20px rgba(255, 0, 128, 0.3);
                }
                
                nav a {
                    color: #00ffff;
                    text-decoration: none;
                    font-weight: 600;
                    transition: all 0.3s ease;
                    position: relative;
                }
                
                nav a:hover {
                    color: #ff0080;
                    text-shadow: 0 0 10px currentColor;
                }
                
                nav a:not(:last-child)::after {
                    content: ' → ';
                    color: #ff0080;
                    margin: 0 8px;
                }
                
                table {
                    width: 100%;
                    border-collapse: separate;
                    border-spacing: 0;
                    background: rgba(0, 0, 0, 0.6);
                    border-radius: 15px;
                    overflow: hidden;
                    box-shadow: 0 0 30px rgba(0, 255, 255, 0.2);
                    border: 1px solid #00ffff;
                }
                
                th {
                    background: linear-gradient(135deg, #ff0080, #8000ff);
                    color: white;
                    padding: 15px;
                    font-weight: 700;
                    text-transform: uppercase;
                    letter-spacing: 1px;
                    font-size: 0.9rem;
                    position: relative;
                }
                
                th a {
                    color: white !important;
                    text-decoration: none;
                    display: block;
                    width: 100%;
                    height: 100%;
                    transition: all 0.3s ease;
                }
                
                th a:hover {
                    text-shadow: 0 0 15px rgba(255, 255, 255, 0.8);
                    transform: scale(1.05);
                }
                
                td {
                    padding: 12px 15px;
                    border-bottom: 1px solid rgba(0, 255, 255, 0.2);
                    transition: all 0.3s ease;
                    font-size: 0.95rem;
                }
                
                tr:hover td {
                    background: rgba(255, 0, 128, 0.1);
                    transform: scale(1.01);
                    box-shadow: 0 0 15px rgba(255, 0, 128, 0.3);
                }
                
                tr:nth-child(even) td {
                    background: rgba(0, 0, 0, 0.3);
                }
                
                .dir {
                    font-weight: 700;
                    color: #ffff00 !important;
                    text-shadow: 0 0 10px rgba(255, 255, 0, 0.5);
                }
                
                a {
                    color: #00ffff;
                    text-decoration: none;
                    transition: all 0.3s ease;
                    position: relative;
                }
                
                a:hover {
                    color: #ff0080;
                    text-shadow: 0 0 10px currentColor;
                    transform: translateX(5px);
                }
                
                a:not(.dir):hover::before {
                    content: '→ ';
                    color: #ff0080;
                }
                
                h3 {
                    color: #ff0080;
                    margin: 30px 0 15px 0;
                    font-size: 1.3rem;
                    text-transform: uppercase;
                    letter-spacing: 2px;
                    text-shadow: 0 0 15px rgba(255, 0, 128, 0.6);
                }
                
                form {
                    background: rgba(0, 0, 0, 0.4);
                    padding: 20px;
                    border-radius: 10px;
                    border: 1px solid #00ffff;
                    box-shadow: 0 0 20px rgba(0, 255, 255, 0.3);
                    margin-top: 20px;
                }
                
                input[type='file'] {
                    background: rgba(0, 0, 0, 0.6);
                    border: 2px solid #00ffff;
                    border-radius: 8px;
                    color: #00ffff;
                    padding: 10px;
                    font-family: 'Orbitron', monospace;
                    margin-right: 15px;
                    transition: all 0.3s ease;
                }
                
                input[type='file']:hover {
                    border-color: #ff0080;
                    box-shadow: 0 0 15px rgba(255, 0, 128, 0.4);
                }
                
                button {
                    background: linear-gradient(135deg, #ff0080, #8000ff);
                    border: none;
                    border-radius: 8px;
                    color: white;
                    padding: 12px 25px;
                    font-family: 'Orbitron', monospace;
                    font-weight: 600;
                    text-transform: uppercase;
                    letter-spacing: 1px;
                    cursor: pointer;
                    transition: all 0.3s ease;
                    box-shadow: 0 4px 15px rgba(255, 0, 128, 0.3);
                }
                
                button:hover {
                    transform: translateY(-2px);
                    box-shadow: 0 6px 20px rgba(255, 0, 128, 0.5);
                    filter: brightness(1.2);
                }
                
                button:active {
                    transform: translateY(1px);
                }
                
                /* Responsive design */
                @media (max-width: 768px) {
                    body { padding: 10px; }
                    h1 { font-size: 2rem; }
                    table { font-size: 0.8rem; }
                    th, td { padding: 8px; }
                    form { padding: 15px; }
                    button { padding: 10px 20px; }
                }
                
                /* Scrollbar styling */
                ::-webkit-scrollbar {
                    width: 12px;
                }
                
                ::-webkit-scrollbar-track {
                    background: rgba(0, 0, 0, 0.3);
                    border-radius: 6px;
                }
                
                ::-webkit-scrollbar-thumb {
                    background: linear-gradient(135deg, #ff0080, #00ffff);
                    border-radius: 6px;
                }
                
                ::-webkit-scrollbar-thumb:hover {
                    background: linear-gradient(135deg, #00ffff, #ff0080);
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
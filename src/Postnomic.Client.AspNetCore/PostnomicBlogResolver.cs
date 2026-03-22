using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client.AspNetCore;

/// <summary>
/// Configuration that maps base paths to named blog registrations.
/// </summary>
public class PostnomicBlogResolverOptions
{
    /// <summary>
    /// Maps each registered base path (e.g. <c>"/blog/free"</c>) to its blog name key.
    /// </summary>
    public Dictionary<string, string> BasePathToBlogName { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Resolves the named blog registration for a given HTTP request path.
/// </summary>
public interface IPostnomicBlogResolver
{
    /// <summary>
    /// Returns the blog name for the given request path, or <see langword="null"/>
    /// when the path matches the default (unnamed) blog.
    /// </summary>
    string? ResolveBlogName(string requestPath);
}

internal sealed class PostnomicBlogResolver(IOptions<PostnomicBlogResolverOptions> options) : IPostnomicBlogResolver
{
    public string? ResolveBlogName(string requestPath)
    {
        string? bestMatch = null;
        int bestLength = 0;

        foreach (var (basePath, name) in options.Value.BasePathToBlogName)
        {
            var normalizedBasePath = "/" + basePath.Trim('/');

            if (!requestPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
                continue;

            // Ensure we match at a segment boundary (not a partial segment like /blog/free matching /blog/freebird)
            if (requestPath.Length > normalizedBasePath.Length &&
                requestPath[normalizedBasePath.Length] != '/' &&
                requestPath[normalizedBasePath.Length] != '?')
                continue;

            // Longest-prefix match wins
            if (normalizedBasePath.Length > bestLength)
            {
                bestLength = normalizedBasePath.Length;
                bestMatch = name;
            }
        }

        return bestMatch;
    }
}

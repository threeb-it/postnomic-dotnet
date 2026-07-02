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

    /// <summary>
    /// Maps each registered base path to the <see cref="PostnomicLanguageRouteStyle"/> configured
    /// for that blog, so the resolver can correctly match requests that carry a language segment
    /// (e.g. a <see cref="PostnomicLanguageRouteStyle.Prefix"/>-style <c>/de/blog</c>). Base paths
    /// with no entry here default to <see cref="PostnomicLanguageRouteStyle.Suffix"/>.
    /// </summary>
    public Dictionary<string, PostnomicLanguageRouteStyle> BasePathToLanguageRouteStyle { get; } = new(StringComparer.OrdinalIgnoreCase);
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
            var style = options.Value.BasePathToLanguageRouteStyle
                .GetValueOrDefault(basePath, PostnomicLanguageRouteStyle.Suffix);

            if (!PostnomicRouteBuilder.MatchesBlog(requestPath, basePath, style))
                continue;

            // Longest-prefix match wins
            var normalizedBasePath = "/" + basePath.Trim('/');
            if (normalizedBasePath.Length > bestLength)
            {
                bestLength = normalizedBasePath.Length;
                bestMatch = name;
            }
        }

        return bestMatch;
    }
}

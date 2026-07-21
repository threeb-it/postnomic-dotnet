using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.AspNetCore;

namespace Postnomic.Client.AspNetCore.Tests;

/// <summary>
/// Unit tests for <see cref="IPostnomicBlogResolver"/> and its internal implementation.
/// Verifies correct path-to-blog-name mapping, case insensitivity, prefix matching for
/// longer paths, and null return for unmatched paths.
/// </summary>
public class PostnomicBlogResolverTests
{
    /// <summary>
    /// Creates an <see cref="IPostnomicBlogResolver"/> by registering named blogs via
    /// <see cref="PostnomicAspNetCoreExtensions.AddPostnomicBlog(IServiceCollection, string, Action{PostnomicClientOptions})"/>,
    /// which internally registers the resolver and populates the resolver options.
    /// When custom base paths need to be injected directly (e.g. without leading slash),
    /// uses manual <see cref="PostnomicBlogResolverOptions"/> configuration.
    /// </summary>
    private static IPostnomicBlogResolver CreateResolverViaExtension(
        params (string basePath, string name, string slug)[] blogs)
    {
        var services = new ServiceCollection();

        foreach (var (basePath, name, slug) in blogs)
        {
            services.AddPostnomicBlog(name, options =>
            {
                options.BaseUrl = "https://api.postnomic.com";
                options.ApiKey = $"pk_{name}";
                options.BlogSlug = slug;
                options.BasePath = basePath;
            });
        }

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IPostnomicBlogResolver>();
    }

    /// <summary>
    /// Creates an <see cref="IPostnomicBlogResolver"/> with raw resolver options to test
    /// edge cases like missing leading slashes or trailing slashes. Registers the resolver
    /// via the named blog extension to get the internal type registered, then overrides
    /// the options.
    /// </summary>
    private static IPostnomicBlogResolver CreateResolverWithRawOptions(
        params (string basePath, string name)[] mappings)
    {
        var services = new ServiceCollection();

        // Register at least one named blog to get the resolver type registered
        services.AddPostnomicBlog("__setup__", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_setup";
            options.BlogSlug = "setup";
            options.BasePath = "/__setup__";
        });

        // Override the resolver options with our raw mappings
        services.Configure<PostnomicBlogResolverOptions>(opts =>
        {
            opts.BasePathToBlogName.Clear();
            foreach (var (basePath, name) in mappings)
            {
                opts.BasePathToBlogName[basePath] = name;
            }
        });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IPostnomicBlogResolver>();
    }

    // ── Matching path ──────────────────────────────────────────────────────────

    [Fact]
    public void ResolveBlogName_ReturnsCorrectName_ForMatchingPath()
    {
        // Arrange
        var resolver = CreateResolverViaExtension(("/blog/free", "free", "free-blog"));

        // Act
        var result = resolver.ResolveBlogName("/blog/free");

        // Assert
        Assert.Equal("free", result);
    }

    [Fact]
    public void ResolveBlogName_ReturnsCorrectName_ForMultipleMappings()
    {
        // Arrange
        var resolver = CreateResolverViaExtension(
            ("/blog/free", "free", "free-blog"),
            ("/blog/enterprise", "enterprise", "enterprise-blog"));

        // Act & Assert
        Assert.Equal("free", resolver.ResolveBlogName("/blog/free"));
        Assert.Equal("enterprise", resolver.ResolveBlogName("/blog/enterprise"));
    }

    // ── Unmatched path ─────────────────────────────────────────────────────────

    [Fact]
    public void ResolveBlogName_ReturnsNull_ForUnmatchedPath()
    {
        // Arrange
        var resolver = CreateResolverViaExtension(("/blog/free", "free", "free-blog"));

        // Act
        var result = resolver.ResolveBlogName("/about");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ResolveBlogName_ReturnsNull_WhenNoMappingsConfigured()
    {
        // Arrange
        var resolver = CreateResolverWithRawOptions();

        // Act
        var result = resolver.ResolveBlogName("/blog/anything");

        // Assert
        Assert.Null(result);
    }

    // ── Case-insensitive matching ──────────────────────────────────────────────

    [Fact]
    public void ResolveBlogName_IsCaseInsensitive()
    {
        // Arrange
        var resolver = CreateResolverViaExtension(("/blog/free", "free", "free-blog"));

        // Act & Assert
        Assert.Equal("free", resolver.ResolveBlogName("/Blog/Free"));
        Assert.Equal("free", resolver.ResolveBlogName("/BLOG/FREE"));
        Assert.Equal("free", resolver.ResolveBlogName("/blog/FREE"));
    }

    // ── Prefix matching for longer paths ───────────────────────────────────────

    [Fact]
    public void ResolveBlogName_MatchesLongerPaths_WhenBasePathIsPrefix()
    {
        // Arrange
        var resolver = CreateResolverViaExtension(("/blog/free", "free", "free-blog"));

        // Act
        var result = resolver.ResolveBlogName("/blog/free/post/my-first-post");

        // Assert
        Assert.Equal("free", result);
    }

    [Fact]
    public void ResolveBlogName_MatchesSubPages()
    {
        // Arrange
        var resolver = CreateResolverViaExtension(
            ("/blog/enterprise", "enterprise", "enterprise-blog"));

        // Act & Assert
        Assert.Equal("enterprise", resolver.ResolveBlogName("/blog/enterprise/author/jane-doe"));
        Assert.Equal("enterprise", resolver.ResolveBlogName("/blog/enterprise/post/hello-world"));
    }

    // ── Segment boundary matching ───────────────────────────────────────────────

    [Fact]
    public void ResolveBlogName_DoesNotMatchPartialSegment()
    {
        // Arrange — /blog/free should NOT match /blog/freebird
        var resolver = CreateResolverViaExtension(("/blog/free", "free", "free-blog"));

        // Act
        var result = resolver.ResolveBlogName("/blog/freebird");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ResolveBlogName_MatchesExactPathWithQueryString()
    {
        // Arrange
        var resolver = CreateResolverViaExtension(("/blog/free", "free", "free-blog"));

        // Act
        var result = resolver.ResolveBlogName("/blog/free?tag=csharp");

        // Assert
        Assert.Equal("free", result);
    }

    // ── Longest-prefix match ────────────────────────────────────────────────────

    [Fact]
    public void ResolveBlogName_PicksLongestMatchingBasePath()
    {
        // Arrange — both /blog and /blog/enterprise registered; /blog/enterprise should win
        var resolver = CreateResolverWithRawOptions(
            ("/blog", "default"),
            ("/blog/enterprise", "enterprise"));

        // Act
        var result = resolver.ResolveBlogName("/blog/enterprise/post/hello");

        // Assert
        Assert.Equal("enterprise", result);
    }

    [Fact]
    public void ResolveBlogName_FallsBackToShorterPrefix_WhenLongerDoesNotMatch()
    {
        // Arrange
        var resolver = CreateResolverWithRawOptions(
            ("/blog", "default"),
            ("/blog/enterprise", "enterprise"));

        // Act
        var result = resolver.ResolveBlogName("/blog/free/post/hello");

        // Assert — /blog/enterprise doesn't match, /blog does
        Assert.Equal("default", result);
    }

    // ── Base path normalization ────────────────────────────────────────────────

    [Fact]
    public void ResolveBlogName_NormalizesBasePath_WithoutLeadingSlash()
    {
        // Arrange — base path stored without leading slash; resolver should still match
        var resolver = CreateResolverWithRawOptions(("blog/free", "free"));

        // Act
        var result = resolver.ResolveBlogName("/blog/free");

        // Assert
        Assert.Equal("free", result);
    }

    [Fact]
    public void ResolveBlogName_NormalizesBasePath_WithTrailingSlash()
    {
        // Arrange — base path stored with trailing slash; resolver should still match
        var resolver = CreateResolverWithRawOptions(("/blog/free/", "free"));

        // Act
        var result = resolver.ResolveBlogName("/blog/free");

        // Assert
        Assert.Equal("free", result);
    }

    // ── LanguageRouteStyle.Prefix — lang leads the base path ──────────────────

    [Fact]
    public void ResolveBlogName_MatchesLangPrefixedPath_WhenStyleIsPrefix()
    {
        // Arrange
        var resolver = CreateResolverWithStyle("/blog", "default", PostnomicLanguageRouteStyle.Prefix);

        // Act
        var result = resolver.ResolveBlogName("/de/blog");

        // Assert
        Assert.Equal("default", result);
    }

    [Fact]
    public void ResolveBlogName_MatchesLangPrefixedSubPage_WhenStyleIsPrefix()
    {
        // Arrange
        var resolver = CreateResolverWithStyle("/blog", "default", PostnomicLanguageRouteStyle.Prefix);

        // Act
        var result = resolver.ResolveBlogName("/de/blog/post/hello-world");

        // Assert
        Assert.Equal("default", result);
    }

    [Fact]
    public void ResolveBlogName_MatchesMultiSegmentBasePath_WhenStyleIsPrefix()
    {
        // Arrange — named blogs commonly use multi-segment base paths (e.g. /blog/free)
        var resolver = CreateResolverWithStyle("/blog/free", "free", PostnomicLanguageRouteStyle.Prefix);

        // Act
        var result = resolver.ResolveBlogName("/de/blog/free/post/hello");

        // Assert
        Assert.Equal("free", result);
    }

    [Fact]
    public void ResolveBlogName_ReturnsNull_WhenPrefixStyleLangSegmentIsMissingAndNoBarePathRegistered()
    {
        // Arrange — a completely unrelated path must still not match
        var resolver = CreateResolverWithStyle("/blog", "default", PostnomicLanguageRouteStyle.Prefix);

        // Act
        var result = resolver.ResolveBlogName("/about");

        // Assert
        Assert.Null(result);
    }

    // ── Helpers — style-aware resolver construction ────────────────────────────

    private static IPostnomicBlogResolver CreateResolverWithStyle(
        string basePath, string name, PostnomicLanguageRouteStyle style)
    {
        var services = new ServiceCollection();

        services.AddPostnomicBlog(name, options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = $"pk_{name}";
            options.BlogSlug = name;
            options.BasePath = basePath;
            options.LanguageRouteStyle = style;
        });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IPostnomicBlogResolver>();
    }
}

using FluentAssertions;
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
        result.Should().Be("free");
    }

    [Fact]
    public void ResolveBlogName_ReturnsCorrectName_ForMultipleMappings()
    {
        // Arrange
        var resolver = CreateResolverViaExtension(
            ("/blog/free", "free", "free-blog"),
            ("/blog/enterprise", "enterprise", "enterprise-blog"));

        // Act & Assert
        resolver.ResolveBlogName("/blog/free").Should().Be("free");
        resolver.ResolveBlogName("/blog/enterprise").Should().Be("enterprise");
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
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveBlogName_ReturnsNull_WhenNoMappingsConfigured()
    {
        // Arrange
        var resolver = CreateResolverWithRawOptions();

        // Act
        var result = resolver.ResolveBlogName("/blog/anything");

        // Assert
        result.Should().BeNull();
    }

    // ── Case-insensitive matching ──────────────────────────────────────────────

    [Fact]
    public void ResolveBlogName_IsCaseInsensitive()
    {
        // Arrange
        var resolver = CreateResolverViaExtension(("/blog/free", "free", "free-blog"));

        // Act & Assert
        resolver.ResolveBlogName("/Blog/Free").Should().Be("free");
        resolver.ResolveBlogName("/BLOG/FREE").Should().Be("free");
        resolver.ResolveBlogName("/blog/FREE").Should().Be("free");
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
        result.Should().Be("free");
    }

    [Fact]
    public void ResolveBlogName_MatchesSubPages()
    {
        // Arrange
        var resolver = CreateResolverViaExtension(
            ("/blog/enterprise", "enterprise", "enterprise-blog"));

        // Act & Assert
        resolver.ResolveBlogName("/blog/enterprise/author/jane-doe").Should().Be("enterprise");
        resolver.ResolveBlogName("/blog/enterprise/post/hello-world").Should().Be("enterprise");
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
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveBlogName_MatchesExactPathWithQueryString()
    {
        // Arrange
        var resolver = CreateResolverViaExtension(("/blog/free", "free", "free-blog"));

        // Act
        var result = resolver.ResolveBlogName("/blog/free?tag=csharp");

        // Assert
        result.Should().Be("free");
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
        result.Should().Be("enterprise");
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
        result.Should().Be("default");
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
        result.Should().Be("free");
    }

    [Fact]
    public void ResolveBlogName_NormalizesBasePath_WithTrailingSlash()
    {
        // Arrange — base path stored with trailing slash; resolver should still match
        var resolver = CreateResolverWithRawOptions(("/blog/free/", "free"));

        // Act
        var result = resolver.ResolveBlogName("/blog/free");

        // Assert
        result.Should().Be("free");
    }
}

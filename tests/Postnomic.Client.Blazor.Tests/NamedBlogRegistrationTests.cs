using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postnomic.Client;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Blazor;

namespace Postnomic.Client.Blazor.Tests;

/// <summary>
/// Unit tests for the named (multi-blog) overload of
/// <see cref="PostnomicBlazorExtensions.AddPostnomicBlog(IServiceCollection, string, Action{PostnomicClientOptions})"/>.
/// Verifies that keyed <see cref="IPostnomicBlogService"/> instances are registered and that
/// named registrations coexist with default registrations.
/// </summary>
public class NamedBlogRegistrationTests
{
    // ── Keyed service registration ─────────────────────────────────────────────

    [Fact]
    public void AddPostnomicBlog_Named_RegistersKeyedService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPostnomicBlog("free", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_free";
            options.BlogSlug = "free-blog";
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetKeyedService<IPostnomicBlogService>("free");
        Assert.NotNull(service);
        Assert.IsType<PostnomicBlogService>(service);
    }

    [Fact]
    public void AddPostnomicBlog_Named_ReturnsServiceCollection_ForFluentChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var returned = services.AddPostnomicBlog("free", o => o.BaseUrl = "https://api.example.com");

        // Assert
        Assert.Same(services, returned);
    }

    // ── Multiple named registrations ───────────────────────────────────────────

    [Fact]
    public void AddPostnomicBlog_MultipleNamed_BothResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog("free", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_free";
            options.BlogSlug = "free-blog";
        });
        services.AddPostnomicBlog("enterprise", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_enterprise";
            options.BlogSlug = "enterprise-blog";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var freeService = provider.GetKeyedService<IPostnomicBlogService>("free");
        var enterpriseService = provider.GetKeyedService<IPostnomicBlogService>("enterprise");

        // Assert
        Assert.NotNull(freeService);
        Assert.NotNull(enterpriseService);
        Assert.NotSame(enterpriseService, freeService);
    }

    // ── Named options ──────────────────────────────────────────────────────────

    [Fact]
    public void AddPostnomicBlog_Named_ConfiguresNamedOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog("free", options =>
        {
            options.BaseUrl = "https://api-free.example.com";
            options.ApiKey = "pk_free";
            options.BlogSlug = "free-slug";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var monitor = provider.GetRequiredService<IOptionsMonitor<PostnomicClientOptions>>();
        var options = monitor.Get("free");

        // Assert
        Assert.Equal("https://api-free.example.com", options.BaseUrl);
        Assert.Equal("pk_free", options.ApiKey);
        Assert.Equal("free-slug", options.BlogSlug);
    }

    // ── Named + default coexistence ────────────────────────────────────────────

    [Fact]
    public void AddPostnomicBlog_DefaultAndNamed_BothResolvable()
    {
        // Arrange
        var services = new ServiceCollection();

        // Default (unnamed) registration
        services.AddPostnomicBlog(options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_default";
            options.BlogSlug = "default-blog";
        });

        // Named registration
        services.AddPostnomicBlog("enterprise", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_enterprise";
            options.BlogSlug = "enterprise-blog";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var defaultService = provider.GetService<IPostnomicBlogService>();
        var namedService = provider.GetKeyedService<IPostnomicBlogService>("enterprise");

        // Assert
        Assert.NotNull(defaultService);
        Assert.NotNull(namedService);
        Assert.NotSame(namedService, defaultService);
    }

    // ── Caching decorator for named blogs ──────────────────────────────────────

    [Fact]
    public void AddPostnomicBlog_Named_WithCacheEnabled_AppliesCachingDecorator()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog("cached", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_cached";
            options.BlogSlug = "cached-blog";
            options.Cache = new PostnomicCacheOptions { Enabled = true };
        });

        var provider = services.BuildServiceProvider();

        // Act
        var service = provider.GetKeyedService<IPostnomicBlogService>("cached");

        // Assert — CachingPostnomicBlogService is internal, so verify via the
        // IPostnomicCacheControl interface it implements
        Assert.NotNull(service);
        Assert.IsAssignableFrom<IPostnomicCacheControl>(service);
    }
}

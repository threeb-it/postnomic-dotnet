using FluentAssertions;
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
        service.Should().NotBeNull();
        service.Should().BeOfType<PostnomicBlogService>();
    }

    [Fact]
    public void AddPostnomicBlog_Named_ReturnsServiceCollection_ForFluentChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var returned = services.AddPostnomicBlog("free", o => o.BaseUrl = "https://api.example.com");

        // Assert
        returned.Should().BeSameAs(services);
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
        freeService.Should().NotBeNull();
        enterpriseService.Should().NotBeNull();
        freeService.Should().NotBeSameAs(enterpriseService);
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
        options.BaseUrl.Should().Be("https://api-free.example.com");
        options.ApiKey.Should().Be("pk_free");
        options.BlogSlug.Should().Be("free-slug");
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
        defaultService.Should().NotBeNull();
        namedService.Should().NotBeNull();
        defaultService.Should().NotBeSameAs(namedService);
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
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<IPostnomicCacheControl>();
    }
}

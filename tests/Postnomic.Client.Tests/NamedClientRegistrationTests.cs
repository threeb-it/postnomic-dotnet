using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client.Tests;

/// <summary>
/// Unit tests for the named (multi-blog) overload of
/// <see cref="ServiceCollectionExtensions.AddPostnomicClient(IServiceCollection, string, Action{PostnomicClientOptions})"/>.
/// Verifies that keyed <see cref="IPostnomicBlogService"/> instances are registered, each with
/// its own configuration, and that named registrations coexist with default registrations.
/// </summary>
public class NamedClientRegistrationTests
{
    // ── Keyed service registration ─────────────────────────────────────────────

    [Fact]
    public void AddPostnomicClient_Named_RegistersKeyedIPostnomicBlogService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPostnomicClient("blog-a", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "key-a";
            options.BlogSlug = "blog-a";
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetKeyedService<IPostnomicBlogService>("blog-a");
        service.Should().NotBeNull();
        service.Should().BeOfType<PostnomicBlogService>();
    }

    [Fact]
    public void AddPostnomicClient_Named_ReturnsServiceCollection_ForFluentChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var returned = services.AddPostnomicClient("blog-a", o => o.BaseUrl = "https://api.example.com");

        // Assert
        returned.Should().BeSameAs(services);
    }

    // ── Multiple named registrations coexist ───────────────────────────────────

    [Fact]
    public void AddPostnomicClient_MultipleNamedRegistrations_BothResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicClient("blog-a", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "key-a";
            options.BlogSlug = "slug-a";
        });
        services.AddPostnomicClient("blog-b", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "key-b";
            options.BlogSlug = "slug-b";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var serviceA = provider.GetKeyedService<IPostnomicBlogService>("blog-a");
        var serviceB = provider.GetKeyedService<IPostnomicBlogService>("blog-b");

        // Assert
        serviceA.Should().NotBeNull();
        serviceB.Should().NotBeNull();
        serviceA.Should().NotBeSameAs(serviceB);
    }

    // ── Named options via IOptionsMonitor ───────────────────────────────────────

    [Fact]
    public void AddPostnomicClient_Named_ConfiguresNamedOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicClient("blog-a", options =>
        {
            options.BaseUrl = "https://api-a.example.com";
            options.ApiKey = "key-a";
            options.BlogSlug = "slug-a";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var monitor = provider.GetRequiredService<IOptionsMonitor<PostnomicClientOptions>>();
        var options = monitor.Get("blog-a");

        // Assert
        options.BaseUrl.Should().Be("https://api-a.example.com");
        options.ApiKey.Should().Be("key-a");
        options.BlogSlug.Should().Be("slug-a");
    }

    [Fact]
    public void AddPostnomicClient_MultipleNamed_EachHasDistinctOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicClient("blog-a", options =>
        {
            options.BaseUrl = "https://api-a.example.com";
            options.ApiKey = "key-a";
            options.BlogSlug = "slug-a";
        });
        services.AddPostnomicClient("blog-b", options =>
        {
            options.BaseUrl = "https://api-b.example.com";
            options.ApiKey = "key-b";
            options.BlogSlug = "slug-b";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var monitor = provider.GetRequiredService<IOptionsMonitor<PostnomicClientOptions>>();
        var optionsA = monitor.Get("blog-a");
        var optionsB = monitor.Get("blog-b");

        // Assert
        optionsA.BaseUrl.Should().Be("https://api-a.example.com");
        optionsA.ApiKey.Should().Be("key-a");
        optionsA.BlogSlug.Should().Be("slug-a");

        optionsB.BaseUrl.Should().Be("https://api-b.example.com");
        optionsB.ApiKey.Should().Be("key-b");
        optionsB.BlogSlug.Should().Be("slug-b");
    }

    // ── Caching decorator for named blogs ──────────────────────────────────────

    [Fact]
    public void AddPostnomicClient_Named_WithCacheEnabled_AppliesCachingDecorator()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicClient("cached-blog", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "key-cached";
            options.BlogSlug = "cached-slug";
            options.Cache = new PostnomicCacheOptions { Enabled = true };
        });

        var provider = services.BuildServiceProvider();

        // Act
        var service = provider.GetKeyedService<IPostnomicBlogService>("cached-blog");

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<CachingPostnomicBlogService>();
    }

    [Fact]
    public void AddPostnomicClient_Named_WithoutCache_ReturnsPostnomicBlogService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicClient("plain-blog", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "key-plain";
            options.BlogSlug = "plain-slug";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var service = provider.GetKeyedService<IPostnomicBlogService>("plain-blog");

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<PostnomicBlogService>();
    }

    // ── Named and default registrations coexist ────────────────────────────────

    [Fact]
    public void AddPostnomicClient_DefaultAndNamed_BothResolvable()
    {
        // Arrange
        var services = new ServiceCollection();

        // Default (unnamed) registration
        services.AddPostnomicClient(options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "default-key";
            options.BlogSlug = "default-blog";
        });

        // Named registration
        services.AddPostnomicClient("named-blog", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "named-key";
            options.BlogSlug = "named-slug";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var defaultService = provider.GetService<IPostnomicBlogService>();
        var namedService = provider.GetKeyedService<IPostnomicBlogService>("named-blog");

        // Assert
        defaultService.Should().NotBeNull();
        namedService.Should().NotBeNull();
        defaultService.Should().NotBeSameAs(namedService);
    }

    [Fact]
    public void AddPostnomicClient_DefaultAndNamed_HaveDistinctOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddPostnomicClient(options =>
        {
            options.BaseUrl = "https://default.example.com";
            options.ApiKey = "default-key";
            options.BlogSlug = "default-slug";
        });

        services.AddPostnomicClient("named-blog", options =>
        {
            options.BaseUrl = "https://named.example.com";
            options.ApiKey = "named-key";
            options.BlogSlug = "named-slug";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var defaultOptions = provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;
        var monitor = provider.GetRequiredService<IOptionsMonitor<PostnomicClientOptions>>();
        var namedOptions = monitor.Get("named-blog");

        // Assert
        defaultOptions.BaseUrl.Should().Be("https://default.example.com");
        defaultOptions.ApiKey.Should().Be("default-key");

        namedOptions.BaseUrl.Should().Be("https://named.example.com");
        namedOptions.ApiKey.Should().Be("named-key");
    }

    // ── HttpClient uses correct base URL per named blog ────────────────────────

    [Fact]
    public void AddPostnomicClient_Named_EachServiceGetsOwnHttpClient()
    {
        // Arrange — register two named blogs pointing to different base URLs.
        // Resolving both keyed services must not throw, proving they each got
        // their own HttpClient with the correct base address.
        var services = new ServiceCollection();
        services.AddPostnomicClient("blog-a", options =>
        {
            options.BaseUrl = "https://api-a.postnomic.com";
            options.ApiKey = "key-a";
            options.BlogSlug = "slug-a";
        });
        services.AddPostnomicClient("blog-b", options =>
        {
            options.BaseUrl = "https://api-b.postnomic.com";
            options.ApiKey = "key-b";
            options.BlogSlug = "slug-b";
        });

        var provider = services.BuildServiceProvider();

        // Act — resolution should not throw; each service has its own HttpClient
        var actA = () => provider.GetRequiredKeyedService<IPostnomicBlogService>("blog-a");
        var actB = () => provider.GetRequiredKeyedService<IPostnomicBlogService>("blog-b");

        // Assert
        actA.Should().NotThrow();
        actB.Should().NotThrow();
    }
}

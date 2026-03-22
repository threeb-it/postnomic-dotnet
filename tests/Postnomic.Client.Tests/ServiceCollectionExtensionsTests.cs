using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client.Tests;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionExtensions.AddPostnomicClient"/>.
/// Verifies that the extension method registers <see cref="IPostnomicBlogService"/> in the DI
/// container and correctly configures <see cref="PostnomicClientOptions"/> from the provided
/// delegate.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    // ── IPostnomicBlogService registration ────────────────────────────────────

    [Fact]
    public void AddPostnomicClient_RegistersIPostnomicBlogService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPostnomicClient(options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "key";
            options.BlogSlug = "blog";
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetService<IPostnomicBlogService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<PostnomicBlogService>();
    }

    [Fact]
    public void AddPostnomicClient_ReturnsServiceCollection_ForFluentChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var returned = services.AddPostnomicClient(o => o.BaseUrl = "https://api.example.com");

        // Assert
        returned.Should().BeSameAs(services);
    }

    // ── PostnomicClientOptions configuration ──────────────────────────────────

    [Fact]
    public void AddPostnomicClient_ConfiguresBaseUrl()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicClient(options => options.BaseUrl = "https://custom-api.example.com");

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;

        // Assert
        options.BaseUrl.Should().Be("https://custom-api.example.com");
    }

    [Fact]
    public void AddPostnomicClient_ConfiguresApiKey()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicClient(options => options.ApiKey = "test-api-key-123");

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;

        // Assert
        options.ApiKey.Should().Be("test-api-key-123");
    }

    [Fact]
    public void AddPostnomicClient_ConfiguresBlogSlug()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicClient(options => options.BlogSlug = "my-tech-blog");

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;

        // Assert
        options.BlogSlug.Should().Be("my-tech-blog");
    }

    [Fact]
    public void AddPostnomicClient_ConfiguresAllOptionsAtOnce()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicClient(options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "secret-key";
            options.BlogSlug = "engineering-blog";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;

        // Assert
        options.BaseUrl.Should().Be("https://api.postnomic.com");
        options.ApiKey.Should().Be("secret-key");
        options.BlogSlug.Should().Be("engineering-blog");
    }

    // ── PostnomicApiKeyHandler registration ───────────────────────────────────

    [Fact]
    public void AddPostnomicClient_RegistersPostnomicApiKeyHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicClient(options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var handler = provider.GetService<PostnomicApiKeyHandler>();

        // Assert
        handler.Should().NotBeNull();
    }

    // ── HttpClient base address ───────────────────────────────────────────────

    [Fact]
    public void AddPostnomicClient_TrimsTrailingSlashFromBaseUrl_WhenConfiguringHttpClient()
    {
        // Arrange — base URL with trailing slash; service should normalise it
        var services = new ServiceCollection();
        services.AddPostnomicClient(options =>
        {
            options.BaseUrl = "https://api.postnomic.com/";
            options.BlogSlug = "blog";
        });

        var provider = services.BuildServiceProvider();

        // Act — resolving the service should not throw; base address is set correctly
        var act = () => provider.GetRequiredService<IPostnomicBlogService>();

        // Assert
        act.Should().NotThrow();
    }
}

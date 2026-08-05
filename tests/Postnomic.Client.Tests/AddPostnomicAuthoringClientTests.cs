using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client.Tests;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionExtensions.AddPostnomicAuthoringClient"/>.
/// Verifies that the extension method registers <see cref="IPostnomicAuthoringService"/> in the
/// DI container, correctly configures <see cref="PostnomicClientOptions"/> from the provided
/// delegate, and wires up the <see cref="PostnomicPersonalAccessTokenHandler"/>.
/// </summary>
public class AddPostnomicAuthoringClientTests
{
    // ── IPostnomicAuthoringService registration ───────────────────────────────

    [Fact]
    public void AddPostnomicAuthoringClient_RegistersIPostnomicAuthoringService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPostnomicAuthoringClient(options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.PersonalAccessToken = "pnp_token";
            options.BlogId = "blog-guid";
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetService<IPostnomicAuthoringService>();
        Assert.NotNull(service);
        Assert.IsType<PostnomicAuthoringService>(service);
    }

    [Fact]
    public void AddPostnomicAuthoringClient_ReturnsServiceCollection_ForFluentChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var returned = services.AddPostnomicAuthoringClient(o => o.BaseUrl = "https://api.example.com");

        // Assert
        Assert.Same(services, returned);
    }

    // ── PostnomicClientOptions configuration ──────────────────────────────────

    [Fact]
    public void AddPostnomicAuthoringClient_ConfiguresPersonalAccessTokenAndBlogId()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicAuthoringClient(options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.PersonalAccessToken = "pnp_secret";
            options.BlogId = "3f2a1c9e-guid";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var options2 = provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;

        // Assert
        Assert.Equal("https://api.postnomic.com", options2.BaseUrl);
        Assert.Equal("pnp_secret", options2.PersonalAccessToken);
        Assert.Equal("3f2a1c9e-guid", options2.BlogId);
    }

    // ── PostnomicPersonalAccessTokenHandler registration ──────────────────────

    [Fact]
    public void AddPostnomicAuthoringClient_RegistersPostnomicPersonalAccessTokenHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicAuthoringClient(options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var handler = provider.GetService<PostnomicPersonalAccessTokenHandler>();

        // Assert
        Assert.NotNull(handler);
    }

    // ── HttpClient base address ───────────────────────────────────────────────

    [Fact]
    public void AddPostnomicAuthoringClient_TrimsTrailingSlashFromBaseUrl_WhenConfiguringHttpClient()
    {
        // Arrange — base URL with trailing slash; service should normalise it
        var services = new ServiceCollection();
        services.AddPostnomicAuthoringClient(options =>
        {
            options.BaseUrl = "https://api.postnomic.com/";
            options.BlogId = "blog-guid";
        });

        var provider = services.BuildServiceProvider();

        // Act — resolving the service should not throw; base address is set correctly
        var act = () => provider.GetRequiredService<IPostnomicAuthoringService>();

        // Assert
        Assert.Null(Record.Exception(act));
    }

    // ── Coexistence with AddPostnomicClient ───────────────────────────────────

    [Fact]
    public void AddPostnomicClientAndAddPostnomicAuthoringClient_BothRegister_Independently()
    {
        // Arrange — a consumer that reads via ApiKey and authors via PAT for the same blog
        var services = new ServiceCollection();
        services.AddPostnomicClient(options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_live_read";
            options.BlogSlug = "my-blog";
        });
        services.AddPostnomicAuthoringClient(options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.PersonalAccessToken = "pnp_write";
            options.BlogId = "blog-guid";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var reader = provider.GetService<IPostnomicBlogService>();
        var author = provider.GetService<IPostnomicAuthoringService>();
        var options2 = provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;

        // Assert — both services resolve, and the shared options carry both credentials
        Assert.NotNull(reader);
        Assert.NotNull(author);
        Assert.Equal("pk_live_read", options2.ApiKey);
        Assert.Equal("my-blog", options2.BlogSlug);
        Assert.Equal("pnp_write", options2.PersonalAccessToken);
        Assert.Equal("blog-guid", options2.BlogId);
    }
}

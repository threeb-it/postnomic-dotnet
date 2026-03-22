using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postnomic.Client;
using Postnomic.Client.Abstractions;
using Postnomic.Client.AspNetCore;

namespace Postnomic.Client.AspNetCore.Tests;

/// <summary>
/// Unit tests for the named (multi-blog) overload of
/// <see cref="PostnomicAspNetCoreExtensions.AddPostnomicBlog(IServiceCollection, string, Action{PostnomicClientOptions})"/>.
/// Verifies keyed service registration, resolver options, route conventions, and coexistence
/// with the default (unnamed) registration.
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
            options.BasePath = "/blog/free";
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetKeyedService<IPostnomicBlogService>("free");
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddPostnomicBlog_Named_ReturnsServiceCollection_ForFluentChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var returned = services.AddPostnomicBlog("free", o =>
        {
            o.BaseUrl = "https://api.example.com";
            o.BasePath = "/blog/free";
        });

        // Assert
        returned.Should().BeSameAs(services);
    }

    // ── IPostnomicBlogResolver registration ────────────────────────────────────

    [Fact]
    public void AddPostnomicBlog_Named_RegistersIPostnomicBlogResolver()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog("free", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_free";
            options.BlogSlug = "free-blog";
            options.BasePath = "/blog/free";
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var resolver = provider.GetService<IPostnomicBlogResolver>();
        resolver.Should().NotBeNull();
    }

    // ── PostnomicBlogResolverOptions populated ─────────────────────────────────

    [Fact]
    public void AddPostnomicBlog_Named_PopulatesResolverOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog("free", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_free";
            options.BlogSlug = "free-blog";
            options.BasePath = "/blog/free";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var resolverOptions = provider.GetRequiredService<IOptions<PostnomicBlogResolverOptions>>().Value;

        // Assert
        resolverOptions.BasePathToBlogName.Should().ContainKey("/blog/free");
        resolverOptions.BasePathToBlogName["/blog/free"].Should().Be("free");
    }

    [Fact]
    public void AddPostnomicBlog_MultipleNamed_PopulatesAllResolverOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog("free", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_free";
            options.BlogSlug = "free-blog";
            options.BasePath = "/blog/free";
        });
        services.AddPostnomicBlog("enterprise", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_enterprise";
            options.BlogSlug = "enterprise-blog";
            options.BasePath = "/blog/enterprise";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var resolverOptions = provider.GetRequiredService<IOptions<PostnomicBlogResolverOptions>>().Value;

        // Assert
        resolverOptions.BasePathToBlogName.Should().HaveCount(2);
        resolverOptions.BasePathToBlogName["/blog/free"].Should().Be("free");
        resolverOptions.BasePathToBlogName["/blog/enterprise"].Should().Be("enterprise");
    }

    // ── Multiple named blogs get route conventions ─────────────────────────────

    [Fact]
    public void AddPostnomicBlog_MultipleNamed_RegistersMultipleRouteConventions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog("free", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_free";
            options.BlogSlug = "free-blog";
            options.BasePath = "/blog/free";
        });
        services.AddPostnomicBlog("enterprise", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_enterprise";
            options.BlogSlug = "enterprise-blog";
            options.BasePath = "/blog/enterprise";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var razorPagesOptions = provider.GetRequiredService<IOptions<RazorPagesOptions>>().Value;

        // Assert — each named blog registration adds one route convention
        razorPagesOptions.Conventions
            .OfType<IPageRouteModelConvention>()
            .Should().HaveCount(2);
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
            options.BasePath = "/blog";
        });

        // Named registration
        services.AddPostnomicBlog("enterprise", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_enterprise";
            options.BlogSlug = "enterprise-blog";
            options.BasePath = "/blog/enterprise";
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

    [Fact]
    public void AddPostnomicBlog_DefaultAndNamed_RegistersBothRouteConventions()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddPostnomicBlog(options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_default";
            options.BlogSlug = "default-blog";
            options.BasePath = "/blog";
        });

        services.AddPostnomicBlog("enterprise", options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "pk_enterprise";
            options.BlogSlug = "enterprise-blog";
            options.BasePath = "/blog/enterprise";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var razorPagesOptions = provider.GetRequiredService<IOptions<RazorPagesOptions>>().Value;

        // Assert — both default and named register a route convention
        razorPagesOptions.Conventions
            .OfType<IPageRouteModelConvention>()
            .Should().HaveCount(2);
    }
}

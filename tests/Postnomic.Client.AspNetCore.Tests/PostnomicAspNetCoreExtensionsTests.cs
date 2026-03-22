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
/// Unit tests for <see cref="PostnomicAspNetCoreExtensions"/>.
/// Verifies that <see cref="PostnomicAspNetCoreExtensions.AddPostnomicBlog"/> registers
/// <see cref="IPostnomicBlogService"/> and correctly configures
/// <see cref="PostnomicClientOptions"/> from the supplied delegate.
/// </summary>
public class PostnomicAspNetCoreExtensionsTests
{
    // ── IPostnomicBlogService registration ────────────────────────────────────

    [Fact]
    public void AddPostnomicBlog_RegistersIPostnomicBlogService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPostnomicBlog(options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "test-key";
            options.BlogSlug = "my-blog";
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetService<IPostnomicBlogService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<PostnomicBlogService>();
    }

    [Fact]
    public void AddPostnomicBlog_ReturnsServiceCollection_ForFluentChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var returned = services.AddPostnomicBlog(o => o.BaseUrl = "https://api.example.com");

        // Assert
        returned.Should().BeSameAs(services);
    }

    // ── PostnomicClientOptions configuration ──────────────────────────────────

    [Fact]
    public void AddPostnomicBlog_ConfiguresBaseUrl()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options => options.BaseUrl = "https://custom.example.com");

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;

        // Assert
        options.BaseUrl.Should().Be("https://custom.example.com");
    }

    [Fact]
    public void AddPostnomicBlog_ConfiguresApiKey()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options => options.ApiKey = "razor-pages-key");

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;

        // Assert
        options.ApiKey.Should().Be("razor-pages-key");
    }

    [Fact]
    public void AddPostnomicBlog_ConfiguresBlogSlug()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options => options.BlogSlug = "razor-blog");

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;

        // Assert
        options.BlogSlug.Should().Be("razor-blog");
    }

    [Fact]
    public void AddPostnomicBlog_ConfiguresAllOptionsAtOnce()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options =>
        {
            options.BaseUrl = "https://api.postnomic.com";
            options.ApiKey = "super-secret";
            options.BlogSlug = "engineering";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;

        // Assert
        options.BaseUrl.Should().Be("https://api.postnomic.com");
        options.ApiKey.Should().Be("super-secret");
        options.BlogSlug.Should().Be("engineering");
    }

    // ── PostnomicClientOptions.BasePath configuration ────────────────────────

    [Fact]
    public void AddPostnomicBlog_DefaultsBasePathToBlog()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options =>
        {
            options.BaseUrl = "https://api.example.com";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;

        // Assert
        options.BasePath.Should().Be("/blog");
    }

    [Fact]
    public void AddPostnomicBlog_ConfiguresCustomBasePath()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options =>
        {
            options.BasePath = "/articles";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;

        // Assert
        options.BasePath.Should().Be("/articles");
    }

    // ── MapPostnomicBlog ──────────────────────────────────────────────────────

    [Fact]
    public void MapPostnomicBlog_ReturnsEndpointRouteBuilder_ForFluentChaining()
    {
        // Arrange — use a minimal stub since we only need to verify the return value
        var services = new ServiceCollection();
        services.AddRouting();
        var provider = services.BuildServiceProvider();

        var builder = new StubEndpointRouteBuilder(provider);

        // Act
        var returned = builder.MapPostnomicBlog();

        // Assert
        returned.Should().BeSameAs(builder);
    }

    // ── PostnomicBlogAreaRouteConvention — registration ───────────────────────

    [Fact]
    public void AddPostnomicBlog_RegistersPageRouteModelConvention()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options =>
        {
            options.BaseUrl = "https://api.example.com";
            options.ApiKey = "test-key";
            options.BlogSlug = "my-blog";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var razorPagesOptions = provider
            .GetRequiredService<IOptions<RazorPagesOptions>>().Value;

        // Assert — the Blog area route convention must be registered exactly once
        razorPagesOptions.Conventions
            .OfType<IPageRouteModelConvention>()
            .Should().ContainSingle(
                "AddPostnomicBlog must register exactly one IPageRouteModelConvention");
    }

    // ── PostnomicBlogAreaRouteConvention — Author page routing ────────────────

    [Fact]
    public void PostnomicBlogAreaRouteConvention_MapsAuthorPage_WithDefaultBasePath()
    {
        // Arrange — resolve the convention via AddPostnomicBlog (it is internal, so we
        // reach it through the registered IPageRouteModelConvention on RazorPagesOptions)
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options => options.BasePath = "/blog");
        var provider = services.BuildServiceProvider();

        var convention = provider
            .GetRequiredService<IOptions<RazorPagesOptions>>().Value
            .Conventions
            .OfType<IPageRouteModelConvention>()
            .Single();

        var model = BuildAuthorPageRouteModel();

        // Act
        convention.Apply(model);

        // Assert — convention adds a new selector with the mapped route
        model.Selectors.Should().HaveCount(2);
        model.Selectors[1].AttributeRouteModel!.Template
            .Should().Be("blog/author/{authorSlug}");
    }

    [Fact]
    public void PostnomicBlogAreaRouteConvention_WithCustomBasePath_MapsAuthorPage()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options => options.BasePath = "/articles");
        var provider = services.BuildServiceProvider();

        var convention = provider
            .GetRequiredService<IOptions<RazorPagesOptions>>().Value
            .Conventions
            .OfType<IPageRouteModelConvention>()
            .Single();

        var model = BuildAuthorPageRouteModel();

        // Act
        convention.Apply(model);

        // Assert — convention adds a new selector with the mapped route
        model.Selectors.Should().HaveCount(2);
        model.Selectors[1].AttributeRouteModel!.Template
            .Should().Be("articles/author/{authorSlug}");
    }

    [Fact]
    public void PostnomicBlogAreaRouteConvention_WithBasePathWithoutLeadingSlash_MapsAuthorPage()
    {
        // Arrange — verify the convention trims slashes from basePath on both ends
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options => options.BasePath = "news");
        var provider = services.BuildServiceProvider();

        var convention = provider
            .GetRequiredService<IOptions<RazorPagesOptions>>().Value
            .Conventions
            .OfType<IPageRouteModelConvention>()
            .Single();

        var model = BuildAuthorPageRouteModel();

        // Act
        convention.Apply(model);

        // Assert — convention adds a new selector with the mapped route
        model.Selectors.Should().HaveCount(2);
        model.Selectors[1].AttributeRouteModel!.Template
            .Should().Be("news/author/{authorSlug}");
    }

    [Fact]
    public void PostnomicBlogAreaRouteConvention_IgnoresNonBlogArea()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options => options.BasePath = "/blog");
        var provider = services.BuildServiceProvider();

        var convention = provider
            .GetRequiredService<IOptions<RazorPagesOptions>>().Value
            .Conventions
            .OfType<IPageRouteModelConvention>()
            .Single();

        // Build a model for a page that lives in a different area
        var model = new PageRouteModel(
            relativePath: "/Areas/Other/Pages/Author.cshtml",
            viewEnginePath: "/Author",
            areaName: "Other");
        model.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel { Template = "original" }
        });

        // Act
        convention.Apply(model);

        // Assert — template must be left untouched
        model.Selectors[0].AttributeRouteModel!.Template
            .Should().Be("original");
    }

    [Fact]
    public void PostnomicBlogAreaRouteConvention_IgnoresSelectorWithoutAttributeRouteModel()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options => options.BasePath = "/blog");
        var provider = services.BuildServiceProvider();

        var convention = provider
            .GetRequiredService<IOptions<RazorPagesOptions>>().Value
            .Conventions
            .OfType<IPageRouteModelConvention>()
            .Single();

        // A selector without an AttributeRouteModel must not cause an exception
        var model = new PageRouteModel(
            relativePath: "/Areas/Blog/Pages/Author.cshtml",
            viewEnginePath: "/Author",
            areaName: "Blog");
        model.Selectors.Add(new SelectorModel { AttributeRouteModel = null });

        // Act
        var act = () => convention.Apply(model);

        // Assert — no exception, and the null selector is left intact
        act.Should().NotThrow();
        model.Selectors[0].AttributeRouteModel.Should().BeNull();
    }

    // ── PostnomicBlogAreaRouteConvention — other Blog pages ───────────────────

    [Fact]
    public void PostnomicBlogAreaRouteConvention_MapsIndexPage()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options => options.BasePath = "/blog");
        var provider = services.BuildServiceProvider();

        var convention = provider
            .GetRequiredService<IOptions<RazorPagesOptions>>().Value
            .Conventions
            .OfType<IPageRouteModelConvention>()
            .Single();

        var model = new PageRouteModel(
            relativePath: "/Areas/Blog/Pages/Index.cshtml",
            viewEnginePath: "/Index",
            areaName: "Blog");
        model.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel { Template = "placeholder" }
        });

        // Act
        convention.Apply(model);

        // Assert — convention adds a new selector with the mapped route
        model.Selectors.Should().HaveCount(2);
        model.Selectors[1].AttributeRouteModel!.Template
            .Should().Be("blog");
    }

    [Fact]
    public void PostnomicBlogAreaRouteConvention_MapsPostPage()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options => options.BasePath = "/blog");
        var provider = services.BuildServiceProvider();

        var convention = provider
            .GetRequiredService<IOptions<RazorPagesOptions>>().Value
            .Conventions
            .OfType<IPageRouteModelConvention>()
            .Single();

        var model = new PageRouteModel(
            relativePath: "/Areas/Blog/Pages/Post.cshtml",
            viewEnginePath: "/Post",
            areaName: "Blog");
        model.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel { Template = "placeholder" }
        });

        // Act
        convention.Apply(model);

        // Assert — convention adds a new selector with the mapped route
        model.Selectors.Should().HaveCount(2);
        model.Selectors[1].AttributeRouteModel!.Template
            .Should().Be("blog/post/{postSlug}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="PageRouteModel"/> that represents
    /// <c>/Areas/Blog/Pages/Author.cshtml</c> with a single selector that has a non-null
    /// <see cref="AttributeRouteModel"/> — the minimum required by the convention.
    /// </summary>
    private static PageRouteModel BuildAuthorPageRouteModel()
    {
        var model = new PageRouteModel(
            relativePath: "/Areas/Blog/Pages/Author.cshtml",
            viewEnginePath: "/Author",
            areaName: "Blog");

        model.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel { Template = "placeholder" }
        });

        return model;
    }

    // ── StubEndpointRouteBuilder ──────────────────────────────────────────────

    private sealed class StubEndpointRouteBuilder(IServiceProvider serviceProvider)
        : Microsoft.AspNetCore.Routing.IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;

        public ICollection<Microsoft.AspNetCore.Routing.EndpointDataSource> DataSources { get; } =
            new List<Microsoft.AspNetCore.Routing.EndpointDataSource>();

        public Microsoft.AspNetCore.Builder.IApplicationBuilder CreateApplicationBuilder() =>
            throw new NotImplementedException();
    }
}

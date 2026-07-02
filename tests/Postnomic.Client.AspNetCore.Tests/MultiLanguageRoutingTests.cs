using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.AspNetCore.Areas.Blog.Pages;

namespace Postnomic.Client.AspNetCore.Tests;

/// <summary>
/// Unit tests for the Phase 4b multi-language additions to the ASP.NET Core Client SDK:
/// the <c>/{lang}/</c>-prefixed route selectors added by
/// <see cref="PostnomicBlogAreaRouteConvention"/>, and the <see cref="PostModel.CanonicalUrl"/>
/// / <see cref="PostModel.AlternateLanguageUrls"/> hreflang data.
/// </summary>
public class MultiLanguageRoutingTests
{
    // ── PostnomicBlogAreaRouteConvention — {lang} selectors ───────────────────

    [Fact]
    public void PostnomicBlogAreaRouteConvention_MapsPostPage_WithLanguagePrefixedRoute()
    {
        // Arrange
        var convention = ResolveConvention("/blog");
        var model = BuildPostPageRouteModel();

        // Act
        convention.Apply(model);

        // Assert — both the existing route and the new /{lang}/ route must be present
        var templates = model.Selectors
            .Where(s => s.AttributeRouteModel is not null)
            .Select(s => s.AttributeRouteModel!.Template)
            .ToList();

        templates.Should().Contain("blog/post/{postSlug}");
        templates.Should().Contain("blog/{lang:regex(^[a-z]{2}$)}/post/{postSlug}");
    }

    [Fact]
    public void PostnomicBlogAreaRouteConvention_MapsIndexPage_WithLanguagePrefixedRoute()
    {
        // Arrange
        var convention = ResolveConvention("/blog");
        var model = BuildIndexPageRouteModel();

        // Act
        convention.Apply(model);

        // Assert
        var templates = model.Selectors
            .Where(s => s.AttributeRouteModel is not null)
            .Select(s => s.AttributeRouteModel!.Template)
            .ToList();

        templates.Should().Contain("blog");
        templates.Should().Contain("blog/{lang:regex(^[a-z]{2}$)}");
    }

    [Fact]
    public void PostnomicBlogAreaRouteConvention_MapsAuthorPage_WithLanguagePrefixedRoute()
    {
        // Arrange
        var convention = ResolveConvention("/blog");
        var model = BuildAuthorPageRouteModel();

        // Act
        convention.Apply(model);

        // Assert
        var templates = model.Selectors
            .Where(s => s.AttributeRouteModel is not null)
            .Select(s => s.AttributeRouteModel!.Template)
            .ToList();

        templates.Should().Contain("blog/author/{authorSlug}");
        templates.Should().Contain("blog/{lang:regex(^[a-z]{2}$)}/author/{authorSlug}");
    }

    // ── PostModel — CanonicalUrl / AlternateLanguageUrls ──────────────────────

    [Fact]
    public async Task AlternateLanguageUrls_WhenPostHasAvailableLanguages_ContainsCanonicalAndAlternateUrls()
    {
        // Arrange
        var mock = new Mock<IPostnomicBlogService>();
        mock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Test Blog", Slug = "test-blog" });
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());
        mock.Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPostDetail
            {
                Slug = "slug",
                Title = "Title",
                AuthorName = "Jane Doe",
                PublishedAt = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                Language = "en",
                AvailableLanguages = ["en", "de"]
            });

        var sut = CreateSut(mock);
        sut.PostSlug = "slug";

        // Act
        await sut.OnGetAsync();

        // Assert
        sut.CanonicalUrl.Should().EndWith("/post/slug");
        sut.AlternateLanguageUrls.Should().Contain(("en", "/blog/post/slug"));
        sut.AlternateLanguageUrls.Should().Contain(("de", "/blog/de/post/slug"));
    }

    [Fact]
    public void AlternateLanguageUrls_WhenPostHasNoAvailableLanguages_IsEmpty()
    {
        // Arrange
        var mock = new Mock<IPostnomicBlogService>();
        var sut = CreateSut(mock);
        sut.PostSlug = "slug";

        // Act & Assert — before OnGetAsync runs, Post is null; AvailableLanguages defaults to []
        sut.AlternateLanguageUrls.Should().BeEmpty();
    }

    // ── Helpers — convention resolution ───────────────────────────────────────

    private static IPageRouteModelConvention ResolveConvention(string basePath)
    {
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options => options.BasePath = basePath);
        var provider = services.BuildServiceProvider();

        return provider
            .GetRequiredService<IOptions<RazorPagesOptions>>().Value
            .Conventions
            .OfType<IPageRouteModelConvention>()
            .Single();
    }

    private static PageRouteModel BuildPostPageRouteModel()
    {
        var model = new PageRouteModel(
            relativePath: "/Areas/Blog/Pages/Post.cshtml",
            viewEnginePath: "/Post",
            areaName: "Blog");

        model.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel { Template = "placeholder" }
        });

        return model;
    }

    private static PageRouteModel BuildIndexPageRouteModel()
    {
        var model = new PageRouteModel(
            relativePath: "/Areas/Blog/Pages/Index.cshtml",
            viewEnginePath: "/Index",
            areaName: "Blog");

        model.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel { Template = "placeholder" }
        });

        return model;
    }

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

    // ── Helpers — PostModel construction ──────────────────────────────────────

    private static PostModel CreateSut(Mock<IPostnomicBlogService> mock)
    {
        var resolver = new Mock<IPostnomicBlogResolver>();
        resolver.Setup(r => r.ResolveBlogName(It.IsAny<string>())).Returns((string?)null);
        var model = new PostModel(
            mock.Object,
            Mock.Of<IServiceProvider>(),
            resolver.Object,
            Options.Create(new PostnomicClientOptions { BasePath = "/blog" }),
            Mock.Of<IOptionsMonitor<PostnomicClientOptions>>());

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new PageActionDescriptor(),
            new ModelStateDictionary());

        var urlHelper = new Mock<IUrlHelper>();
        var urlHelperFactory = new Mock<IUrlHelperFactory>();
        urlHelperFactory.Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>())).Returns(urlHelper.Object);

        model.PageContext = new PageContext(actionContext);
        model.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        model.Url = urlHelper.Object;

        return model;
    }
}

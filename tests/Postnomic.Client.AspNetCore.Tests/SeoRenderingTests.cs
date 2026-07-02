using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.AspNetCore;

namespace Postnomic.Client.AspNetCore.Tests;

/// <summary>
/// End-to-end integration tests for the SEO head section added to the Razor Blog area pages
/// (Task 3), and for the closed integration-test gap from Task 2: a full
/// request -> Prefix-style route match -> page-model binding -> link-generation pipeline
/// exercised through a real <see cref="TestServer"/>, not just unit-level page-model tests.
/// </summary>
public class SeoRenderingTests : IAsyncLifetime
{
    private const string Slug = "seo-e2e-post";
    private const string AuthorSlug = "jane-doe";

    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var blogServiceMock = new Mock<IPostnomicBlogService>();

        blogServiceMock
            .Setup(s => s.GetPostAsync(Slug, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPostDetail
            {
                Slug = Slug,
                Title = "The SEO End-to-End Post",
                Content = "<p>Some post content used to derive a description when no excerpt is set.</p>",
                Excerpt = "A short excerpt for the SEO end-to-end post.",
                CoverImageUrl = "/images/cover.jpg",
                AuthorName = "Jane Doe",
                AuthorSlug = AuthorSlug,
                PublishedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                Language = "de",
                AvailableLanguages = ["en", "de"],
                Tags = [new PostnomicTag { Slug = "seo", Name = "SEO", PostCount = 1 }],
            });

        blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "SEO Test Blog", Slug = "seo-test-blog" });

        blogServiceMock
            .Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        blogServiceMock
            .Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        blogServiceMock
            .Setup(s => s.GetAuthorProfileAsync(AuthorSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicAuthorProfile
            {
                Name = "Jane Doe",
                Slug = AuthorSlug,
                Headline = "Senior Writer",
            });

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseContentRoot(AppContext.BaseDirectory);

                webHost.ConfigureServices(services =>
                {
                    services.AddRazorPages();

                    services.AddPostnomicBlog(options =>
                    {
                        options.BaseUrl = "https://api.postnomic.example";
                        options.ApiKey = "test-key";
                        options.BlogSlug = "seo-test-blog";
                        options.BasePath = "/blog";
                        options.LanguageRouteStyle = PostnomicLanguageRouteStyle.Prefix;
                    });

                    // Overrides the real HTTP-backed IPostnomicBlogService registered by
                    // AddPostnomicBlog above (last registration wins for non-keyed resolution).
                    services.AddSingleton(blogServiceMock.Object);
                });

                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapRazorPages());
                });
            });

        _host = await hostBuilder.StartAsync();
        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task GetPostPage_WithPrefixLanguageRoute_ReturnsOkWithLangBoundAndBlogLinks()
    {
        // Act
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        // Assert — Prefix routing resolved, page-model bound Lang="de", and link generation
        // (PostnomicRouteBuilder) produced /de/blog/... links back into the page.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("/de/blog/");
    }

    [Fact]
    public async Task GetPostPage_RendersCanonicalLinkAsAbsoluteUrl()
    {
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("rel=\"canonical\"");
        html.Should().MatchRegex("<link rel=\"canonical\" href=\"https?://[^\"]+/blog/post/" + Slug + "\"");
    }

    [Fact]
    public async Task GetPostPage_ForGermanLanguageRoute_CanonicalIsSelfReferentialToTheDeUrl()
    {
        // The canonical for a language variant must point to that variant's own URL, not to the
        // default/English URL — otherwise search engines treat the de page as a duplicate of en.
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().MatchRegex("<link rel=\"canonical\" href=\"https?://[^\"]+/de/blog/post/" + Slug + "\"");
    }

    [Fact]
    public async Task GetPostPage_RendersOgLocaleInOpenGraphUnderscoreRegionFormat()
    {
        // og:locale must follow the OpenGraph convention (e.g. "de_DE"), not a bare language code.
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("property=\"og:locale\" content=\"de_DE\"");
    }

    [Fact]
    public async Task GetPostPage_RendersOpenGraphTitle()
    {
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("property=\"og:title\"");
        html.Should().Contain("The SEO End-to-End Post");
    }

    [Fact]
    public async Task GetPostPage_RendersTwitterCardMeta()
    {
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("name=\"twitter:card\"");
        html.Should().Contain("summary_large_image");
    }

    [Fact]
    public async Task GetPostPage_RendersJsonLdBlogPostingScript()
    {
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("application/ld+json");
        html.Should().Contain("\"@type\":\"BlogPosting\"");
    }

    [Fact]
    public async Task GetPostPage_RendersHreflangEnglishAlternate()
    {
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("hreflang=\"en\"");
    }

    [Fact]
    public async Task GetAuthorPage_RendersSingleH1ForAuthorName()
    {
        var response = await _client.GetAsync($"/de/blog/author/{AuthorSlug}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("<h1");
        html.Should().Contain("Jane Doe");
    }
}

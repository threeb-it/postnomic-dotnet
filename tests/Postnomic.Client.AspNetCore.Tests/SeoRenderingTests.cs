using System.Net;
using System.Text.RegularExpressions;
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
                // Deliberately Unspecified-kind, mirroring what System.Text.Json produces for the
                // API's zoneless "publishedAt" field — exercises the UTC-normalization fix (see
                // GetPostPage_RendersUtcNormalizedPublishedTimestamps below) end-to-end.
                PublishedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Unspecified),
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
                    services.AddLocalization();
                    services.AddRazorPages().AddViewLocalization();

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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/de/blog/", html);
    }

    [Fact]
    public async Task GetPostPage_RendersCanonicalLinkAsAbsoluteUrl()
    {
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("rel=\"canonical\"", html);
        Assert.Matches("<link rel=\"canonical\" href=\"https?://[^\"]+/blog/post/" + Slug + "\"", html);
    }

    [Fact]
    public async Task GetPostPage_ForGermanLanguageRoute_CanonicalIsSelfReferentialToTheDeUrl()
    {
        // The canonical for a language variant must point to that variant's own URL, not to the
        // default/English URL — otherwise search engines treat the de page as a duplicate of en.
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Matches("<link rel=\"canonical\" href=\"https?://[^\"]+/de/blog/post/" + Slug + "\"", html);
    }

    [Fact]
    public async Task GetPostPage_RendersOgLocaleInOpenGraphUnderscoreRegionFormat()
    {
        // og:locale must follow the OpenGraph convention (e.g. "de_DE"), not a bare language code.
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("property=\"og:locale\" content=\"de_DE\"", html);
    }

    [Fact]
    public async Task GetPostPage_RendersOpenGraphTitle()
    {
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("property=\"og:title\"", html);
        Assert.Contains("The SEO End-to-End Post", html);
    }

    [Fact]
    public async Task GetPostPage_RendersTwitterCardMeta()
    {
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("name=\"twitter:card\"", html);
        Assert.Contains("summary_large_image", html);
    }

    [Fact]
    public async Task GetPostPage_RendersJsonLdBlogPostingScript()
    {
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("application/ld+json", html);
        Assert.Contains("\"@type\":\"BlogPosting\"", html);
    }

    [Fact]
    public async Task GetPostPage_RendersHreflangEnglishAlternate()
    {
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("hreflang=\"en\"", html);
    }

    [Fact]
    public async Task GetPostPage_ForGermanAndEnglishLanguageRoutes_XDefaultIsTheSameDefaultLanguageUrl()
    {
        // AvailableLanguages = ["en", "de"] => "en" is the default language, so x-default must be
        // the /en/... English URL on BOTH the de and the en page — not each page's own canonical
        // — otherwise Google sees a different x-default per page in the cluster. Under Prefix
        // routing there is no bare /blog/... route (only /{lang}/blog/...), so x-default must
        // carry the "en" language segment rather than being bare, or it would 404.
        var deResponse = await _client.GetAsync($"/de/blog/post/{Slug}");
        var deHtml = await deResponse.Content.ReadAsStringAsync();
        var enResponse = await _client.GetAsync($"/en/blog/post/{Slug}");
        var enHtml = await enResponse.Content.ReadAsStringAsync();

        var xDefaultRegex = new Regex("hreflang=\"x-default\" href=\"([^\"]+)\"");
        var deXDefault = xDefaultRegex.Match(deHtml).Groups[1].Value;
        var enXDefault = xDefaultRegex.Match(enHtml).Groups[1].Value;

        Assert.False(string.IsNullOrEmpty(deXDefault));
        Assert.Equal(enXDefault, deXDefault);
        Assert.Matches($"https?://[^/]+/en/blog/post/{Slug}$", deXDefault);
    }

    [Fact]
    public async Task GetPostPage_PrefixStyle_NoHreflangAlternateNorXDefaultIsABareBlogUrl()
    {
        // Guard test (elimination of the "bare URL in Prefix mode -> 404" bug class): under Prefix
        // routing, only /{lang}/blog/... routes are registered — a bare /blog/post/{slug} 404s.
        // Every hreflang alternate (including x-default) rendered for either language variant of
        // this post must therefore be language-prefixed.
        var deResponse = await _client.GetAsync($"/de/blog/post/{Slug}");
        var deHtml = await deResponse.Content.ReadAsStringAsync();

        var hrefs = Regex.Matches(deHtml, "hreflang=\"[^\"]+\" href=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(hrefs);
        Assert.All(hrefs, href => Assert.Matches("https?://[^/]+/[a-z]{2}/blog/post/" + Slug + "$", href));
        Assert.DoesNotContain(hrefs, href => Regex.IsMatch(href, "https?://[^/]+/blog/post/" + Slug + "$"));
    }

    [Fact]
    public async Task GetPostPage_RendersUtcNormalizedPublishedTimestamps()
    {
        // The mocked post's PublishedAt is DateTimeKind.Unspecified (see InitializeAsync);
        // article:published_time and the JSON-LD datePublished must still carry a UTC "Z"
        // designator instead of a zoneless timestamp, consistent with the sitemap/RSS date
        // normalization covered by FeedDateNormalizationTests.
        var response = await _client.GetAsync($"/de/blog/post/{Slug}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("property=\"article:published_time\" content=\"2025-06-01T00:00:00.0000000Z\"", html);
        Assert.Contains("\"datePublished\":\"2025-06-01T00:00:00.0000000Z\"", html);
    }

    [Fact]
    public async Task GetAuthorPage_RendersSingleH1ForAuthorName()
    {
        var response = await _client.GetAsync($"/de/blog/author/{AuthorSlug}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<h1", html);
        Assert.Contains("Jane Doe", html);
    }
}

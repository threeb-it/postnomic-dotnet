using System.Net;
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
/// End-to-end integration tests for the <c>sitemap.xml</c> and <c>rss.xml</c> endpoints mapped
/// by <see cref="PostnomicAspNetCoreExtensions.MapPostnomicBlog"/> (Task 4), exercised through a
/// real <see cref="TestServer"/> with a mocked <see cref="IPostnomicBlogService"/>, mirroring the
/// pattern used by <c>SeoRenderingTests</c>.
/// </summary>
public class FeedEndpointTests : IAsyncLifetime
{
    private const string Slug = "feed-e2e-post";

    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var blogServiceMock = new Mock<IPostnomicBlogService>();

        blogServiceMock
            .Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int pageSize, string? _, string? _, string? _, string? _, string? _, CancellationToken _) =>
                new PostnomicPagedResult<PostnomicPostSummary>
                {
                    Items = page == 1
                        ?
                        [
                            new PostnomicPostSummary
                            {
                                Slug = Slug,
                                Title = "The Feed End-to-End Post",
                                Excerpt = "A short excerpt for the feed end-to-end post.",
                                AuthorName = "Jane Doe",
                                AuthorSlug = "jane-doe",
                                PublishedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                                Language = "de",
                                AvailableLanguages = ["en", "de"],
                            },
                        ]
                        : [],
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = 1,
                    TotalPages = 1,
                });

        blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo
            {
                Name = "Feed Test Blog",
                Slug = "feed-test-blog",
                Description = "A blog used to test feed endpoints.",
            });

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseContentRoot(AppContext.BaseDirectory);

                webHost.ConfigureServices(services =>
                {
                    services.AddRazorPages();
                    services.AddRouting();

                    services.AddPostnomicBlog(options =>
                    {
                        options.BaseUrl = "https://api.postnomic.example";
                        options.ApiKey = "test-key";
                        options.BlogSlug = "feed-test-blog";
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
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapRazorPages();
                        endpoints.MapPostnomicBlog();
                        endpoints.MapPostnomicRobots();
                    });
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

    // ── sitemap.xml ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSitemap_ReturnsOkWithXmlContentType()
    {
        var response = await _client.GetAsync("/blog/sitemap.xml");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task GetSitemap_ContainsUrlsetElement()
    {
        var response = await _client.GetAsync("/blog/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Contains("<urlset", xml);
    }

    [Fact]
    public async Task GetSitemap_ContainsPostLoc_WithPrefixStyleAbsoluteUrl()
    {
        // The blog is registered with LanguageRouteStyle.Prefix and the mocked post has
        // Language = "de", so its canonical loc must be the /de/blog/post/{slug} URL.
        var response = await _client.GetAsync("/blog/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Matches($"<loc>https?://[^<]+/de/blog/post/{Slug}</loc>", xml);
    }

    [Fact]
    public async Task GetSitemap_ContainsHreflangAlternateLink_ForEnglish()
    {
        var response = await _client.GetAsync("/blog/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Contains("hreflang=\"en\"", xml);
        Assert.Matches($"href=\"https?://[^\"]+/en/blog/post/{Slug}\"", xml);
    }

    [Fact]
    public async Task GetSitemap_ContainsHreflangAlternateLink_ForGerman()
    {
        var response = await _client.GetAsync("/blog/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Contains("hreflang=\"de\"", xml);
    }

    // ── rss.xml ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRss_ReturnsOkWithRssContentType()
    {
        var response = await _client.GetAsync("/blog/rss.xml");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/rss+xml", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task GetRss_ContainsRssElement()
    {
        var response = await _client.GetAsync("/blog/rss.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Contains("<rss", xml);
    }

    [Fact]
    public async Task GetRss_ContainsItemWithAbsoluteLink()
    {
        var response = await _client.GetAsync("/blog/rss.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Contains("<item>", xml);
        Assert.Matches($"<link>https?://[^<]+/de/blog/post/{Slug}</link>", xml);
    }

    [Fact]
    public async Task GetRss_ItemContainsTitleAndPubDate()
    {
        var response = await _client.GetAsync("/blog/rss.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Contains("The Feed End-to-End Post", xml);
        Assert.Contains("<pubDate>", xml);
    }

    [Fact]
    public async Task GetRss_ChannelUsesBlogNameAsTitle()
    {
        var response = await _client.GetAsync("/blog/rss.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Contains("<title>Feed Test Blog</title>", xml);
    }

    // ── robots.txt (opt-in helper) ─────────────────────────────────────────────

    [Fact]
    public async Task GetRobots_ReferencesRegisteredBlogSitemap()
    {
        var response = await _client.GetAsync("/robots.txt");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("Sitemap: https?://[^\\s]+/blog/sitemap.xml", text);
    }
}

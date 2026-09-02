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
/// Regression coverage for a host-timezone bug in <see cref="PostnomicFeeds"/>: the live
/// Postnomic API returns <c>publishedAt</c> without a trailing "Z"/offset (e.g.
/// <c>"2026-07-02T00:00:00"</c>), so System.Text.Json deserializes it as
/// <see cref="DateTimeKind.Unspecified"/>. Calling <c>.ToUniversalTime()</c> directly on an
/// Unspecified value treats it as local time, so on a non-UTC host the emitted RSS
/// <c>&lt;pubDate&gt;</c> / sitemap <c>&lt;lastmod&gt;</c> silently shifts by the host's UTC
/// offset, even though the API's timestamps are always already UTC. These assertions pin an
/// Unspecified <see cref="DateTime"/> and expect it to be rendered as if it were UTC, which must
/// hold true no matter what timezone the test machine (or CI runner) is set to.
/// </summary>
public class FeedDateNormalizationTests : IAsyncLifetime
{
    private const string Slug = "date-normalization-post";

    // Deliberately DateTimeKind.Unspecified, mirroring what System.Text.Json produces when the
    // API omits a UTC offset. 2026-07-02 is a Thursday.
    private static readonly DateTime UnspecifiedPublishedAt = new(2026, 7, 2, 0, 0, 0, DateTimeKind.Unspecified);

    private IHost _host = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
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
                                Title = "Date Normalization Post",
                                Excerpt = "An excerpt used to test feed date normalization.",
                                AuthorName = "Jane Doe",
                                AuthorSlug = "jane-doe",
                                PublishedAt = UnspecifiedPublishedAt,
                                Language = "en",
                                AvailableLanguages = ["en"],
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
                Name = "Date Normalization Blog",
                Slug = "date-normalization-blog",
                Description = "A blog used to test feed date normalization.",
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
                        options.BlogSlug = "date-normalization-blog";
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
                    });
                });
            });

        _host = await hostBuilder.StartAsync();
        _client = _host.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task GetRss_UnspecifiedPublishedAt_IsRenderedAsUtc_RegardlessOfHostTimezone()
    {
        var response = await _client.GetAsync("/blog/rss.xml", TestContext.Current.CancellationToken);
        var xml = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // RFC-822 rendering of 2026-07-02T00:00:00 interpreted as UTC. If Unspecified were
        // wrongly treated as local time, this would shift by the host's UTC offset instead.
        Assert.Contains("<pubDate>Thu, 02 Jul 2026 00:00:00 GMT</pubDate>", xml);
    }

    [Fact]
    public async Task GetSitemap_UnspecifiedPublishedAt_IsRenderedAsUtc_RegardlessOfHostTimezone()
    {
        var response = await _client.GetAsync("/blog/sitemap.xml", TestContext.Current.CancellationToken);
        var xml = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("<lastmod>2026-07-02</lastmod>", xml);
    }
}

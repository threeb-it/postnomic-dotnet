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
/// Regression coverage for a final-review finding in <see cref="Postnomic.Client.AspNetCore.Seo.PostnomicFeeds"/>:
/// under <see cref="PostnomicLanguageRouteStyle.Prefix"/>, only <c>{basePath}/{lang}</c> routes
/// are registered (see <c>PostnomicBlogAreaRouteConvention</c>) — there is no bare
/// <c>{basePath}</c> route. Advertising the bare <c>/blog</c> URL as the sitemap index
/// <c>&lt;loc&gt;</c> or the RSS channel <c>&lt;link&gt;</c> therefore pointed crawlers at a 404.
/// The fix builds that URL with the blog's inferred default language under Prefix, while leaving
/// Suffix/None (where a bare <c>/blog</c> route IS valid) unchanged.
/// </summary>
public class FeedIndexLanguageTests
{
    private static async Task<(IHost Host, HttpClient Client)> StartHostAsync(
        PostnomicLanguageRouteStyle style, string postLanguage)
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
                                Slug = "index-lang-post",
                                Title = "Index Language Post",
                                Excerpt = "An excerpt used to test the index URL's inferred language.",
                                AuthorName = "Jane Doe",
                                AuthorSlug = "jane-doe",
                                PublishedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                                Language = postLanguage,
                                AvailableLanguages = [postLanguage],
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
                Name = "Index Language Blog",
                Slug = "index-language-blog",
                Description = "A blog used to test the feed index URL's inferred language.",
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
                        options.BlogSlug = "index-language-blog";
                        options.BasePath = "/blog";
                        options.LanguageRouteStyle = style;
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

        var host = await hostBuilder.StartAsync();
        return (host, host.GetTestClient());
    }

    // ── Prefix: index URL must be language-prefixed (no bare-route 404) ───────────────────────

    [Fact]
    public async Task GetSitemap_PrefixStyle_IndexLoc_IsLanguagePrefixed_NotBareBlog()
    {
        var (host, client) = await StartHostAsync(PostnomicLanguageRouteStyle.Prefix, postLanguage: "de");
        using var _ = host;

        var response = await client.GetAsync("/blog/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The index <loc> (as opposed to the post's own <loc>, which carries a /post/{slug} tail)
        // must be the real, routable /de/blog URL — a bare /blog <loc> would 404 under Prefix.
        Assert.Matches("<loc>https?://[^<]+/de/blog</loc>", xml);
    }

    [Fact]
    public async Task GetRss_PrefixStyle_ChannelLink_IsLanguagePrefixed_NotBareBlog()
    {
        var (host, client) = await StartHostAsync(PostnomicLanguageRouteStyle.Prefix, postLanguage: "de");
        using var _ = host;

        var response = await client.GetAsync("/blog/rss.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("<link>https?://[^<]+/de/blog</link>", xml);
    }

    // ── Suffix: bare /blog index URL is still correct (a real route) — unchanged behavior ─────

    [Fact]
    public async Task GetSitemap_SuffixStyle_IndexLoc_StaysBareBlog()
    {
        var (host, client) = await StartHostAsync(PostnomicLanguageRouteStyle.Suffix, postLanguage: "en");
        using var _ = host;

        var response = await client.GetAsync("/blog/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Matches("<loc>https?://[^<]+/blog</loc>", xml);
    }

    [Fact]
    public async Task GetRss_SuffixStyle_ChannelLink_StaysBareBlog()
    {
        var (host, client) = await StartHostAsync(PostnomicLanguageRouteStyle.Suffix, postLanguage: "en");
        using var _ = host;

        var response = await client.GetAsync("/blog/rss.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Matches("<link>https?://[^<]+/blog</link>", xml);
    }
}

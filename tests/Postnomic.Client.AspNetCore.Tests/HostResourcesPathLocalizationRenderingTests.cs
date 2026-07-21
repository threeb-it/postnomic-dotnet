using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.AspNetCore;

namespace Postnomic.Client.AspNetCore.Tests;

/// <summary>
/// Reproduces a production bug seen on a real consuming site (ThreeBIT): the Blog Area rendered
/// raw resource keys (<c>ReadMore</c>, <c>TopCommented</c>, <c>Search</c>, ...) instead of
/// translated strings, for BOTH cultures.
/// </summary>
/// <remarks>
/// <para>
/// Root cause: <c>PostnomicViewLocalizer</c> previously resolved Blog Area resources via the
/// injected HOST <c>IHtmlLocalizerFactory</c>. That factory is backed by the host's
/// <c>ResourceManagerStringLocalizerFactory</c>, configured with the HOST's
/// <c>LocalizationOptions.ResourcesPath</c>. A host that calls
/// <c>services.AddLocalization(o =&gt; o.ResourcesPath = "Resources")</c> for its OWN page
/// resources (a very common ASP.NET Core convention) causes that factory to prepend
/// <c>Resources.</c> to the base resource name it searches for -- but the SDK embeds its resx
/// files WITHOUT that segment (<c>Postnomic.Client.AspNetCore.Areas.Blog.Pages.Index</c>, not
/// <c>Postnomic.Client.AspNetCore.Resources.Areas.Blog.Pages.Index</c>). The lookup misses,
/// <c>ResourceManagerStringLocalizer</c> falls back to returning the key name itself, and the
/// view renders <c>ReadMore</c> instead of "Read More" / "Weiterlesen".
/// </para>
/// <para>
/// This test's <c>TestServer</c> host is configured EXACTLY like ThreeBIT's: it calls
/// <c>services.AddLocalization(o =&gt; o.ResourcesPath = "Resources")</c> BEFORE
/// <c>AddPostnomicBlog(...)</c> and <c>AddRazorPages()</c>, matching production. It must render
/// translated strings, and must NEVER render a raw resource key, in either configured culture.
/// </para>
/// </remarks>
public class HostResourcesPathLocalizationRenderingTests : IAsyncLifetime
{
    private const string Slug = "resources-path-post";

    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var blogServiceMock = new Mock<IPostnomicBlogService>();

        blogServiceMock
            .Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items =
                [
                    new PostnomicPostSummary
                    {
                        Slug = Slug,
                        Title = "The ResourcesPath Post",
                        Excerpt = "A short excerpt.",
                        AuthorName = "Jane Doe",
                        PublishedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                        Language = "en",
                        AvailableLanguages = ["en"],
                    },
                ],
                Page = 1,
                PageSize = 5,
                TotalCount = 1,
                TotalPages = 1,
            });

        blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "ResourcesPath Blog", Slug = "resourcespath-blog" });

        blogServiceMock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        blogServiceMock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        blogServiceMock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        blogServiceMock
            .Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicPopularPost { Slug = Slug, Title = "The ResourcesPath Post", Count = 3 }]);
        blogServiceMock
            .Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicPopularPost { Slug = Slug, Title = "The ResourcesPath Post", Count = 7 }]);

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseContentRoot(AppContext.BaseDirectory);

                webHost.ConfigureServices(services =>
                {
                    // Matches ThreeBIT's production configuration EXACTLY: the host sets its own
                    // ResourcesPath (for its own page resx files) BEFORE registering the Postnomic
                    // Blog Area. This is the configuration that reproduced the production bug.
                    services.AddLocalization(o => o.ResourcesPath = "Resources");

                    services.Configure<RequestLocalizationOptions>(options =>
                    {
                        options.SetDefaultCulture("en");
                        options.AddSupportedCultures("en", "de");
                        options.AddSupportedUICultures("en", "de");
                    });

                    services.AddPostnomicBlog(options =>
                    {
                        options.BaseUrl = "https://api.postnomic.example";
                        options.ApiKey = "test-key";
                        options.BlogSlug = "resourcespath-blog";
                        options.BasePath = "/blog";
                    });

                    services.AddRazorPages();

                    services.AddSingleton(blogServiceMock.Object);
                });

                webHost.Configure(app =>
                {
                    app.UseRequestLocalization();
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
    public async Task GermanAcceptLanguage_WithHostResourcesPath_RendersGermanChrome_NotRawKeys()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/blog");
        request.Headers.Add("Accept-Language", "de");

        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Weiterlesen", html);
        Assert.DoesNotContain("ReadMore", html);
        Assert.DoesNotContain("Read More", html);
    }

    [Fact]
    public async Task DefaultCulture_WithHostResourcesPath_RendersEnglishChrome_NotRawKeys()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/blog");

        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Read More", html);
        Assert.DoesNotContain("ReadMore", html);
        Assert.DoesNotContain("Weiterlesen", html);
    }
}

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
/// Covers Fix 2: <c>AddPostnomicBlog</c> must be self-sufficient for localization. A host that
/// calls <c>AddPostnomicBlog()</c> + <c>AddRazorPages()</c> but does <em>not</em> also call
/// <c>services.AddLocalization()</c> / <c>.AddViewLocalization()</c> must still be able to render
/// the Blog Area — including in German — without throwing
/// <see cref="InvalidOperationException"/> for an unresolved <c>IHtmlLocalizerFactory</c>.
/// </summary>
public class PostnomicBlogSelfSufficientLocalizationTests : IAsyncLifetime
{
    private const string Slug = "self-sufficient-post";

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
                        Title = "The Self-Sufficient Post",
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
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Self-Sufficient Blog", Slug = "self-sufficient-blog" });

        blogServiceMock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        blogServiceMock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        blogServiceMock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        blogServiceMock
            .Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        blogServiceMock
            .Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseContentRoot(AppContext.BaseDirectory);

                webHost.ConfigureServices(services =>
                {
                    // Deliberately NOT calling services.AddLocalization() / .AddViewLocalization()
                    // here — the point of this test is that AddPostnomicBlog wires up everything
                    // the Blog Area's views need on its own.
                    services.AddRazorPages();

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
                        options.BlogSlug = "self-sufficient-blog";
                        options.BasePath = "/blog";
                    });

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
    public async Task GermanAcceptLanguage_RendersGermanChrome_WithoutHostCallingAddLocalization()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/blog");
        request.Headers.Add("Accept-Language", "de");

        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Weiterlesen", html);
        Assert.DoesNotContain("Read More", html);
    }
}

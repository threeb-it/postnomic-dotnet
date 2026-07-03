using System.Globalization;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
/// Render-level integration test verifying the Blog Area's UI chrome is actually culture-aware
/// end to end — <c>TestServer</c> request in, rendered HTML out — rather than just unit-testing
/// the resx lookup in isolation (see <see cref="BlogAreaLocalizationTests"/> for that). Uses
/// <c>Accept-Language</c> plus ASP.NET Core's standard <see cref="RequestLocalizationMiddleware"/>,
/// the same mechanism a real host (e.g. ThreeBIT's site) uses to pick <c>CultureInfo.CurrentUICulture</c>
/// per request.
/// </summary>
public class BlogAreaLocalizationRenderingTests : IAsyncLifetime
{
    private const string Slug = "localization-post";
    private const string AuthorSlug = "jane-doe";
    private static readonly DateTime AnonymousCommentCreatedAt = new(2025, 6, 15, 9, 45, 0, DateTimeKind.Utc);

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
                        Title = "The Localization Post",
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
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Localization Blog", Slug = "localization-blog" });

        blogServiceMock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        blogServiceMock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        blogServiceMock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        blogServiceMock
            .Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicPopularPost { Slug = Slug, Title = "The Localization Post", Count = 3 }]);
        blogServiceMock
            .Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicPopularPost { Slug = Slug, Title = "The Localization Post", Count = 7 }]);

        blogServiceMock
            .Setup(s => s.GetPostAsync(Slug, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPostDetail
            {
                Slug = Slug,
                Title = "The Localization Post",
                Content = "<p>Some post content.</p>",
                AuthorName = "Jane Doe",
                PublishedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                Language = "en",
                AvailableLanguages = ["en"],
                CommentsEnabled = true,
                // Anonymous (no AuthorName) so _Comment.cshtml's "CommentAnonymous" fallback
                // renders, and dated so the "f" (long date + short time) format is exercised.
                Comments =
                [
                    new PostnomicComment
                    {
                        PublicId = "c-anon",
                        AuthorName = null,
                        Body = "Thanks for sharing this!",
                        CreatedAt = AnonymousCommentCreatedAt,
                    },
                ],
            });

        blogServiceMock
            .Setup(s => s.GetAuthorProfileAsync(AuthorSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicAuthorProfile
            {
                Name = "Jane Doe",
                Slug = AuthorSlug,
                Headline = "Senior Writer",
                PostCount = 2,
                SocialLinks = [new PostnomicSocialLink { Platform = "GitHub", Url = "https://github.com/janedoe" }],
                Skills = ["Writing"],
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
                        options.BlogSlug = "localization-blog";
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
    public async Task GermanAcceptLanguage_RendersGermanChrome_NotEnglish()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/blog");
        request.Headers.Add("Accept-Language", "de");

        using var response = await _client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("Weiterlesen");
        html.Should().NotContain("Read More");
    }

    [Fact]
    public async Task DefaultCulture_RendersEnglishChrome()
    {
        var html = await _client.GetStringAsync("/blog");

        html.Should().Contain("Read More");
        html.Should().NotContain("Weiterlesen");
    }

    [Fact]
    public async Task GermanAcceptLanguage_PostPage_RendersGermanSidebarWidgets_NotEnglish()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/blog/post/{Slug}");
        request.Headers.Add("Accept-Language", "de");

        using var response = await _client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("Meistkommentiert");
        html.Should().Contain("Meistgelesen");
        html.Should().Contain("Kommentar hinterlassen");
        html.Should().NotContain("Top Commented");
        html.Should().NotContain("Most Read");
        html.Should().NotContain("Leave a Comment");

        // Fix 3 coverage: _Comment.cshtml renders the localized author fallback (the mocked
        // comment has AuthorName = null) and a culture-formatted "long date + short time" ("f")
        // timestamp. We assert on the long-date ("D") portion computed with the SAME culture the
        // request resolves to (de) rather than the full "f" string: per the .NET spec "f" == "D"
        // + " " + short time, so the "D" output is the exact, ASCII-only prefix of what the view
        // renders. This keeps the assertion OS/ICU-robust — the short-time portion of "f" for some
        // cultures uses a narrow no-break space (U+202F) before AM/PM on Linux ICU, which Razor's
        // HtmlEncoder then emits as the entity "&#x202F;", so a full-"f" compare mismatches the
        // rendered HTML cross-OS even when computed the same way.
        var deCulture = CultureInfo.GetCultureInfo("de");
        html.Should().Contain(">Anonym<");
        html.Should().NotContain(">Anonymous<");
        html.Should().Contain(AnonymousCommentCreatedAt.ToString("D", deCulture));
        html.Should().Contain("Juni"); // German month name; distinct from English "June"
    }

    [Fact]
    public async Task DefaultCulture_PostPage_RendersEnglishSidebarWidgets()
    {
        var html = await _client.GetStringAsync($"/blog/post/{Slug}");

        html.Should().Contain("Top Commented");
        html.Should().Contain("Most Read");
        html.Should().Contain("Leave a Comment");
        html.Should().NotContain("Meistkommentiert");
        html.Should().NotContain("Meistgelesen");

        // Fix 3 coverage: same anonymous comment, rendered with the English author fallback and
        // the long-date ("D") portion of the culture-formatted timestamp for the English culture
        // (see the German test above for why "D" rather than the full "f" string — OS/ICU-robust).
        var enCulture = CultureInfo.GetCultureInfo("en");
        html.Should().Contain(">Anonymous<");
        html.Should().NotContain(">Anonym<");
        html.Should().Contain(AnonymousCommentCreatedAt.ToString("D", enCulture));
        html.Should().Contain("June"); // English month name; distinct from German "Juni"
    }

    [Fact]
    public async Task GermanAcceptLanguage_AuthorPage_RendersGermanChrome_NotEnglish()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/blog/author/{AuthorSlug}");
        request.Headers.Add("Accept-Language", "de");

        using var response = await _client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("Vernetzen");
        html.Should().Contain("Fähigkeiten");
        html.Should().NotContain(">Connect<");
        html.Should().NotContain(">Skills<");
    }

    [Fact]
    public async Task DefaultCulture_AuthorPage_RendersEnglishChrome()
    {
        var html = await _client.GetStringAsync($"/blog/author/{AuthorSlug}");

        html.Should().Contain(">Connect<");
        html.Should().Contain(">Skills<");
        html.Should().NotContain("Vernetzen");
        html.Should().NotContain("Fähigkeiten");
    }
}

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
/// End-to-end integration tests verifying that the Razor Pages Blog Area (<c>Index</c>, <c>Post</c>,
/// <c>Author</c>) resolves its CSS classes through <see cref="PostnomicCssClasses"/> according to
/// the configured <see cref="PostnomicMarkupStyle"/> — the default (<see cref="PostnomicMarkupStyle.Bootstrap"/>)
/// must keep emitting today's literal Bootstrap markup byte-for-byte (Xircuit regression guard),
/// while <see cref="PostnomicMarkupStyle.Semantic"/> must emit only <c>pn-*</c> classes and carry no
/// Bootstrap vestiges. Mirrors the <see cref="SeoRenderingTests"/> / <see cref="FeedEndpointTests"/>
/// <see cref="TestServer"/> + mocked <see cref="IPostnomicBlogService"/> pattern.
/// </summary>
public class MarkupStyleRenderingTests : IAsyncLifetime
{
    private const string Slug = "markup-style-post";
    private const string AuthorSlug = "jane-doe";

    private IHost _bootstrapHost = null!;
    private IHost _semanticHost = null!;
    private HttpClient _bootstrapClient = null!;
    private HttpClient _semanticClient = null!;

    public async Task InitializeAsync()
    {
        _bootstrapHost = await BuildHostAsync(PostnomicMarkupStyle.Bootstrap);
        _semanticHost = await BuildHostAsync(PostnomicMarkupStyle.Semantic);
        _bootstrapClient = _bootstrapHost.GetTestClient();
        _semanticClient = _semanticHost.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _bootstrapClient.Dispose();
        _semanticClient.Dispose();
        await _bootstrapHost.StopAsync();
        await _semanticHost.StopAsync();
        _bootstrapHost.Dispose();
        _semanticHost.Dispose();
    }

    private static async Task<IHost> BuildHostAsync(PostnomicMarkupStyle style)
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
                        Title = "The Markup Style Post",
                        Excerpt = "A short excerpt for the markup style test post.",
                        AuthorName = "Jane Doe",
                        AuthorSlug = AuthorSlug,
                        PublishedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                        Language = "en",
                        AvailableLanguages = ["en"],
                        Tags = [new PostnomicTag { Slug = "tag", Name = "Tag", PostCount = 1 }],
                        Categories = [new PostnomicCategory { Slug = "cat", Name = "Category", PostCount = 1 }],
                    },
                ],
                Page = 1,
                PageSize = 5,
                TotalCount = 1,
                TotalPages = 1,
            });

        blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Markup Style Blog", Slug = "markup-style-blog" });

        blogServiceMock
            .Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicTag { Slug = "tag", Name = "Tag", PostCount = 1 }]);

        blogServiceMock
            .Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicCategory { Slug = "cat", Name = "Category", PostCount = 1 }]);

        blogServiceMock
            .Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicAuthor { Name = "Jane Doe", PostCount = 1 }]);

        blogServiceMock
            .Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicPopularPost { Slug = Slug, Title = "The Markup Style Post", Count = 3 }]);

        blogServiceMock
            .Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicPopularPost { Slug = Slug, Title = "The Markup Style Post", Count = 7 }]);

        blogServiceMock
            .Setup(s => s.GetPostAsync(Slug, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPostDetail
            {
                Slug = Slug,
                Title = "The Markup Style Post",
                Content = "<p>Some post content.</p>",
                Excerpt = "A short excerpt for the markup style test post.",
                CoverImageUrl = "/images/cover.jpg",
                AuthorName = "Jane Doe",
                AuthorSlug = AuthorSlug,
                PublishedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                Language = "en",
                AvailableLanguages = ["en"],
                Tags = [new PostnomicTag { Slug = "tag", Name = "Tag", PostCount = 1 }],
                Categories = [new PostnomicCategory { Slug = "cat", Name = "Category", PostCount = 1 }],
                CommentsEnabled = true,
                Comments =
                [
                    new PostnomicComment
                    {
                        PublicId = "c1",
                        AuthorName = "A Reader",
                        Body = "Nice post!",
                        CreatedAt = DateTime.UtcNow,
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
                PostCount = 1,
                Interests = ["Writing"],
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
                        options.BlogSlug = "markup-style-blog";
                        options.BasePath = "/blog";
                        options.MarkupStyle = style;
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

        return await hostBuilder.StartAsync();
    }

    [Fact]
    public async Task Default_mode_emits_bootstrap_classes()
    {
        var html = await _bootstrapClient.GetStringAsync("/blog");
        Assert.Contains("card", html);
        Assert.Contains("col-lg-8", html);
        Assert.DoesNotContain("pn-card", html);
    }

    [Fact]
    public async Task Semantic_mode_emits_pn_classes()
    {
        var html = await _semanticClient.GetStringAsync("/blog");
        Assert.Contains("pn-card", html);
        Assert.DoesNotContain("col-lg-8", html);
    }

    [Fact]
    public async Task Semantic_mode_blog_index_has_no_bootstrap_vestiges()
    {
        var html = await _semanticClient.GetStringAsync("/blog");
        foreach (var bs in new[] { "col-lg-", "card mb-4", "badge", "btn btn-", "bi bi-" })
            Assert.DoesNotContain(bs, html);
    }

    [Fact]
    public async Task Default_mode_post_page_emits_bootstrap_classes()
    {
        var html = await _bootstrapClient.GetStringAsync($"/blog/post/{Slug}");
        Assert.Contains("col-lg-8", html);
        Assert.Contains("card mb-4 shadow-sm", html);
        Assert.DoesNotContain("pn-card", html);
    }

    [Fact]
    public async Task Semantic_mode_post_page_has_no_bootstrap_vestiges()
    {
        var html = await _semanticClient.GetStringAsync($"/blog/post/{Slug}");
        Assert.Contains("pn-post-content", html);
        Assert.Contains("pn-card", html);
        foreach (var bs in new[] { "col-lg-", "card mb-4", "badge", "btn btn-", "bi bi-" })
            Assert.DoesNotContain(bs, html);
    }

    [Fact]
    public async Task Default_mode_author_page_emits_bootstrap_classes()
    {
        var html = await _bootstrapClient.GetStringAsync($"/blog/author/{AuthorSlug}");
        Assert.Contains("col-lg-8", html);
        Assert.Contains("card shadow-sm", html);
        Assert.DoesNotContain("pn-card", html);
    }

    [Fact]
    public async Task Semantic_mode_author_page_has_no_bootstrap_vestiges()
    {
        var html = await _semanticClient.GetStringAsync($"/blog/author/{AuthorSlug}");
        Assert.Contains("pn-main", html);
        Assert.Contains("pn-card", html);
        foreach (var bs in new[] { "col-lg-", "card mb-4", "badge", "btn btn-", "bi bi-" })
            Assert.DoesNotContain(bs, html);
    }
}

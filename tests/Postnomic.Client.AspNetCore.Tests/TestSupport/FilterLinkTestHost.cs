using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.AspNetCore.Tests.TestSupport;

/// <summary>
/// A <see cref="TestServer"/> host for the Blog Area whose mocked data makes every kind of SDK-rendered
/// link appear at least once: post links, per-post tag/category/author links, the sidebar tag,
/// category and author filters, the search form, pagination (three pages), and the external
/// promotion links (branding on). Used by the <c>FilterLinkRel</c> rendering tests.
/// </summary>
internal static partial class FilterLinkTestHost
{
    public const string Slug = "filter-link-post";
    public const string AuthorSlug = "jane-doe";

    /// <summary>Configures one blog's options for the host. <paramref name="name"/> null = the default registration.</summary>
    public sealed record BlogRegistration(string? Name, string BasePath, string? FilterLinkRel,
        PostnomicMarkupStyle MarkupStyle = PostnomicMarkupStyle.Bootstrap);

    public static Mock<IPostnomicBlogService> CreateBlogServiceMock()
    {
        var mock = new Mock<IPostnomicBlogService>();
        var tag = new PostnomicTag { Slug = "dotnet", Name = ".NET", PostCount = 2 };
        var category = new PostnomicCategory { Slug = "guides", Name = "Guides", PostCount = 2 };

        mock.Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int pageSize, string? _, string? _, string? _, string? _, string? _, CancellationToken _) =>
                new PostnomicPagedResult<PostnomicPostSummary>
                {
                    Items =
                    [
                        new PostnomicPostSummary
                        {
                            Slug = Slug,
                            Title = "The Filter Link Post",
                            Excerpt = "Excerpt.",
                            AuthorName = "Jane Doe",
                            AuthorSlug = AuthorSlug,
                            PublishedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                            Language = "en",
                            AvailableLanguages = ["en"],
                            Tags = [tag],
                            Categories = [category],
                        },
                    ],
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = 3 * pageSize,
                    TotalPages = 3,
                });

        mock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Filter Link Blog", Slug = "filter-link-blog", ShowBranding = true });
        mock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([tag]);
        mock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([category]);
        mock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicAuthor { Name = "Jane Doe", PostCount = 2 }]);
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicPopularPost { Slug = Slug, Title = "The Filter Link Post", Count = 3 }]);
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicPopularPost { Slug = Slug, Title = "The Filter Link Post", Count = 7 }]);

        mock.Setup(s => s.GetPostAsync(Slug, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPostDetail
            {
                Slug = Slug,
                Title = "The Filter Link Post",
                Content = "<p>Some post content.</p>",
                Excerpt = "Excerpt.",
                AuthorName = "Jane Doe",
                AuthorSlug = AuthorSlug,
                PublishedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                Language = "en",
                AvailableLanguages = ["en"],
                Tags = [tag],
                Categories = [category],
                CommentsEnabled = true,
                Comments = [],
            });

        mock.Setup(s => s.GetAuthorProfileAsync(AuthorSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicAuthorProfile
            {
                Name = "Jane Doe",
                Slug = AuthorSlug,
                Headline = "Senior Writer",
                WebsiteUrl = "https://jane.example",
                PostCount = 1,
                RecentPosts = [new PostnomicPostSummary
                {
                    Slug = Slug,
                    Title = "The Filter Link Post",
                    AuthorName = "Jane Doe",
                    AuthorSlug = AuthorSlug,
                    PublishedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                }],
            });

        return mock;
    }

    public static Task<IHost> StartAsync(params BlogRegistration[] blogs) =>
        StartAsync(blogs.Select(b => (b, CreateBlogServiceMock())).ToArray());

    /// <summary>Starts a host whose blogs use the given (inspectable) service mocks.</summary>
    public static async Task<IHost> StartAsync(params (BlogRegistration Blog, Mock<IPostnomicBlogService> Service)[] blogs)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseContentRoot(AppContext.BaseDirectory);

                webHost.ConfigureServices(services =>
                {
                    services.AddLocalization();
                    services.AddRazorPages().AddViewLocalization();

                    foreach (var (blog, service) in blogs)
                    {
                        void Configure(PostnomicClientOptions options)
                        {
                            options.BaseUrl = "https://api.postnomic.example";
                            options.ApiKey = "test-key";
                            options.BlogSlug = "filter-link-blog";
                            options.BasePath = blog.BasePath;
                            options.MarkupStyle = blog.MarkupStyle;
                            options.ShowBranding = true;
                            options.FilterLinkRel = blog.FilterLinkRel;
                        }

                        if (blog.Name is null)
                        {
                            services.AddPostnomicBlog(Configure);
                            services.AddSingleton(service.Object);
                        }
                        else
                        {
                            services.AddPostnomicBlog(blog.Name, Configure);
                            services.AddKeyedSingleton(blog.Name, service.Object);
                        }
                    }
                });

                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapRazorPages());
                });
            });

        return await hostBuilder.StartAsync();
    }

    /// <summary>One rendered <c>&lt;a&gt;</c> (or <c>&lt;form&gt;</c>) start tag with its decoded attributes.</summary>
    public sealed record Element(string Tag, string Raw, string? Href, string? Rel)
    {
        public IReadOnlyList<string> RelTokens =>
            Rel is null ? [] : Rel.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    [GeneratedRegex("""<(a|form)\b[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex StartTagRegex();

    [GeneratedRegex(@"\s(href|action|rel)=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex AttributeRegex();

    /// <summary>Every <c>&lt;a&gt;</c> and <c>&lt;form&gt;</c> start tag in the page's body.</summary>
    public static IReadOnlyList<Element> Elements(string html)
    {
        var bodyStart = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        var body = bodyStart >= 0 ? html[bodyStart..] : html;

        return StartTagRegex().Matches(body).Select(m =>
        {
            string? href = null, rel = null;
            foreach (System.Text.RegularExpressions.Match attr in AttributeRegex().Matches(m.Value))
            {
                var value = System.Net.WebUtility.HtmlDecode(attr.Groups[2].Value);
                switch (attr.Groups[1].Value.ToLowerInvariant())
                {
                    case "href" or "action": href = value; break;
                    case "rel": rel = value; break;
                }
            }
            return new Element(m.Groups[1].Value.ToLowerInvariant(), m.Value, href, rel);
        }).ToList();
    }

    /// <summary>
    /// Whether an internal link is a filter / archive / pagination variant — the set
    /// <see cref="PostnomicClientOptions.FilterLinkRel"/> applies to. Classified by URL shape, not by
    /// how the view builds it, so a newly added link that forgets the helper is still caught.
    /// </summary>
    public static bool IsFilterOrPagingLink(Element element)
    {
        if (element.Href is null || element.Href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return false;
        if (element.Tag == "form")
            return true; // the GET search form
        var query = element.Href.Contains('?') ? element.Href[(element.Href.IndexOf('?') + 1)..].ToLowerInvariant() : "";
        var queryKeys = query.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=')[0]);
        return queryKeys.Any(k => k is "tag" or "category" or "author" or "search" or "p" or "pagesize")
            || element.Href.Contains("/author/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExternal(Element element) =>
        element.Href is not null && element.Href.StartsWith("http", StringComparison.OrdinalIgnoreCase);
}

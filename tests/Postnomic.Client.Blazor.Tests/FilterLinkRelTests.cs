using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.Blazor.Components.Pages;

namespace Postnomic.Client.Blazor.Tests;

/// <summary>
/// bUnit tests for <see cref="PostnomicClientOptions.FilterLinkRel"/> in the Blazor components.
/// <para>
/// The Blazor tag cloud, category list, author list, search box and pager are interactive
/// <c>&lt;button&gt;</c>s with no <c>href</c>, so crawlers cannot follow them and they take no
/// <c>rel</c>. The only filter/archive anchors the Blazor components render are the author archive
/// links (<c>/author/{slug}</c>). The guard below classifies every rendered anchor by URL shape, so a
/// filter or paging <em>link</em> added later without the helper is caught here.
/// </para>
/// </summary>
public class FilterLinkRelTests : BunitContext
{
    private readonly Mock<IPostnomicBlogService> _blogServiceMock = new();

    private static readonly PostnomicTag Tag = new() { Slug = "dotnet", Name = ".NET", PostCount = 1 };
    private static readonly PostnomicCategory Category = new() { Slug = "guides", Name = "Guides", PostCount = 1 };

    public FilterLinkRelTests()
    {
        Services.AddSingleton(_blogServiceMock.Object);

        _blogServiceMock.Setup(s => s.RecordPageViewAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blogServiceMock.Setup(s => s.UpdateReadDurationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blogServiceMock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Blog", Slug = "blog", ShowBranding = true });
        _blogServiceMock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Tag]);
        _blogServiceMock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Category]);
        _blogServiceMock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicAuthor { Name = "Jane Doe", PostCount = 1 }]);
        _blogServiceMock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicPopularPost { Slug = "first", Title = "First", Count = 2 }]);
        _blogServiceMock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicPopularPost { Slug = "first", Title = "First", Count = 9 }]);
        _blogServiceMock.Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items =
                [
                    new PostnomicPostSummary
                    {
                        Slug = "first", Title = "First", AuthorName = "Jane Doe", AuthorSlug = "jane-doe",
                        PublishedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                        Tags = [Tag], Categories = [Category],
                    },
                ],
                Page = 1, PageSize = 5, TotalCount = 15, TotalPages = 3,
            });
        _blogServiceMock.Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPostDetail
            {
                Slug = "first", Title = "First", AuthorName = "Jane Doe", AuthorSlug = "jane-doe",
                PublishedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                Content = "<p>Body</p>", Tags = [Tag], Categories = [Category],
            });
    }

    private void UseOptions(string? filterLinkRel) =>
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(
            Options.Create(new PostnomicClientOptions { FilterLinkRel = filterLinkRel, ShowBranding = true }));

    private IRenderedComponent<BlogPage> RenderBlogPage() => Render<BlogPage>();

    private IRenderedComponent<PostPage> RenderPostPage() => Render<PostPage>(p => p.Add(c => c.PostSlug, "first"));

    private static bool IsExternal(IElement a) => a.GetAttribute("href")?.StartsWith("http", StringComparison.Ordinal) == true;

    private static bool IsFilterOrPagingLink(IElement a)
    {
        var href = a.GetAttribute("href");
        if (href is null || IsExternal(a)) return false;
        var query = href.Contains('?') ? href[(href.IndexOf('?') + 1)..].ToLowerInvariant() : "";
        var keys = query.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=')[0]);
        return keys.Any(k => k is "tag" or "category" or "author" or "search" or "p" or "pagesize")
               || href.Contains("/author/", StringComparison.Ordinal);
    }

    private static string[] RelTokens(IElement a) =>
        a.GetAttribute("rel")?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];

    // ── BlogPage ──────────────────────────────────────────────────────────────

    [Fact]
    public void BlogPage_unset_option_emits_no_rel_on_internal_links()
    {
        UseOptions(null);
        var cut = RenderBlogPage();
        cut.WaitForState(() => cut.FindAll("a[href*='/author/']").Count > 0);

        Assert.All(cut.FindAll("a").Where(a => !IsExternal(a)), a => Assert.False(a.HasAttribute("rel")));
    }

    [Fact]
    public void BlogPage_nofollow_is_on_author_archive_links_and_nowhere_else_internal()
    {
        UseOptions("nofollow");
        var cut = RenderBlogPage();
        cut.WaitForState(() => cut.FindAll("a[href*='/author/']").Count > 0);
        var anchors = cut.FindAll("a").ToList();

        var filterLinks = anchors.Where(IsFilterOrPagingLink).ToList();
        Assert.NotEmpty(filterLinks);
        Assert.All(filterLinks, a => Assert.Contains("nofollow", RelTokens(a)));

        var postLinks = anchors.Where(a => a.GetAttribute("href")!.Contains("/post/", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(postLinks);
        Assert.All(postLinks, a => Assert.False(a.HasAttribute("rel")));

        Assert.All(anchors.Where(IsExternal), a => Assert.Equal("noopener noreferrer", a.GetAttribute("rel")));
    }

    [Fact]
    public void BlogPage_filters_and_pager_are_buttons_not_links()
    {
        // Documents why FilterLinkRel touches only the author archive link in Blazor: the tag,
        // category, author and search filters and the pager have no href for a crawler to follow.
        UseOptions("nofollow");
        var cut = RenderBlogPage();
        cut.WaitForState(() => cut.FindAll("a[href*='/author/']").Count > 0);

        Assert.DoesNotContain(cut.FindAll("a"), a =>
            a.GetAttribute("href")!.Contains('?', StringComparison.Ordinal));
        Assert.NotEmpty(cut.FindAll("nav button"));
    }

    [Fact]
    public void BlogPage_uses_the_cascaded_blog_context_value()
    {
        // A named blog (PostnomicBlogScope) cascades its own options: its value wins over the
        // default registration's, in both directions.
        UseOptions("nofollow");
        var context = new PostnomicBlogContext
        {
            BlogName = "open",
            BlogService = _blogServiceMock.Object,
            Options = new PostnomicClientOptions { FilterLinkRel = null },
        };
        var cut = Render<BlogPage>(p => p.AddCascadingValue(context));
        cut.WaitForState(() => cut.FindAll("a[href*='/author/']").Count > 0);

        Assert.All(cut.FindAll("a").Where(a => !IsExternal(a)), a => Assert.False(a.HasAttribute("rel")));
    }

    [Fact]
    public void BlogPage_cascaded_blog_context_value_is_applied()
    {
        UseOptions(null);
        var context = new PostnomicBlogContext
        {
            BlogName = "closed",
            BlogService = _blogServiceMock.Object,
            Options = new PostnomicClientOptions { FilterLinkRel = "nofollow" },
        };
        var cut = Render<BlogPage>(p => p.AddCascadingValue(context));
        cut.WaitForState(() => cut.FindAll("a[href*='/author/']").Count > 0);

        Assert.All(cut.FindAll("a[href*='/author/']"), a => Assert.Equal("nofollow", a.GetAttribute("rel")));
    }

    // ── PostPage ──────────────────────────────────────────────────────────────

    [Fact]
    public void PostPage_unset_option_emits_no_rel_on_internal_links()
    {
        UseOptions(null);
        var cut = RenderPostPage();
        cut.WaitForState(() => cut.FindAll("a[href*='/author/']").Count > 0);

        Assert.All(cut.FindAll("a").Where(a => !IsExternal(a)), a => Assert.False(a.HasAttribute("rel")));
    }

    [Fact]
    public void PostPage_nofollow_is_on_the_author_archive_link_only()
    {
        UseOptions("nofollow");
        var cut = RenderPostPage();
        cut.WaitForState(() => cut.FindAll("a[href*='/author/']").Count > 0);
        var anchors = cut.FindAll("a").ToList();

        var filterLinks = anchors.Where(IsFilterOrPagingLink).ToList();
        Assert.NotEmpty(filterLinks);
        Assert.All(filterLinks, a => Assert.Equal("nofollow", a.GetAttribute("rel")));

        // "Back to blog" and post links stay followable; external links keep their own rel.
        Assert.All(anchors.Where(a => !IsExternal(a) && !IsFilterOrPagingLink(a)), a => Assert.False(a.HasAttribute("rel")));
        Assert.All(anchors.Where(IsExternal), a => Assert.Equal("noopener noreferrer", a.GetAttribute("rel")));
    }
}

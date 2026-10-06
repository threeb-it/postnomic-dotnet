using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Postnomic.Client.Abstractions;
using Postnomic.Client.AspNetCore.Tests.TestSupport;
using static Postnomic.Client.AspNetCore.Tests.TestSupport.FilterLinkTestHost;

namespace Postnomic.Client.AspNetCore.Tests;

/// <summary>
/// End-to-end rendering tests for <see cref="PostnomicClientOptions.FilterLinkRel"/> across the Razor
/// Pages Blog Area (<c>Index</c>, <c>Post</c>, <c>Author</c>), in both markup styles.
/// <para>
/// Links are classified by the <b>shape of their URL</b> (<see cref="FilterLinkTestHost.IsFilterOrPagingLink"/>),
/// not by how a view builds them, so a filter or paging link added to a view later without the
/// <c>FilterRel</c> helper fails these tests instead of silently leaking a crawlable variant.
/// </para>
/// </summary>
public class FilterLinkRelRenderingTests
{
    private const string Post = $"/blog/post/{Slug}";
    private const string AuthorPage = $"/blog/author/{AuthorSlug}";

    /// <summary>Pages covering every link kind: plain index, filtered + paged index, search, post, author.</summary>
    public static TheoryData<PostnomicMarkupStyle, string> Pages()
    {
        var data = new TheoryData<PostnomicMarkupStyle, string>();
        foreach (var style in new[] { PostnomicMarkupStyle.Bootstrap, PostnomicMarkupStyle.Semantic })
        foreach (var url in new[] { "/blog", "/blog?tag=dotnet&p=2", "/blog?search=hello&author=Jane%20Doe", Post, AuthorPage })
            data.Add(style, url);
        return data;
    }

    private static async Task<string> RenderAsync(string? filterLinkRel, PostnomicMarkupStyle style, string url)
    {
        using var host = await StartAsync(new BlogRegistration(null, "/blog", filterLinkRel, style));
        using var client = host.GetTestClient();
        var html = await client.GetStringAsync(url, TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
        return html;
    }

    // ── Option unset: nothing changes ─────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Unset_option_emits_no_rel_on_any_internal_link(PostnomicMarkupStyle style, string url)
    {
        var elements = Elements(await RenderAsync(null, style, url));

        Assert.All(elements.Where(e => !IsExternal(e)), e => Assert.Null(e.Rel));
        // External links keep exactly the rel they always had.
        Assert.All(elements.Where(IsExternal), e => Assert.Equal("noopener noreferrer", e.Rel));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Whitespace_option_is_treated_as_unset(string filterLinkRel)
    {
        var elements = Elements(await RenderAsync(filterLinkRel, PostnomicMarkupStyle.Bootstrap, "/blog?tag=dotnet&p=2"));

        Assert.All(elements.Where(e => !IsExternal(e)), e => Assert.Null(e.Rel));
    }

    // ── Option set: filter / archive / paging links carry it, nothing else does ─

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Nofollow_is_on_every_filter_archive_and_paging_link(PostnomicMarkupStyle style, string url)
    {
        var elements = Elements(await RenderAsync("nofollow", style, url));
        // Non-vacuity per link kind is asserted separately (the *_covers_* tests): the author page
        // legitimately has no filter links at all.
        Assert.All(elements.Where(IsFilterOrPagingLink), e => Assert.Contains("nofollow", e.RelTokens));
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Nofollow_is_never_on_post_links_or_the_blog_index(PostnomicMarkupStyle style, string url)
    {
        var elements = Elements(await RenderAsync("nofollow", style, url));

        var postLinks = elements.Where(e => e.Href?.Contains("/post/", StringComparison.Ordinal) == true).ToList();
        Assert.NotEmpty(postLinks);
        Assert.All(postLinks, e => Assert.Null(e.Rel));

        // "Clear filter" / "Back to blog" point at the plain index — a real page, not a variant.
        Assert.All(elements.Where(e => e.Tag == "a" && e.Href == "/blog"), e => Assert.Null(e.Rel));
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task External_links_keep_their_own_rel_and_gain_nothing(PostnomicMarkupStyle style, string url)
    {
        var external = Elements(await RenderAsync("nofollow", style, url)).Where(IsExternal).ToList();

        Assert.NotEmpty(external);
        Assert.All(external, e => Assert.Equal("noopener noreferrer", e.Rel));
    }

    [Fact]
    public async Task Index_page_covers_every_filter_link_kind()
    {
        // Guards the guard: if the fixture stopped rendering one of these kinds, the "every filter
        // link has the rel" theory above would pass vacuously for it.
        var hrefs = Elements(await RenderAsync("nofollow", PostnomicMarkupStyle.Bootstrap, "/blog"))
            .Where(IsFilterOrPagingLink)
            .Select(e => e.Href!)
            .ToList();

        Assert.Contains(hrefs, h => h.Contains("?tag=", StringComparison.Ordinal));
        Assert.Contains(hrefs, h => h.Contains("?category=", StringComparison.Ordinal));
        Assert.Contains(hrefs, h => h.Contains("?author=", StringComparison.Ordinal));
        Assert.Contains(hrefs, h => h.Contains("/author/", StringComparison.Ordinal));
        Assert.Contains(hrefs, h => h.Contains("?p=", StringComparison.Ordinal));
        Assert.Contains(hrefs, h => h == "/blog"); // the search form's action
    }

    [Fact]
    public async Task Post_page_covers_its_filter_link_kinds()
    {
        var hrefs = Elements(await RenderAsync("nofollow", PostnomicMarkupStyle.Bootstrap, Post))
            .Where(IsFilterOrPagingLink)
            .Select(e => e.Href!)
            .ToList();

        Assert.Contains(hrefs, h => h.Contains("?tag=", StringComparison.Ordinal));
        Assert.Contains(hrefs, h => h.Contains("?category=", StringComparison.Ordinal));
        Assert.Contains(hrefs, h => h.Contains("/author/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Search_form_carries_the_rel()
    {
        var form = Assert.Single(Elements(await RenderAsync("nofollow", PostnomicMarkupStyle.Bootstrap, "/blog")),
            e => e.Tag == "form" && e.Href == "/blog");

        Assert.Equal("nofollow", form.Rel);
    }

    [Fact]
    public async Task Multiple_tokens_are_rendered_once_each()
    {
        var elements = Elements(await RenderAsync("nofollow  ugc NOFOLLOW", PostnomicMarkupStyle.Bootstrap, "/blog"));

        Assert.All(elements.Where(IsFilterOrPagingLink), e => Assert.Equal("nofollow ugc", e.Rel));
    }

    // ── Per named blog ────────────────────────────────────────────────────────

    [Fact]
    public async Task Named_blogs_each_use_their_own_value()
    {
        // The Razor page models also take the default (unnamed) IPostnomicBlogService, so a
        // multi-blog host registers a default blog alongside the named ones.
        using var host = await StartAsync(
            new BlogRegistration(null, "/news", "nofollow"),
            new BlogRegistration("open", "/blog/open", null),
            new BlogRegistration("closed", "/blog/closed", "nofollow"));
        using var client = host.GetTestClient();
        var ct = TestContext.Current.CancellationToken;

        var open = Elements(await client.GetStringAsync("/blog/open", ct));
        var closed = Elements(await client.GetStringAsync("/blog/closed", ct));

        Assert.All(open.Where(e => !IsExternal(e)), e => Assert.Null(e.Rel));
        Assert.Contains(closed, IsFilterOrPagingLink);
        Assert.All(closed.Where(IsFilterOrPagingLink), e => Assert.Equal("nofollow", e.Rel));

        await host.StopAsync(ct);
    }
}

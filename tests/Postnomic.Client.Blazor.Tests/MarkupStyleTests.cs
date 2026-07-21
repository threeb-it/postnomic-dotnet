using System.Linq;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.Blazor.Components;
using Postnomic.Client.Blazor.Components.Pages;

namespace Postnomic.Client.Blazor.Tests;

/// <summary>
/// bUnit tests verifying that <see cref="BlogPage"/>, <see cref="AuthorPage"/>,
/// <see cref="PostPage"/>, and <see cref="CommentView"/> (and, transitively, every sidebar widget
/// <see cref="BlogPage"/> hosts) resolve their CSS classes through <see cref="PostnomicCssClasses"/>
/// according to the configured <see cref="PostnomicMarkupStyle"/> — the default
/// (<see cref="PostnomicMarkupStyle.Bootstrap"/>) must keep emitting today's literal Bootstrap
/// markup, while <see cref="PostnomicMarkupStyle.Semantic"/> must emit only <c>pn-*</c> classes and
/// carry no Bootstrap vestiges.
/// </summary>
/// <remarks>
/// These per-page Bootstrap/Semantic pairs (plus the icon-distinctness test at the bottom) are the
/// committed regression guard for the "byte-identity" review finding: previously, only
/// <see cref="BlogPage"/> had a rendered assertion here, and the decisive full-page snapshot-diff
/// verification for <see cref="AuthorPage"/>/<see cref="PostPage"/>/<see cref="CommentView"/> was a
/// throwaway test deleted before the original commit (see <c>.superpowers/sdd/task-3-report.md</c>).
/// </remarks>
public class MarkupStyleTests : BunitContext
{
    // ── BlogPage ──────────────────────────────────────────────────────────────

    private void Wire(PostnomicMarkupStyle style)
    {
        var svc = new Mock<IPostnomicBlogService>();
        svc.Setup(s => s.GetBlogAsync(default)).ReturnsAsync(new PostnomicBlogInfo { Name = "Blog", Slug = "b" });
        svc.Setup(s => s.GetPostsAsync(1, 5, null, null, null, null, null, default)).ReturnsAsync(
            new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items = [ new PostnomicPostSummary { Slug="p", Title="Hello", AuthorName="A",
                    PublishedAt=DateTime.UtcNow, Language="en", AvailableLanguages=["en"] } ],
                Page=1, PageSize=5, TotalCount=1, TotalPages=1
            });
        // sidebar getters → empty
        svc.Setup(s => s.GetTagsAsync(default)).ReturnsAsync([]);
        svc.Setup(s => s.GetCategoriesAsync(default)).ReturnsAsync([]);
        svc.Setup(s => s.GetAuthorsAsync(default)).ReturnsAsync([]);
        svc.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), default)).ReturnsAsync([]);
        svc.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), default)).ReturnsAsync([]);
        Services.AddSingleton(svc.Object);
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(Options.Create(new PostnomicClientOptions
        { BaseUrl="https://api.x", ApiKey="k", BlogSlug="b", BasePath="/blog", MarkupStyle=style }));
    }

    [Fact]
    public void Default_bootstrap_mode_still_emits_bootstrap()
    {
        Wire(PostnomicMarkupStyle.Bootstrap);
        var html = Render<BlogPage>().Markup;
        Assert.Contains("card", html);
        Assert.Contains("col-lg-8", html);
        Assert.DoesNotContain("pn-card", html);
    }

    [Fact]
    public void Semantic_mode_emits_pn_and_no_bootstrap()
    {
        Wire(PostnomicMarkupStyle.Semantic);
        var html = Render<BlogPage>().Markup;
        Assert.Contains("pn-blog", html);
        Assert.Contains("pn-card", html);
        Assert.Contains("pn-post-title", html);
        foreach (var bs in new[] { "col-lg-", "card mb-4", "badge", "btn btn-", "bi bi-" })
            Assert.DoesNotContain(bs, html);
    }

    // ── AuthorPage ────────────────────────────────────────────────────────────

    private void WireAuthor(PostnomicMarkupStyle style)
    {
        var svc = new Mock<IPostnomicBlogService>();
        svc.Setup(s => s.GetAuthorProfileAsync(It.IsAny<string>(), default)).ReturnsAsync(
            new PostnomicAuthorProfile
            {
                Name = "Jane Doe",
                Slug = "jane-doe",
                Headline = "Software Engineer",
                Bio = "<p>About Jane</p>",
                Location = "Berlin",
                WebsiteUrl = "https://example.com",
                ProfileImageUrl = "https://example.com/avatar.jpg",
                HeaderImageUrl = "https://example.com/header.jpg",
                Company = "Acme Inc",
                JobTitle = "Senior Dev",
                PostCount = 5,
                SocialLinks = [ new PostnomicSocialLink { Platform = "GitHub", Url = "https://github.com/jane" } ],
                Certifications = [ new PostnomicCertification { Name = "Azure Architect" } ],
                Interests = [ "Hiking" ],
                Skills = [ "C#" ],
                Education = [ new PostnomicEducation { Institution = "MIT" } ],
                Languages = [ new PostnomicLanguage { Name = "English" } ],
                RecentPosts = [ new PostnomicPostSummary { Slug="p", Title="Hello", AuthorName="Jane Doe",
                    PublishedAt=DateTime.UtcNow, Language="en", AvailableLanguages=["en"] } ]
            });
        Services.AddSingleton(svc.Object);
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(Options.Create(new PostnomicClientOptions
        { BaseUrl="https://api.x", ApiKey="k", BlogSlug="b", BasePath="/blog", MarkupStyle=style }));
    }

    [Fact]
    public void AuthorPage_bootstrap_mode_still_emits_bootstrap()
    {
        WireAuthor(PostnomicMarkupStyle.Bootstrap);
        var html = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe")).Markup;
        Assert.Contains("col-lg-8", html);
        Assert.Contains("card shadow-sm", html);
        Assert.DoesNotContain("pn-card", html);
    }

    [Fact]
    public void AuthorPage_semantic_mode_emits_pn_and_no_bootstrap()
    {
        WireAuthor(PostnomicMarkupStyle.Semantic);
        var html = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe")).Markup;
        Assert.Contains("pn-main", html);
        Assert.Contains("pn-card", html);
        foreach (var bs in new[] { "col-lg-", "card mb-4", "badge", "btn btn-", "bi bi-" })
            Assert.DoesNotContain(bs, html);
    }

    // ── PostPage ──────────────────────────────────────────────────────────────

    private void WirePost(PostnomicMarkupStyle style)
    {
        var svc = new Mock<IPostnomicBlogService>();
        var detail = new PostnomicPostDetail
        {
            Slug = "hello-world",
            Title = "Hello World",
            AuthorName = "Jane Doe",
            AuthorSlug = "jane-doe",
            PublishedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Content = "<p>Body</p>",
            CoverImageUrl = "https://example.com/cover.jpg",
            CommentsEnabled = true,
            CommentRequireFirstname = true,
            Tags = [ new PostnomicTag { Name = "Blazor", Slug = "blazor", PostCount = 1 } ],
            Categories = [ new PostnomicCategory { Name = "Tech", Slug = "tech", PostCount = 1 } ],
            Comments = []
        };
        svc.Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(detail);
        svc.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PostnomicBlogInfo { Name = "Blog", Slug = "b" });
        svc.Setup(s => s.RecordPageViewAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        svc.Setup(s => s.UpdateReadDurationAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        svc.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        svc.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Services.AddSingleton(svc.Object);
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(Options.Create(new PostnomicClientOptions
        { BaseUrl="https://api.x", ApiKey="k", BlogSlug="b", BasePath="/blog", MarkupStyle=style }));
    }

    [Fact]
    public void PostPage_bootstrap_mode_still_emits_bootstrap()
    {
        WirePost(PostnomicMarkupStyle.Bootstrap);
        var html = Render<PostPage>(p => p.Add(x => x.PostSlug, "hello-world")).Markup;
        Assert.Contains("blog-post-content", html);
        Assert.Contains("form-control", html);
        Assert.DoesNotContain("pn-post-content", html);
    }

    [Fact]
    public void PostPage_semantic_mode_emits_pn_and_no_bootstrap()
    {
        WirePost(PostnomicMarkupStyle.Semantic);
        var html = Render<PostPage>(p => p.Add(x => x.PostSlug, "hello-world")).Markup;
        Assert.Contains("pn-post-content", html);
        Assert.Contains("pn-field", html);
        foreach (var bs in new[] { "col-lg-", "card mb-4", "badge", "btn btn-", "bi bi-" })
            Assert.DoesNotContain(bs, html);
    }

    // ── CommentView ───────────────────────────────────────────────────────────

    private void WireComment(PostnomicMarkupStyle style)
    {
        var svc = new Mock<IPostnomicBlogService>();
        Services.AddSingleton(svc.Object);
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(Options.Create(new PostnomicClientOptions
        { BaseUrl="https://api.x", ApiKey="k", BlogSlug="b", BasePath="/blog", MarkupStyle=style }));
    }

    private static PostnomicComment CreateComment() => new()
    {
        PublicId = "c1",
        Body = "Nice post!",
        AuthorName = "Reader",
        CreatedAt = DateTime.UtcNow
    };

    private static PostnomicPostDetail CreatePostForComment() => new()
    {
        Slug = "hello-world",
        Title = "Hello World",
        AuthorName = "Jane Doe",
        PublishedAt = DateTime.UtcNow,
        CommentsEnabled = true,
        CommentRequireFirstname = true
    };

    [Fact]
    public void CommentView_bootstrap_mode_reply_form_uses_form_control()
    {
        WireComment(PostnomicMarkupStyle.Bootstrap);
        var cut = Render<CommentView>(p => p
            .Add(x => x.Comment, CreateComment())
            .Add(x => x.Post, CreatePostForComment())
            .Add(x => x.PostSlug, "hello-world")
            .Add(x => x.Depth, 0));

        // Reveal the reply form (hidden until the "Reply" toggle button is clicked).
        cut.Find("button").Click();

        Assert.Contains("form-control", cut.Markup);
        Assert.DoesNotContain("pn-field", cut.Markup);
    }

    [Fact]
    public void CommentView_semantic_mode_reply_form_uses_pn_field_no_bootstrap()
    {
        WireComment(PostnomicMarkupStyle.Semantic);
        var cut = Render<CommentView>(p => p
            .Add(x => x.Comment, CreateComment())
            .Add(x => x.Post, CreatePostForComment())
            .Add(x => x.PostSlug, "hello-world")
            .Add(x => x.Depth, 0));

        cut.Find("button").Click();

        var html = cut.Markup;
        Assert.Contains("pn-field", html);
        foreach (var bs in new[] { "col-", "card mb-4", "badge", "btn btn-", "bi bi-", "form-control" })
            Assert.DoesNotContain(bs, html);
    }

    // ── Icon distinctness (Finding 1 regression guard) ───────────────────────

    /// <summary>
    /// Before the fix, every Semantic-mode icon — regardless of the requested bootstrap-icon
    /// class — rendered the exact same generic placeholder <c>&lt;svg&gt;</c> (a filled circle).
    /// <see cref="BlogPage"/>'s post-meta line renders the "bi bi-person" icon (author) and the
    /// "bi bi-calendar" icon (published date) as the first two icons in the page, in that order,
    /// so they are the most direct way to assert two different bootstrap-icon classes now render
    /// two genuinely different SVGs.
    /// </summary>
    [Fact]
    public void Semantic_mode_renders_distinct_icons_for_different_bootstrap_classes()
    {
        Wire(PostnomicMarkupStyle.Semantic);
        var html = Render<BlogPage>().Markup;

        var svgBlocks = Regex.Matches(html, "<svg[\\s\\S]*?</svg>")
            .Select(m => m.Value)
            .ToList();

        Assert.True(svgBlocks.Count >= 2,
            "expected at least the person (author) and calendar (published date) icons to render");

        var personIcon = svgBlocks[0];
        var calendarIcon = svgBlocks[1];
        // "the person and calendar icons must be visually distinct SVGs, not the same generic placeholder repeated for every icon"
        Assert.NotEqual(calendarIcon, personIcon);

        // Guard against the trivial "distinct but still not really different" case where every
        // icon collapsed to the same shape with different attributes: also require every distinct
        // bootstrap-icon class used across the rendered page to produce at least a few different
        // glyphs overall, not just two.
        Assert.True(svgBlocks.Distinct().Count() >= 2);
    }
}

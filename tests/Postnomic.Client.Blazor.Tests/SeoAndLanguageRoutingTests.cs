using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.Blazor.Components.Pages;

namespace Postnomic.Client.Blazor.Tests;

/// <summary>
/// bUnit tests proving <see cref="Postnomic.Client.Blazor"/> honors
/// <see cref="PostnomicLanguageRouteStyle"/> in generated links (parity with
/// <c>Postnomic.Client.AspNetCore</c>'s <c>PostnomicRouteBuilder</c> usage) and emits the same
/// SEO data via Blazor's native <c>&lt;HeadContent&gt;</c>.
/// </summary>
public class SeoAndLanguageRoutingTests : BunitContext
{
    private readonly Mock<IPostnomicBlogService> _blogServiceMock;

    public SeoAndLanguageRoutingTests()
    {
        _blogServiceMock = new Mock<IPostnomicBlogService>();
        Services.AddSingleton(_blogServiceMock.Object);

        // HeadOutlet performs a best-effort JS interop call on first render; Loose mode returns
        // default(string) (null) for it instead of throwing.
        JSInterop.Mode = JSRuntimeMode.Loose;

        _blogServiceMock
            .Setup(s => s.RecordPageViewAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blogServiceMock
            .Setup(s => s.UpdateReadDurationAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void UseOptions(PostnomicLanguageRouteStyle style, string basePath = "/blog")
    {
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(
            Options.Create(new PostnomicClientOptions { BasePath = basePath, LanguageRouteStyle = style }));
    }

    private static PostnomicPostDetail CreatePost(
        string title = "Hello World",
        string author = "Jane Doe",
        string? authorSlug = "jane-doe",
        string language = "de",
        IReadOnlyList<string>? availableLanguages = null,
        string? excerpt = "A short teaser.",
        string? coverImageUrl = null,
        ICollection<PostnomicTag>? tags = null) => new()
    {
        Slug = "hello-world",
        Title = title,
        AuthorName = author,
        AuthorSlug = authorSlug,
        PublishedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        Excerpt = excerpt,
        Content = "<p>Body content.</p>",
        CoverImageUrl = coverImageUrl,
        Language = language,
        AvailableLanguages = availableLanguages ?? ["en", "de"],
        Tags = tags ?? [],
        CommentsEnabled = false,
    };

    private void SetupPost(PostnomicPostDetail post)
    {
        _blogServiceMock
            .Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Acme Blog", Slug = "acme-blog" });
    }

    private void SetupAuthorProfile(PostnomicAuthorProfile profile)
    {
        _blogServiceMock
            .Setup(s => s.GetAuthorProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
    }

    // ── PostPage — Prefix-style links ────────────────────────────────────────

    [Fact]
    public void PostPage_PrefixStyleWithLanguage_BackToBlogLinkUsesLangPrefix()
    {
        // Arrange
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        SetupPost(CreatePost());

        // Act
        var cut = Render<PostPage>(p => p
            .Add(x => x.PostSlug, "hello-world")
            .Add(x => x.Language, "de"));

        // Assert
        cut.FindAll("a[href='/de/blog']").Should().NotBeEmpty();
    }

    [Fact]
    public void PostPage_PrefixStyleWithLanguage_AuthorLinkUsesLangPrefix()
    {
        // Arrange
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        SetupPost(CreatePost(authorSlug: "jane-doe"));

        // Act
        var cut = Render<PostPage>(p => p
            .Add(x => x.PostSlug, "hello-world")
            .Add(x => x.Language, "de"));

        // Assert
        cut.FindAll("a[href='/de/blog/author/jane-doe']").Should().NotBeEmpty();
    }

    // ── PostPage — Suffix-style links (default behavior preserved) ──────────

    [Fact]
    public void PostPage_SuffixStyleWithLanguage_BackToBlogLinkAppendsLangSuffix()
    {
        // Arrange — default LanguageRouteStyle is Suffix.
        UseOptions(PostnomicLanguageRouteStyle.Suffix);
        SetupPost(CreatePost());

        // Act
        var cut = Render<PostPage>(p => p
            .Add(x => x.PostSlug, "hello-world")
            .Add(x => x.Language, "de"));

        // Assert
        cut.FindAll("a[href='/blog/de']").Should().NotBeEmpty();
    }

    [Fact]
    public void PostPage_SuffixStyleWithLanguage_AuthorLinkAppendsLangSuffix()
    {
        // Arrange
        UseOptions(PostnomicLanguageRouteStyle.Suffix);
        SetupPost(CreatePost(authorSlug: "jane-doe"));

        // Act
        var cut = Render<PostPage>(p => p
            .Add(x => x.PostSlug, "hello-world")
            .Add(x => x.Language, "de"));

        // Assert
        cut.FindAll("a[href='/blog/de/author/jane-doe']").Should().NotBeEmpty();
    }

    [Fact]
    public void PostPage_WithoutLanguage_LinksAreUnaffectedByRouteStyle()
    {
        // Arrange — no Language means no lang segment regardless of style (back-compat).
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        SetupPost(CreatePost(authorSlug: "jane-doe"));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "hello-world"));

        // Assert
        cut.FindAll("a[href='/blog']").Should().NotBeEmpty();
        cut.FindAll("a[href='/blog/author/jane-doe']").Should().NotBeEmpty();
    }

    // ── PostPage — <HeadContent> SEO ─────────────────────────────────────────

    [Fact]
    public void PostPage_PrefixStyleWithLanguage_HeadContent_RendersSelfReferentialCanonical()
    {
        // Arrange
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        SetupPost(CreatePost());

        // Act
        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<PostPage>(0);
            builder.AddComponentParameter(1, nameof(PostPage.PostSlug), "hello-world");
            builder.AddComponentParameter(2, nameof(PostPage.Language), "de");
            builder.CloseComponent();
        }));

        // Assert — canonicalizes to the *de* URL actually being rendered, not the default-lang one.
        var canonical = cut.Find("link[rel='canonical']");
        canonical.GetAttribute("href").Should().Be("http://localhost/de/blog/post/hello-world");
    }

    [Fact]
    public void PostPage_HeadContent_RendersOgTitle()
    {
        // Arrange
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        SetupPost(CreatePost(title: "Deep Dive into Blazor SEO"));

        // Act
        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<PostPage>(0);
            builder.AddComponentParameter(1, nameof(PostPage.PostSlug), "hello-world");
            builder.AddComponentParameter(2, nameof(PostPage.Language), "de");
            builder.CloseComponent();
        }));

        // Assert
        cut.Find("meta[property='og:title']").GetAttribute("content").Should().Be("Deep Dive into Blazor SEO");
    }

    [Fact]
    public void PostPage_HeadContent_RendersJsonLdBlogPostingScript()
    {
        // Arrange
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        SetupPost(CreatePost());

        // Act
        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<PostPage>(0);
            builder.AddComponentParameter(1, nameof(PostPage.PostSlug), "hello-world");
            builder.AddComponentParameter(2, nameof(PostPage.Language), "de");
            builder.CloseComponent();
        }));

        // Assert
        cut.Markup.Should().Contain("application/ld+json");
        cut.Markup.Should().Contain("\"@type\":\"BlogPosting\"");
    }

    [Fact]
    public void PostPage_HeadContent_RendersHreflangEnglishAlternate()
    {
        // Arrange — AvailableLanguages = ["en", "de"] per CreatePost default.
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        SetupPost(CreatePost());

        // Act
        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<PostPage>(0);
            builder.AddComponentParameter(1, nameof(PostPage.PostSlug), "hello-world");
            builder.AddComponentParameter(2, nameof(PostPage.Language), "de");
            builder.CloseComponent();
        }));

        // Assert
        var alternate = cut.Find("link[hreflang='en']");
        alternate.GetAttribute("href").Should().Be("http://localhost/blog/post/hello-world");
    }

    [Fact]
    public void PostPage_HeadContent_RendersOgLocaleInUnderscoreRegionFormat()
    {
        // Arrange — post.Language = "de" per CreatePost default.
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        SetupPost(CreatePost());

        // Act
        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<PostPage>(0);
            builder.AddComponentParameter(1, nameof(PostPage.PostSlug), "hello-world");
            builder.AddComponentParameter(2, nameof(PostPage.Language), "de");
            builder.CloseComponent();
        }));

        // Assert
        cut.Find("meta[property='og:locale']").GetAttribute("content").Should().Be("de_DE");
    }

    // ── AuthorPage — single <h1> ──────────────────────────────────────────────

    [Fact]
    public void AuthorPage_RendersExactlyOneH1ForAuthorName()
    {
        // Arrange
        SetupAuthorProfile(new PostnomicAuthorProfile { Name = "Jane Doe", Slug = "jane-doe" });

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert
        var h1s = cut.FindAll("h1");
        h1s.Should().HaveCount(1);
        h1s[0].TextContent.Should().Contain("Jane Doe");
    }

    // ── AuthorPage — language-aware links ────────────────────────────────────

    [Fact]
    public void AuthorPage_PrefixStyleWithLanguage_BackToBlogLinkUsesLangPrefix()
    {
        // Arrange
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        SetupAuthorProfile(new PostnomicAuthorProfile { Name = "Jane Doe", Slug = "jane-doe" });

        // Act
        var cut = Render<AuthorPage>(p => p
            .Add(x => x.AuthorSlug, "jane-doe")
            .Add(x => x.Language, "de"));

        // Assert
        cut.FindAll("a[href='/de/blog']").Should().NotBeEmpty();
    }

    [Fact]
    public void AuthorPage_PrefixStyleWithLanguage_RecentPostLinkUsesLangPrefix()
    {
        // Arrange
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        var profile = new PostnomicAuthorProfile
        {
            Name = "Jane Doe",
            Slug = "jane-doe",
            RecentPosts =
            [
                new PostnomicPostSummary
                {
                    Slug = "recent-post",
                    Title = "Recent Post",
                    AuthorName = "Jane Doe",
                    PublishedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                }
            ]
        };
        SetupAuthorProfile(profile);

        // Act
        var cut = Render<AuthorPage>(p => p
            .Add(x => x.AuthorSlug, "jane-doe")
            .Add(x => x.Language, "de"));

        // Assert
        cut.FindAll("a[href='/de/blog/post/recent-post']").Should().NotBeEmpty();
    }

    [Fact]
    public void AuthorPage_HeadContent_RendersProfilePageJsonLd()
    {
        // Arrange
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        SetupAuthorProfile(new PostnomicAuthorProfile { Name = "Jane Doe", Slug = "jane-doe" });

        // Act
        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<AuthorPage>(0);
            builder.AddComponentParameter(1, nameof(AuthorPage.AuthorSlug), "jane-doe");
            builder.AddComponentParameter(2, nameof(AuthorPage.Language), "de");
            builder.CloseComponent();
        }));

        // Assert
        cut.Markup.Should().Contain("\"@type\":\"ProfilePage\"");
        cut.Find("link[rel='canonical']").GetAttribute("href").Should().Be("http://localhost/de/blog/author/jane-doe");
    }

    // ── BlogPage — language-aware links + <HeadContent> ──────────────────────

    [Fact]
    public void BlogPage_PrefixStyleWithLanguage_PostLinkUsesLangPrefix()
    {
        // Arrange
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Acme Blog", Slug = "acme-blog" });
        _blogServiceMock
            .Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items =
                [
                    new PostnomicPostSummary
                    {
                        Slug = "hello-world",
                        Title = "Hello World",
                        AuthorName = "Jane Doe",
                        AuthorSlug = "jane-doe",
                        PublishedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    }
                ],
                Page = 1,
                PageSize = 5,
                TotalCount = 1,
                TotalPages = 1
            });

        // Act
        var cut = Render<BlogPage>(p => p.Add(x => x.Language, "de"));

        // Assert
        cut.FindAll("a[href='/de/blog/post/hello-world']").Should().NotBeEmpty();
        cut.FindAll("a[href='/de/blog/author/jane-doe']").Should().NotBeEmpty();
    }

    [Fact]
    public void BlogPage_HeadContent_RendersBlogJsonLdAndCanonical()
    {
        // Arrange
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Acme Blog", Slug = "acme-blog" });
        _blogServiceMock
            .Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary> { Items = [], Page = 1, PageSize = 5, TotalCount = 0, TotalPages = 0 });

        // Act
        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<BlogPage>(0);
            builder.AddComponentParameter(1, nameof(BlogPage.Language), "de");
            builder.CloseComponent();
        }));

        // Assert
        cut.Find("link[rel='canonical']").GetAttribute("href").Should().Be("http://localhost/de/blog");
        cut.Markup.Should().Contain("\"@type\":\"Blog\"");
    }
}

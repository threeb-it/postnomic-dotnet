// These tests deliberately exercise the obsolete PostnomicClientOptions.AlternateUrlResolver,
// which must keep working until it is removed in a future major version.
#pragma warning disable CS0618

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client;
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

    private void UseOptions(
        PostnomicLanguageRouteStyle style,
        string basePath = "/blog",
        Func<PostnomicPostDetail, IReadOnlyList<(string Language, string Url)>?>? alternateUrlResolver = null)
    {
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(
            Options.Create(new PostnomicClientOptions
            {
                BasePath = basePath,
                LanguageRouteStyle = style,
                AlternateUrlResolver = alternateUrlResolver,
            }));
    }

    private static PostnomicPostDetail CreatePost(
        string title = "Hello World",
        string author = "Jane Doe",
        string? authorSlug = "jane-doe",
        string language = "de",
        IReadOnlyList<string>? availableLanguages = null,
        string? excerpt = "A short teaser.",
        string? coverImageUrl = null,
        ICollection<PostnomicTag>? tags = null,
        DateTime? publishedAt = null) => new()
        {
            Slug = "hello-world",
            Title = title,
            AuthorName = author,
            AuthorSlug = authorSlug,
            PublishedAt = publishedAt ?? new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
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
        Assert.NotEmpty(cut.FindAll("a[href='/de/blog']"));
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
        Assert.NotEmpty(cut.FindAll("a[href='/de/blog/author/jane-doe']"));
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
        Assert.NotEmpty(cut.FindAll("a[href='/blog/de']"));
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
        Assert.NotEmpty(cut.FindAll("a[href='/blog/de/author/jane-doe']"));
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
        Assert.NotEmpty(cut.FindAll("a[href='/blog']"));
        Assert.NotEmpty(cut.FindAll("a[href='/blog/author/jane-doe']"));
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
        Assert.Equal("http://localhost/de/blog/post/hello-world", canonical.GetAttribute("href"));
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
        Assert.Equal("Deep Dive into Blazor SEO", cut.Find("meta[property='og:title']").GetAttribute("content"));
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
        Assert.Contains("application/ld+json", cut.Markup);
        Assert.Contains("\"@type\":\"BlogPosting\"", cut.Markup);
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

        // Assert — under Prefix there is no bare /blog/... route, so the "en" alternate must carry
        // its own language segment rather than being bare, or it would 404.
        var alternate = cut.Find("link[hreflang='en']");
        Assert.Equal("http://localhost/en/blog/post/hello-world", alternate.GetAttribute("href"));
    }

    [Fact]
    public void PostPage_HeadContent_XDefaultIsTheDefaultLanguageUrl_NotTheCurrentPageCanonical()
    {
        // Arrange — AvailableLanguages = ["en", "de"] per CreatePost default, so "en" is the
        // default-language URL. Under Prefix routing only /{lang}/blog/... routes are registered
        // (no bare /blog/... route), so that URL must be /en/blog/post/hello-world, not bare.
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

        // Assert — x-default points at the default-language (en) URL, not this de page's own
        // self-referential canonical.
        var xDefault = cut.Find("link[hreflang='x-default']");
        var canonical = cut.Find("link[rel='canonical']");
        Assert.Equal("http://localhost/en/blog/post/hello-world", xDefault.GetAttribute("href"));
        Assert.NotEqual(canonical.GetAttribute("href"), xDefault.GetAttribute("href"));
    }

    [Fact]
    public void PostPage_HeadContent_XDefault_OnEnglishVariant_IsTheSameDefaultLanguageUrl()
    {
        // Arrange — companion to the "de" variant test above. bUnit only allows one HeadOutlet
        // subscriber per test context, so each language variant is rendered in its own test
        // (fresh BunitContext) rather than both in a single test; both asserting the same known
        // constant proves the de and en pages emit an identical x-default, which is the actual
        // requirement (one consistent x-default across the whole language cluster).
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        SetupPost(CreatePost());

        // Act
        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<PostPage>(0);
            builder.AddComponentParameter(1, nameof(PostPage.PostSlug), "hello-world");
            builder.AddComponentParameter(2, nameof(PostPage.Language), "en");
            builder.CloseComponent();
        }));

        // Assert — same x-default value as the "de" variant test, even though this page's own
        // canonical is the "en" URL rather than the "de" one; under Prefix that "en" URL is itself
        // language-prefixed (/en/blog/...), never bare.
        Assert.Equal(
            "http://localhost/en/blog/post/hello-world",
            cut.Find("link[hreflang='x-default']").GetAttribute("href"));
    }

    [Fact]
    public void PostPage_HeadContent_PrefixStyle_NoHreflangAlternateNorXDefaultIsABareUrl()
    {
        // Guard test (elimination of the "bare URL in Prefix mode -> 404" bug class): under
        // Prefix, only /{lang}/blog/... routes are registered — a bare /blog/post/hello-world
        // 404s. Every rendered hreflang alternate (including x-default) must be language-prefixed.
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        SetupPost(CreatePost());

        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<PostPage>(0);
            builder.AddComponentParameter(1, nameof(PostPage.PostSlug), "hello-world");
            builder.AddComponentParameter(2, nameof(PostPage.Language), "de");
            builder.CloseComponent();
        }));

        const string bareUrl = "http://localhost/blog/post/hello-world";
        var alternateHrefs = cut.FindAll("link[hreflang]")
            .Select(el => el.GetAttribute("href"))
            .ToList();

        Assert.NotEmpty(alternateHrefs);
        Assert.DoesNotContain(bareUrl, alternateHrefs);
        Assert.All(alternateHrefs, href => Assert.True(href != null
            && System.Text.RegularExpressions.Regex.IsMatch(href, "^http://localhost/[a-z]{2}/blog/post/hello-world$")));
    }

    [Fact]
    public void PostPage_HeadContent_RendersUtcNormalizedPublishedTimestamp_ForUnspecifiedKindDate()
    {
        // Arrange — Unspecified-kind, mirroring what System.Text.Json produces for the API's
        // zoneless "publishedAt" field; must still render with a "Z" designator, host-tz
        // independent (see PostnomicSeoBuilderTests for the equivalent unit-level coverage).
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        SetupPost(CreatePost(publishedAt: new DateTime(2026, 7, 2, 13, 30, 0, DateTimeKind.Unspecified)));

        // Act
        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<PostPage>(0);
            builder.AddComponentParameter(1, nameof(PostPage.PostSlug), "hello-world");
            builder.AddComponentParameter(2, nameof(PostPage.Language), "de");
            builder.CloseComponent();
        }));

        // Assert
        Assert.Equal(
            "2026-07-02T13:30:00.0000000Z",
            cut.Find("meta[property='article:published_time']").GetAttribute("content"));
        Assert.Contains("\"datePublished\":\"2026-07-02T13:30:00.0000000Z\"", cut.Markup);
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
        Assert.Equal("de_DE", cut.Find("meta[property='og:locale']").GetAttribute("content"));
    }

    // ── PostPage — <HeadContent> SEO: AlternateUrlResolver override ─────────

    [Fact]
    public void PostPage_HeadContent_NoneStyle_WithoutResolver_SharedUrlAcrossLanguages_RendersOnlyOneAlternate()
    {
        // Reproduces the live production defect: under None style no language ever gets its own
        // URL segment, so without a resolver every language's composed alternate is the identical
        // bare URL — that must now collapse to one honest hreflang entry, not two duplicates.
        UseOptions(PostnomicLanguageRouteStyle.None);
        SetupPost(CreatePost());

        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<PostPage>(0);
            builder.AddComponentParameter(1, nameof(PostPage.PostSlug), "hello-world");
            builder.CloseComponent();
        }));

        // "x-default" is always rendered separately (PostnomicSeoModel.XDefaultUrl); only the
        // per-language hreflang alternates are what got de-duplicated.
        Assert.Single(cut.FindAll("link[rel='alternate']:not([hreflang='x-default'])"));
    }

    [Fact]
    public void PostPage_HeadContent_AlternateUrlResolverConfigured_RendersTheResolvedPerLanguageUrls()
    {
        UseOptions(PostnomicLanguageRouteStyle.None, alternateUrlResolver: post =>
        [
            ("de", "/blog/post/kurze-hoerbucher"),
            ("en", "/blog/post/kurze-hoerbucher-en"),
        ]);
        SetupPost(CreatePost());

        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<PostPage>(0);
            builder.AddComponentParameter(1, nameof(PostPage.PostSlug), "hello-world");
            builder.CloseComponent();
        }));

        Assert.Equal(
            "http://localhost/blog/post/kurze-hoerbucher",
            cut.Find("link[hreflang='de']").GetAttribute("href"));
        Assert.Equal(
            "http://localhost/blog/post/kurze-hoerbucher-en",
            cut.Find("link[hreflang='en']").GetAttribute("href"));
    }

    // ── PostPage — <HeadContent> SEO: IPostnomicAlternateUrlProvider (the supported seam) ──

    /// <summary>
    /// The Blazor half of the lockstep pair: a DI-registered
    /// <see cref="IPostnomicAlternateUrlProvider"/> that depends on
    /// <see cref="IPostnomicBlogService"/> reaches the rendered hreflang links, asynchronously and
    /// with no cache-warming. The ASP.NET Core equivalent asserts the same two URLs.
    /// </summary>
    [Fact]
    public void PostPage_HeadContent_AlternateUrlProviderRegistered_RendersTheResolvedPerLanguageUrls()
    {
        UseOptions(PostnomicLanguageRouteStyle.None);
        Services.AddPostnomicAlternateUrlProvider<BlogServiceBackedAlternateUrlProvider>();
        SetupPost(CreatePost());

        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<PostPage>(0);
            builder.AddComponentParameter(1, nameof(PostPage.PostSlug), "hello-world");
            builder.CloseComponent();
        }));

        Assert.Equal(
            "http://localhost/blog/post/kurze-hoerbucher",
            cut.Find("link[hreflang='de']").GetAttribute("href"));
        Assert.Equal(
            "http://localhost/blog/post/kurze-hoerbucher-en",
            cut.Find("link[hreflang='en']").GetAttribute("href"));
    }

    /// <summary>A registered provider wins over the obsolete options callback.</summary>
    [Fact]
    public void PostPage_HeadContent_ProviderTakesPrecedenceOverTheObsoleteResolver()
    {
        UseOptions(PostnomicLanguageRouteStyle.None, alternateUrlResolver: _ =>
        [
            ("de", "/blog/post/legacy-de"),
        ]);
        Services.AddPostnomicAlternateUrlProvider<BlogServiceBackedAlternateUrlProvider>();
        SetupPost(CreatePost());

        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<PostPage>(0);
            builder.AddComponentParameter(1, nameof(PostPage.PostSlug), "hello-world");
            builder.CloseComponent();
        }));

        Assert.Equal(
            "http://localhost/blog/post/kurze-hoerbucher",
            cut.Find("link[hreflang='de']").GetAttribute("href"));
    }

    // ── PostPage — <HeadContent> SEO: description fallback (no excerpt) ─────

    [Fact]
    public void PostPage_HeadContent_DescriptionFallback_DoesNotLeakMarkdownIntoTheMetaTag()
    {
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        var post = CreatePost(title: "Geteilter Artikel", excerpt: null) with
        {
            Content = "# Geteilter Artikel\n\n" +
                "![Ein Bild, das nicht in der Beschreibung landen darf.](https://cdn.example.com/img.jpg)\n\n" +
                "Dies ist der eigentliche Textinhalt, der in der Beschreibung erscheinen soll.",
        };
        SetupPost(post);

        var cut = Render(HeadOutletTestHelper.WithHeadOutlet(builder =>
        {
            builder.OpenComponent<PostPage>(0);
            builder.AddComponentParameter(1, nameof(PostPage.PostSlug), "hello-world");
            builder.AddComponentParameter(2, nameof(PostPage.Language), "de");
            builder.CloseComponent();
        }));

        var description = cut.Find("meta[name='description']").GetAttribute("content");

        Assert.NotNull(description);
        Assert.DoesNotContain("Geteilter Artikel", description);
        Assert.DoesNotContain("!", description);
        Assert.DoesNotContain("\n", description);
        Assert.Contains("eigentliche Textinhalt", description);
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
        Assert.Single(h1s);
        Assert.Contains("Jane Doe", h1s[0].TextContent);
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
        Assert.NotEmpty(cut.FindAll("a[href='/de/blog']"));
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
        Assert.NotEmpty(cut.FindAll("a[href='/de/blog/post/recent-post']"));
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
        Assert.Contains("\"@type\":\"ProfilePage\"", cut.Markup);
        Assert.Equal("http://localhost/de/blog/author/jane-doe", cut.Find("link[rel='canonical']").GetAttribute("href"));
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
        Assert.NotEmpty(cut.FindAll("a[href='/de/blog/post/hello-world']"));
        Assert.NotEmpty(cut.FindAll("a[href='/de/blog/author/jane-doe']"));
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
        Assert.Equal("http://localhost/de/blog", cut.Find("link[rel='canonical']").GetAttribute("href"));
        Assert.Contains("\"@type\":\"Blog\"", cut.Markup);
    }
}

/// <summary>
/// A host provider shaped like a real one — it takes the SDK's own
/// <see cref="IPostnomicBlogService"/>, which is precisely what the obsolete options-callback
/// wiring could not do.
/// </summary>
internal sealed class BlogServiceBackedAlternateUrlProvider(IPostnomicBlogService blogService)
    : IPostnomicAlternateUrlProvider
{
    public IPostnomicBlogService BlogService { get; } = blogService;

    public ValueTask<IReadOnlyList<(string Language, string Url)>?> GetAlternatesAsync(
        PostnomicPostDetail post,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<(string, string)> alternates =
        [
            ("de", "/blog/post/kurze-hoerbucher"),
            ("en", "/blog/post/kurze-hoerbucher-en"),
        ];
        return ValueTask.FromResult<IReadOnlyList<(string Language, string Url)>?>(alternates);
    }
}

#pragma warning restore CS0618

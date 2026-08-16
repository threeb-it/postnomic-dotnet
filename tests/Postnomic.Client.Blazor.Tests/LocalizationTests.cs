using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.Blazor.Components.Pages;
using Postnomic.Client.Blazor.Localization;

namespace Postnomic.Client.Blazor.Tests;

/// <summary>
/// Unit tests for the localization primitives (<see cref="PostnomicUiStrings"/>,
/// <see cref="PostnomicDateFormatter"/>, <see cref="PostnomicUiStringOverrides"/>) that back the
/// Blazor components' <c>Language</c> parameter, plus rendering tests proving the built-in English
/// strings are unchanged, the built-in German translations render, an unrecognized language falls
/// back to English rather than a raw key or blank text, and a consumer override wins over both.
/// </summary>
public class LocalizationTests : BunitContext
{
    private readonly Mock<IPostnomicBlogService> _blogServiceMock;

    public LocalizationTests()
    {
        _blogServiceMock = new Mock<IPostnomicBlogService>();
        Services.AddSingleton(_blogServiceMock.Object);

        _blogServiceMock
            .Setup(s => s.RecordPageViewAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blogServiceMock
            .Setup(s => s.UpdateReadDurationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void UseOptions(PostnomicClientOptions? options = null) =>
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(Options.Create(options ?? new PostnomicClientOptions()));

    // ── PostnomicUiStrings: English is the untouched baseline ──────────────────

    [Theory]
    [InlineData("Common.BackToBlog", "← Back to blog")]
    [InlineData("Common.NoPostsFound", "No posts found.")]
    [InlineData("Common.ClearFilter", "✕ Clear filter")]
    [InlineData("BlogPage.ClearAllFilters", "✕ Clear all")]
    [InlineData("BlogPage.ReadMore", "Read More →")]
    [InlineData("BlogPage.Pager.Previous", "← Previous")]
    [InlineData("BlogPage.Pager.Next", "Next →")]
    [InlineData("PostPage.LeaveAComment", "Leave a comment")]
    [InlineData("PostPage.PostComment", "Post comment")]
    [InlineData("PostPage.NoCommentsYet", "No comments yet.")]
    [InlineData("PostPage.CommentsClosed", "Comments are closed for this post.")]
    [InlineData("AuthorPage.Connect", "Connect")]
    [InlineData("AuthorPage.Website", "Website")]
    [InlineData("SearchBox.Placeholder", "Search posts…")]
    public void Get_English_MatchesOriginalHardcodedText(string key, string expected)
    {
        Assert.Equal(expected, PostnomicUiStrings.Get(key, "en", overrides: null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")] // recognized ISO code, but not one the catalog ships a translation for
    [InlineData("xx")] // not a real ISO code at all
    public void Get_NullOrUnrecognizedLanguage_FallsBackToEnglish(string? language)
    {
        Assert.Equal("← Back to blog", PostnomicUiStrings.Get("Common.BackToBlog", language, overrides: null));
        Assert.Equal("Read More →", PostnomicUiStrings.Get("BlogPage.ReadMore", language, overrides: null));
    }

    // ── PostnomicUiStrings: German built-ins ────────────────────────────────────

    [Theory]
    [InlineData("Common.BackToBlog", "← Zurück zum Blog")]
    [InlineData("Common.NoPostsFound", "Keine Beiträge gefunden.")]
    [InlineData("BlogPage.ReadMore", "Weiterlesen →")]
    [InlineData("BlogPage.Pager.Previous", "← Zurück")]
    [InlineData("BlogPage.Pager.Next", "Weiter →")]
    [InlineData("PostPage.LeaveAComment", "Kommentar schreiben")]
    [InlineData("PostPage.PostComment", "Kommentar absenden")]
    [InlineData("AuthorPage.Connect", "Kontakt")]
    [InlineData("AuthorList.Title", "Autor:innen")]
    public void Get_German_ReturnsGermanTranslation(string key, string expected)
    {
        Assert.Equal(expected, PostnomicUiStrings.Get(key, "de", overrides: null));
    }

    [Fact]
    public void Get_German_RegionQualifiedCode_NormalizesToGerman()
    {
        Assert.Equal("← Zurück zum Blog", PostnomicUiStrings.Get("Common.BackToBlog", "de-DE", overrides: null));
    }

    // ── PostnomicUiStrings: consumer overrides win ──────────────────────────────

    [Fact]
    public void Get_WithOverrideForResolvedLanguage_ReturnsOverride()
    {
        var overrides = new PostnomicUiStringOverrides()
            .Set("de", "PostPage.LeaveAComment", "Sag was dazu");

        Assert.Equal("Sag was dazu", PostnomicUiStrings.Get("PostPage.LeaveAComment", "de", overrides));
        // Every other German key is untouched by the single override.
        Assert.Equal("Kommentar absenden", PostnomicUiStrings.Get("PostPage.PostComment", "de", overrides));
    }

    [Fact]
    public void Get_WithOverrideForEnglish_AlsoAppliesToUnrecognizedLanguages()
    {
        // An unrecognized language normalizes to English, so an "en" override is the one a
        // consumer should register to cover it too.
        var overrides = new PostnomicUiStringOverrides()
            .Set("en", "Common.BackToBlog", "Back to the blog, please");

        Assert.Equal("Back to the blog, please", PostnomicUiStrings.Get("Common.BackToBlog", "xx", overrides));
    }

    [Fact]
    public void GetFormat_FormatsCompositeStringWithArgs()
    {
        Assert.Equal("Comments (3)", PostnomicUiStrings.GetFormat("PostPage.CommentsHeading", "en", null, 3));
        Assert.Equal("Kommentare (3)", PostnomicUiStrings.GetFormat("PostPage.CommentsHeading", "de", null, 3));
    }

    [Theory]
    [InlineData(0, "comments")]
    [InlineData(1, "comment")]
    [InlineData(2, "comments")]
    public void Pluralize_English_MatchesOriginalTernary(int count, string expected)
    {
        Assert.Equal(expected, PostnomicUiStrings.Pluralize(count, "Common.Comment", "Common.Comments", "en", null));
    }

    // ── PostnomicUiStringOverrides ───────────────────────────────────────────────

    [Fact]
    public void PostnomicUiStringOverrides_TryGet_UnsetKey_ReturnsFalse()
    {
        var overrides = new PostnomicUiStringOverrides();
        Assert.False(overrides.TryGet("de", "Common.BackToBlog", out _));
    }

    [Fact]
    public void PostnomicUiStringOverrides_Set_IsCaseInsensitiveOnLanguage()
    {
        var overrides = new PostnomicUiStringOverrides().Set("DE", "Common.BackToBlog", "Zurück");
        Assert.True(overrides.TryGet("de", "Common.BackToBlog", out var value));
        Assert.Equal("Zurück", value);
    }

    // ── PostnomicDateFormatter ───────────────────────────────────────────────────

    [Fact]
    public void LongDate_English_MatchesOriginalHardcodedPattern()
    {
        var date = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);

        // The exact call the components made before localization — the new helper must produce
        // byte-identical output for English/null/unrecognized languages.
        Assert.Equal(date.ToString("MMMM dd, yyyy"), PostnomicDateFormatter.LongDate(date, null));
        Assert.Equal(date.ToString("MMMM dd, yyyy"), PostnomicDateFormatter.LongDate(date, "en"));
        Assert.Equal(date.ToString("MMMM dd, yyyy"), PostnomicDateFormatter.LongDate(date, "xx"));
    }

    [Fact]
    public void LongDate_German_UsesGermanDayMonthYearOrder()
    {
        var date = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal("16. August 2026", PostnomicDateFormatter.LongDate(date, "de"));
    }

    [Fact]
    public void ShortDateTime_English_MatchesOriginalHardcodedPattern()
    {
        var date = new DateTime(2026, 8, 16, 14, 30, 0, DateTimeKind.Utc);

        Assert.Equal(date.ToString("MMM dd, yyyy · HH:mm"), PostnomicDateFormatter.ShortDateTime(date, null));
    }

    [Fact]
    public void ShortDateTime_German_UsesGermanFormat()
    {
        var date = new DateTime(2026, 8, 16, 14, 30, 0, DateTimeKind.Utc);

        Assert.Equal("16. Aug. 2026 · 14:30", PostnomicDateFormatter.ShortDateTime(date, "de"));
    }

    [Fact]
    public void MonthYear_English_MatchesOriginalHardcodedPattern()
    {
        var date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(date.ToString("MMM yyyy"), PostnomicDateFormatter.MonthYear(date, null));
    }

    [Fact]
    public void MonthYear_German_UsesGermanAbbreviation()
    {
        var date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        // .NET's de-DE abbreviated month name for a standalone "MMM yyyy" pattern (no day token)
        // is "Aug" without a trailing period — verify against the culture directly rather than a
        // literal, since the exact abbreviation is ICU/.NET's call, not this package's.
        var expected = date.ToString("MMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
        Assert.Equal(expected, PostnomicDateFormatter.MonthYear(date, "de"));
        Assert.Equal("Aug 2026", expected);
    }

    // ── BlogPage rendering: German chrome ────────────────────────────────────────

    private void SetupEmptyBlog()
    {
        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Mein Blog", Slug = "mein-blog" });
        _blogServiceMock
            .Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items = [],
                Page = 1,
                PageSize = 5,
                TotalCount = 0,
                TotalPages = 0
            });
    }

    [Fact]
    public void BlogPage_Language_de_RendersGermanEmptyState()
    {
        UseOptions();
        SetupEmptyBlog();

        var cut = Render<BlogPage>(p => p.Add(x => x.Language, "de"));

        Assert.Contains("Keine Beiträge gefunden.", cut.Markup);
        Assert.DoesNotContain("No posts found", cut.Markup);
    }

    [Fact]
    public void BlogPage_Language_de_RendersGermanSidebarWidgetTitles()
    {
        UseOptions();
        SetupEmptyBlog();

        var cut = Render<BlogPage>(p => p.Add(x => x.Language, "de"));

        Assert.Contains("Suche", cut.Markup);
        Assert.Contains("Schlagwörter", cut.Markup);
        Assert.Contains("Kategorien", cut.Markup);
        Assert.Contains("Autor:innen", cut.Markup);
    }

    [Fact]
    public void BlogPage_UnrecognizedLanguage_FallsBackToEnglishEmptyState()
    {
        UseOptions();
        SetupEmptyBlog();

        var cut = Render<BlogPage>(p => p.Add(x => x.Language, "xx"));

        Assert.Contains("No posts found.", cut.Markup);
        Assert.DoesNotContain("Keine Beiträge gefunden", cut.Markup);
    }

    [Fact]
    public void BlogPage_ConsumerOverride_WinsOverBuiltInGerman()
    {
        var options = new PostnomicClientOptions
        {
            UiStrings = new PostnomicUiStringOverrides().Set("de", "Common.NoPostsFound", "Noch nichts hier.")
        };
        UseOptions(options);
        SetupEmptyBlog();

        var cut = Render<BlogPage>(p => p.Add(x => x.Language, "de"));

        Assert.Contains("Noch nichts hier.", cut.Markup);
        Assert.DoesNotContain("Keine Beiträge gefunden", cut.Markup);
    }

    // ── PostPage rendering: German chrome ────────────────────────────────────────

    private static PostnomicPostDetail CreateClosedPost() => new()
    {
        Slug = "test-post",
        Title = "Test Post",
        AuthorName = "Jane Doe",
        PublishedAt = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc),
        Content = "<p>Hello world</p>",
        CommentsEnabled = false,
        Comments = []
    };

    [Fact]
    public void PostPage_Language_de_RendersGermanBackLinkAndDate()
    {
        UseOptions();
        _blogServiceMock
            .Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClosedPost());

        var cut = Render<PostPage>(p => p
            .Add(x => x.PostSlug, "test-post")
            .Add(x => x.Language, "de"));

        Assert.Contains("← Zurück zum Blog", cut.Markup);
        Assert.Contains("16. August 2026", cut.Markup);
        Assert.DoesNotContain("Back to blog", cut.Markup);
    }

    [Fact]
    public void PostPage_Language_de_RendersGermanClosedCommentsMessage()
    {
        UseOptions();
        _blogServiceMock
            .Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClosedPost());

        var cut = Render<PostPage>(p => p
            .Add(x => x.PostSlug, "test-post")
            .Add(x => x.Language, "de"));

        Assert.Contains("Für diesen Beitrag sind keine Kommentare mehr möglich.", cut.Markup);
    }

    // ── AuthorPage rendering: German chrome ──────────────────────────────────────

    [Fact]
    public void AuthorPage_Language_de_RendersGermanBackLinkAndConnector()
    {
        UseOptions();
        _blogServiceMock
            .Setup(s => s.GetAuthorProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicAuthorProfile
            {
                Name = "Jane Doe",
                Slug = "jane-doe",
                JobTitle = "Senior Dev",
                Company = "Acme Inc",
                PostCount = 3
            });

        var cut = Render<AuthorPage>(p => p
            .Add(x => x.AuthorSlug, "jane-doe")
            .Add(x => x.Language, "de"));

        Assert.Contains("← Zurück zum Blog", cut.Markup);
        Assert.Contains(" bei ", cut.Markup);
        Assert.Contains("3 Beiträge", cut.Markup);
    }
}

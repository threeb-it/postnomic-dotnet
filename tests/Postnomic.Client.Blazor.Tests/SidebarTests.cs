using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.Blazor.Components.Sidebar;

namespace Postnomic.Client.Blazor.Tests;

/// <summary>
/// bUnit tests for the sidebar components:
/// <see cref="EstimatedReadTime"/>, <see cref="TagCloud"/>,
/// <see cref="CategoryList"/>, <see cref="AuthorList"/>,
/// <see cref="TopCommentedPosts"/>, and <see cref="MostReadPosts"/>.
/// </summary>
public class SidebarTests : BunitContext
{
    private readonly Mock<IPostnomicBlogService> _blogServiceMock;

    public SidebarTests()
    {
        _blogServiceMock = new Mock<IPostnomicBlogService>();
        Services.AddSingleton(_blogServiceMock.Object);
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(
            Options.Create(new PostnomicClientOptions()));
    }

    // ── EstimatedReadTime ─────────────────────────────────────────────────────

    [Fact]
    public void EstimatedReadTime_WhenContentIsNull_RendersNoContentMessage()
    {
        // Act
        var cut = Render<EstimatedReadTime>(p =>
            p.Add(c => c.PostContent, (string?)null));

        // Assert
        Assert.Contains("No content available", cut.Markup);
    }

    [Fact]
    public void EstimatedReadTime_WhenContentIsEmpty_RendersNoContentMessage()
    {
        // Act
        var cut = Render<EstimatedReadTime>(p =>
            p.Add(c => c.PostContent, ""));

        // Assert
        Assert.Contains("No content available", cut.Markup);
    }

    [Fact]
    public void EstimatedReadTime_WhenContentIsWhitespaceOnly_RendersNoContentMessage()
    {
        // Act
        var cut = Render<EstimatedReadTime>(p =>
            p.Add(c => c.PostContent, "   "));

        // Assert
        Assert.Contains("No content available", cut.Markup);
    }

    [Theory]
    [InlineData(200, 1)]   // exactly 200 words → 1 minute (ceil(200/200))
    [InlineData(201, 2)]   // 201 words → 2 minutes (ceil(201/200))
    [InlineData(400, 2)]   // 400 words → 2 minutes (ceil(400/200))
    [InlineData(1, 1)]     // 1 word → minimum 1 minute
    public void EstimatedReadTime_CalculatesCorrectReadTime(int wordCount, int expectedMinutes)
    {
        // Arrange — create plain text with the required number of words
        var content = string.Join(" ", Enumerable.Repeat("word", wordCount));

        // Act
        var cut = Render<EstimatedReadTime>(p =>
            p.Add(c => c.PostContent, content));

        // Assert — the displayed number should match the expected minutes
        Assert.Contains(expectedMinutes.ToString(), cut.Markup);
    }

    [Fact]
    public void EstimatedReadTime_SingleMinute_RendersSingularLabel()
    {
        // Arrange — 100 words → 1 minute
        var content = string.Join(" ", Enumerable.Repeat("word", 100));

        // Act
        var cut = Render<EstimatedReadTime>(p =>
            p.Add(c => c.PostContent, content));

        // Assert
        Assert.Contains("minute", cut.Markup);
        Assert.DoesNotContain("minutes", cut.Markup);
    }

    [Fact]
    public void EstimatedReadTime_MultipleMinutes_RendersPluralLabel()
    {
        // Arrange — 401 words → 3 minutes
        var content = string.Join(" ", Enumerable.Repeat("word", 401));

        // Act
        var cut = Render<EstimatedReadTime>(p =>
            p.Add(c => c.PostContent, content));

        // Assert
        Assert.Contains("minutes", cut.Markup);
    }

    [Fact]
    public void EstimatedReadTime_WithHtmlContent_StripsTags_BeforeCountingWords()
    {
        // Arrange — 10 HTML-wrapped words → 1 minute
        const string html = "<p>one two three four five six seven eight nine ten</p>";

        // Act
        var cut = Render<EstimatedReadTime>(p =>
            p.Add(c => c.PostContent, html));

        // Assert — word count should be rendered and be 10
        Assert.Contains("10", cut.Markup);
    }

    [Fact]
    public void EstimatedReadTime_RendersWordCount()
    {
        // Arrange — exactly 50 words
        var content = string.Join(" ", Enumerable.Repeat("word", 50));

        // Act
        var cut = Render<EstimatedReadTime>(p =>
            p.Add(c => c.PostContent, content));

        // Assert — word count "50" should be displayed
        Assert.Contains("50", cut.Markup);
    }

    // ── TagCloud ──────────────────────────────────────────────────────────────

    [Fact]
    public void TagCloud_WhenTagsLoaded_RendersAButtonPerTag()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicTag>
            {
                new() { Name = "C#", Slug = "csharp", PostCount = 5 },
                new() { Name = ".NET", Slug = "dotnet", PostCount = 3 }
            });

        // Act
        var cut = Render<TagCloud>();

        // Assert — one button per tag
        var tagButtons = cut.FindAll("button.btn");
        Assert.Equal(2, tagButtons.Count());
    }

    [Fact]
    public void TagCloud_WhenTagsLoaded_RendersTagNames()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicTag>
            {
                new() { Name = "Blazor", Slug = "blazor", PostCount = 1 }
            });

        // Act
        var cut = Render<TagCloud>();

        // Assert
        Assert.Contains("Blazor", cut.Markup);
    }

    [Fact]
    public void TagCloud_WhenTagsLoaded_RendersPostCountBadge()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicTag>
            {
                new() { Name = "Blazor", Slug = "blazor", PostCount = 7 }
            });

        // Act
        var cut = Render<TagCloud>();

        // Assert
        Assert.Contains("7", cut.Markup);
    }

    [Fact]
    public void TagCloud_WhenTagListIsEmpty_RendersNoTagsMessage()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicTag>());

        // Act
        var cut = Render<TagCloud>();

        // Assert
        Assert.Contains("No tags found", cut.Markup);
    }

    [Fact]
    public void TagCloud_WhenActiveTagSlugSet_MarksActiveButtonDifferently()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicTag>
            {
                new() { Name = "C#", Slug = "csharp", PostCount = 2 },
                new() { Name = ".NET", Slug = "dotnet", PostCount = 4 }
            });

        // Act
        var cut = Render<TagCloud>(p =>
            p.Add(c => c.ActiveTagSlug, "csharp"));

        // Assert — active tag button should have btn-secondary class
        var activeBtn = cut.FindAll("button.btn-secondary");
        Assert.Single(activeBtn);
        Assert.Contains("C#", activeBtn[0].TextContent);
    }

    [Fact]
    public void TagCloud_WhenActiveTagSlugSet_ShowsClearFilterButton()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicTag>
            {
                new() { Name = "Tag", Slug = "tag", PostCount = 1 }
            });

        // Act
        var cut = Render<TagCloud>(p =>
            p.Add(c => c.ActiveTagSlug, "tag"));

        // Assert
        Assert.Contains("Clear filter", cut.Markup);
    }

    // ── CategoryList ──────────────────────────────────────────────────────────

    [Fact]
    public void CategoryList_WhenCategoriesLoaded_RendersListItems()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicCategory>
            {
                new() { Name = "Tutorials", Slug = "tutorials", PostCount = 10 },
                new() { Name = "News", Slug = "news", PostCount = 2 }
            });

        // Act
        var cut = Render<CategoryList>();

        // Assert
        var items = cut.FindAll("li.list-group-item");
        Assert.Equal(2, items.Count());
    }

    [Fact]
    public void CategoryList_WhenCategoriesLoaded_RendersCategoryNames()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicCategory>
            {
                new() { Name = "Architecture", Slug = "architecture", PostCount = 3 }
            });

        // Act
        var cut = Render<CategoryList>();

        // Assert
        Assert.Contains("Architecture", cut.Markup);
    }

    [Fact]
    public void CategoryList_WhenCategoriesLoaded_RendersPostCountBadges()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicCategory>
            {
                new() { Name = "DDD", Slug = "ddd", PostCount = 6 }
            });

        // Act
        var cut = Render<CategoryList>();

        // Assert — badge showing count
        Assert.Contains("6", cut.Find("span.badge").TextContent);
    }

    [Fact]
    public void CategoryList_WhenEmpty_RendersNoCategoriesMessage()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicCategory>());

        // Act
        var cut = Render<CategoryList>();

        // Assert
        Assert.Contains("No categories found", cut.Markup);
    }

    [Fact]
    public void CategoryList_WhenActiveCategorySlugSet_ShowsClearFilterButton()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicCategory>
            {
                new() { Name = "Tutorials", Slug = "tutorials", PostCount = 5 }
            });

        // Act
        var cut = Render<CategoryList>(p =>
            p.Add(c => c.ActiveCategorySlug, "tutorials"));

        // Assert
        Assert.Contains("Clear filter", cut.Markup);
    }

    // ── AuthorList ────────────────────────────────────────────────────────────

    [Fact]
    public void AuthorList_WhenAuthorsLoaded_RendersListItems()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicAuthor>
            {
                new() { Name = "Jane Doe", PostCount = 8 },
                new() { Name = "John Smith", PostCount = 3 }
            });

        // Act
        var cut = Render<AuthorList>();

        // Assert
        var items = cut.FindAll("li.list-group-item");
        Assert.Equal(2, items.Count());
    }

    [Fact]
    public void AuthorList_WhenAuthorsLoaded_RendersAuthorNames()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicAuthor>
            {
                new() { Name = "Alice Brown", PostCount = 4 }
            });

        // Act
        var cut = Render<AuthorList>();

        // Assert
        Assert.Contains("Alice Brown", cut.Markup);
    }

    [Fact]
    public void AuthorList_WhenAuthorsLoaded_RendersPostCountBadges()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicAuthor>
            {
                new() { Name = "Bob", PostCount = 12 }
            });

        // Act
        var cut = Render<AuthorList>();

        // Assert
        Assert.Contains("12", cut.Find("span.badge").TextContent);
    }

    [Fact]
    public void AuthorList_WhenEmpty_RendersNoAuthorsMessage()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicAuthor>());

        // Act
        var cut = Render<AuthorList>();

        // Assert
        Assert.Contains("No authors found", cut.Markup);
    }

    [Fact]
    public void AuthorList_WhenActiveAuthorNameSet_ShowsClearFilterButton()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicAuthor>
            {
                new() { Name = "Jane Doe", PostCount = 2 }
            });

        // Act
        var cut = Render<AuthorList>(p =>
            p.Add(c => c.ActiveAuthorName, "Jane Doe"));

        // Assert
        Assert.Contains("Clear filter", cut.Markup);
    }

    [Fact]
    public void AuthorList_WhenAuthorHasSlug_RendersFilterButton()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicAuthor>
            {
                new() { Name = "Jane Doe", Slug = "jane-doe", PostCount = 4 }
            });

        // Act
        var cut = Render<AuthorList>();

        // Assert — a filter button is rendered (sidebar uses filter, not profile links)
        var buttons = cut.FindAll("button.btn.btn-link");
        Assert.Contains(buttons, b => b.TextContent.Contains("Jane Doe"));

        // No profile-page anchor should be present in the sidebar
        var links = cut.FindAll("a[href]");
        Assert.DoesNotContain(links, a => a.GetAttribute("href")!.Contains("/author/"));
    }

    [Fact]
    public void AuthorList_WhenAuthorHasNoSlug_RendersFilterButton()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicAuthor>
            {
                new() { Name = "Anonymous Author", Slug = null, PostCount = 1 }
            });

        // Act
        var cut = Render<AuthorList>();

        // Assert — a btn-link button is rendered for filtering
        var buttons = cut.FindAll("button.btn.btn-link");
        Assert.Contains(buttons, b => b.TextContent.Contains("Anonymous Author"));

        // And no profile-page anchor should be present
        var links = cut.FindAll("a[href]");
        Assert.DoesNotContain(links, a => a.GetAttribute("href")!.Contains("/author/"));
    }

    // ── TopCommentedPosts / MostReadPosts — language-aware links ────────────────

    private void UseOptions(PostnomicLanguageRouteStyle style, string basePath = "/blog")
    {
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(
            Options.Create(new PostnomicClientOptions { BasePath = basePath, LanguageRouteStyle = style }));
    }

    [Fact]
    public void TopCommentedPosts_PrefixStyleWithLanguage_PostLinkUsesLangPrefix()
    {
        // Arrange
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        _blogServiceMock
            .Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>
            {
                new() { Slug = "hello-world", Title = "Hello World", Count = 5 }
            });

        // Act
        var cut = Render<TopCommentedPosts>(p =>
            p.Add(c => c.Language, "de"));

        // Assert — must be language-prefixed, not the bare unprefixed base-path link.
        Assert.NotEmpty(cut.FindAll("a[href='/de/blog/post/hello-world']"));
        Assert.Empty(cut.FindAll("a[href='/blog/post/hello-world']"));
    }

    [Fact]
    public void MostReadPosts_PrefixStyleWithLanguage_PostLinkUsesLangPrefix()
    {
        // Arrange
        UseOptions(PostnomicLanguageRouteStyle.Prefix);
        _blogServiceMock
            .Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>
            {
                new() { Slug = "hello-world", Title = "Hello World", Count = 42 }
            });

        // Act
        var cut = Render<MostReadPosts>(p =>
            p.Add(c => c.Language, "de"));

        // Assert — must be language-prefixed, not the bare unprefixed base-path link.
        Assert.NotEmpty(cut.FindAll("a[href='/de/blog/post/hello-world']"));
        Assert.Empty(cut.FindAll("a[href='/blog/post/hello-world']"));
    }
}

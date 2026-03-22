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
/// bUnit tests for the <see cref="BlogPage"/> Blazor component.
/// Verifies the loading state, blog header rendering, post card rendering, empty state,
/// and pagination visibility.
/// </summary>
public class BlogPageTests : BunitContext
{
    private readonly Mock<IPostnomicBlogService> _blogServiceMock;

    public BlogPageTests()
    {
        _blogServiceMock = new Mock<IPostnomicBlogService>();
        Services.AddSingleton(_blogServiceMock.Object);
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(
            Options.Create(new PostnomicClientOptions()));

        // NavigationManager is provided by bUnit automatically.
        // Register a no-op RecordPageViewAsync so the fire-and-forget call does not interfere.
        _blogServiceMock
            .Setup(s => s.RecordPageViewAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupBlogInfo(string name = "My Blog", string? description = "A blog about things")
    {
        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo
            {
                Name = name,
                Slug = "my-blog",
                Description = description
            });
    }

    private void SetupPosts(IEnumerable<PostnomicPostSummary>? items = null, int totalPages = 1)
    {
        var list = items?.ToList() ?? [];
        _blogServiceMock
            .Setup(s => s.GetPostsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items = list,
                Page = 1,
                PageSize = 5,
                TotalCount = list.Count,
                TotalPages = totalPages
            });
    }

    private static PostnomicPostSummary CreateSummary(string slug, string title, string author) =>
        new()
        {
            Slug = slug,
            Title = title,
            AuthorName = author,
            PublishedAt = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            CommentCount = 0
        };

    // ── Loading state ─────────────────────────────────────────────────────────

    [Fact]
    public void BlogPage_BeforeDataLoads_RendersLoadingText()
    {
        // Arrange — set up services to never complete
        var blogTcs = new TaskCompletionSource<PostnomicBlogInfo?>();
        var postsTcs = new TaskCompletionSource<PostnomicPagedResult<PostnomicPostSummary>>();

        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .Returns(blogTcs.Task);
        _blogServiceMock
            .Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(postsTcs.Task);

        // Act
        var cut = Render<BlogPage>();

        // Assert — while tasks are pending the loading placeholders appear
        cut.Markup.Should().Contain("Loading");
    }

    // ── Blog header ───────────────────────────────────────────────────────────

    [Fact]
    public void BlogPage_WhenBlogLoaded_DisplaysBlogName()
    {
        // Arrange
        SetupBlogInfo("Awesome Dev Blog");
        SetupPosts();

        // Act
        var cut = Render<BlogPage>();

        // Assert
        cut.Find("h1").TextContent.Should().Contain("Awesome Dev Blog");
    }

    [Fact]
    public void BlogPage_WhenBlogHasDescription_DisplaysDescription()
    {
        // Arrange
        SetupBlogInfo("My Blog", "All things .NET");
        SetupPosts();

        // Act
        var cut = Render<BlogPage>();

        // Assert
        cut.Markup.Should().Contain("All things .NET");
    }

    [Fact]
    public void BlogPage_WhenBlogDescriptionIsNull_DoesNotRenderDescriptionParagraph()
    {
        // Arrange
        SetupBlogInfo("My Blog", description: null);
        SetupPosts();

        // Act
        var cut = Render<BlogPage>();

        // Assert — lead paragraph should not appear when description is null
        var leadElements = cut.FindAll("p.lead");
        leadElements.Should().BeEmpty();
    }

    // ── Post cards ────────────────────────────────────────────────────────────

    [Fact]
    public void BlogPage_WhenPostsLoaded_RendersArticleCardPerPost()
    {
        // Arrange
        SetupBlogInfo();
        SetupPosts(new[]
        {
            CreateSummary("post-one", "Post One", "Jane"),
            CreateSummary("post-two", "Post Two", "John")
        });

        // Act
        var cut = Render<BlogPage>();

        // Assert
        var articles = cut.FindAll("article.card");
        articles.Should().HaveCount(2);
    }

    [Fact]
    public void BlogPage_WhenPostsLoaded_RendersPostTitles()
    {
        // Arrange
        SetupBlogInfo();
        SetupPosts(new[]
        {
            CreateSummary("hello-world", "Hello World", "Jane")
        });

        // Act
        var cut = Render<BlogPage>();

        // Assert
        cut.Markup.Should().Contain("Hello World");
    }

    [Fact]
    public void BlogPage_WhenPostsLoaded_RendersAuthorNames()
    {
        // Arrange
        SetupBlogInfo();
        SetupPosts(new[]
        {
            CreateSummary("post-a", "Title A", "Alice Smith")
        });

        // Act
        var cut = Render<BlogPage>();

        // Assert
        cut.Markup.Should().Contain("Alice Smith");
    }

    [Fact]
    public void BlogPage_WhenPostHasTags_RendersBadgesForEachTag()
    {
        // Arrange
        SetupBlogInfo();
        var summary = CreateSummary("tagged", "Tagged Post", "Jane");
        var summaryWithTags = summary with
        {
            Tags =
            [
                new PostnomicTag { Name = "C#", Slug = "csharp", PostCount = 1 },
                new PostnomicTag { Name = ".NET", Slug = "dotnet", PostCount = 2 }
            ]
        };
        SetupPosts(new[] { summaryWithTags });

        // Act
        var cut = Render<BlogPage>();

        // Assert
        cut.Markup.Should().Contain("C#");
        cut.Markup.Should().Contain(".NET");
    }

    [Fact]
    public void BlogPage_WhenPostHasExcerpt_RendersExcerpt()
    {
        // Arrange
        SetupBlogInfo();
        var summary = CreateSummary("excerpted", "Excerpted Post", "Jane") with
        {
            Excerpt = "A short teaser for the post."
        };
        SetupPosts(new[] { summary });

        // Act
        var cut = Render<BlogPage>();

        // Assert
        cut.Markup.Should().Contain("A short teaser for the post.");
    }

    [Fact]
    public void BlogPage_WhenPostHasReadMoreLink_LinkIncludesPostSlug()
    {
        // Arrange
        SetupBlogInfo();
        SetupPosts(new[] { CreateSummary("my-first-post", "First Post", "Jane") });

        // Act
        var cut = Render<BlogPage>();

        // Assert
        var links = cut.FindAll("a[href]");
        links.Should().Contain(a => a.GetAttribute("href")!.Contains("my-first-post"));
    }

    [Fact]
    public void BlogPage_PostLinks_UseDefaultBasePath()
    {
        // Arrange
        SetupBlogInfo();
        SetupPosts(new[] { CreateSummary("test-post", "Test", "Author") });

        // Act
        var cut = Render<BlogPage>();

        // Assert — links should use the default /blog base path
        var links = cut.FindAll("a[href]");
        links.Should().Contain(a => a.GetAttribute("href") == "/blog/post/test-post");
    }

    // ── Empty state ───────────────────────────────────────────────────────────

    [Fact]
    public void BlogPage_WhenNoPosts_RendersNoPostsFoundMessage()
    {
        // Arrange
        SetupBlogInfo();
        SetupPosts(Enumerable.Empty<PostnomicPostSummary>());

        // Act
        var cut = Render<BlogPage>();

        // Assert
        cut.Markup.Should().Contain("No posts found");
    }

    [Fact]
    public void BlogPage_WhenNoPosts_DoesNotRenderAnyArticleCards()
    {
        // Arrange
        SetupBlogInfo();
        SetupPosts(Enumerable.Empty<PostnomicPostSummary>());

        // Act
        var cut = Render<BlogPage>();

        // Assert
        cut.FindAll("article.card").Should().BeEmpty();
    }

    // ── Pagination ────────────────────────────────────────────────────────────

    [Fact]
    public void BlogPage_WhenOnlyOnePage_DoesNotRenderPagination()
    {
        // Arrange
        SetupBlogInfo();
        SetupPosts(new[] { CreateSummary("a", "A", "Author") }, totalPages: 1);

        // Act
        var cut = Render<BlogPage>();

        // Assert
        cut.FindAll("nav[aria-label='Blog pagination']").Should().BeEmpty();
    }

    [Fact]
    public void BlogPage_WhenMultiplePages_RendersPagination()
    {
        // Arrange
        SetupBlogInfo();
        SetupPosts(new[]
        {
            CreateSummary("a", "Post A", "Author"),
            CreateSummary("b", "Post B", "Author")
        }, totalPages: 3);

        // Act
        var cut = Render<BlogPage>();

        // Assert
        var nav = cut.FindAll("nav[aria-label='Blog pagination']");
        nav.Should().HaveCount(1);
    }

    [Fact]
    public void BlogPage_WhenMultiplePages_RendersPageNumberButtons()
    {
        // Arrange
        SetupBlogInfo();
        SetupPosts(new[] { CreateSummary("p", "P", "A") }, totalPages: 3);

        // Act
        var cut = Render<BlogPage>();

        // Assert — page buttons 1, 2, 3 should appear inside the pagination nav
        var nav = cut.Find("nav[aria-label='Blog pagination']");
        nav.TextContent.Should().Contain("1");
        nav.TextContent.Should().Contain("2");
        nav.TextContent.Should().Contain("3");
    }

    // ── Custom BasePath ──────────────────────────────────────────────────────

    [Fact]
    public void BlogPage_WithCustomBasePath_UsesConfiguredBasePath()
    {
        // Arrange — register custom BasePath
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(
            Options.Create(new PostnomicClientOptions { BasePath = "/articles" }));
        SetupBlogInfo();
        SetupPosts(new[] { CreateSummary("my-post", "My Post", "Author") });

        // Act
        var cut = Render<BlogPage>();

        // Assert — links should use the custom base path
        var links = cut.FindAll("a[href]");
        links.Should().Contain(a => a.GetAttribute("href") == "/articles/post/my-post");
    }
}

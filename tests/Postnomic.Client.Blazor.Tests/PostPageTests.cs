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
/// bUnit tests for the <see cref="PostPage"/> Blazor component.
/// Verifies the loading state, post detail rendering, and comments section behaviour.
/// </summary>
public class PostPageTests : BunitContext
{
    private readonly Mock<IPostnomicBlogService> _blogServiceMock;

    public PostPageTests()
    {
        _blogServiceMock = new Mock<IPostnomicBlogService>();
        Services.AddSingleton(_blogServiceMock.Object);
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(
            Options.Create(new PostnomicClientOptions()));

        // Stub analytics calls so fire-and-forget does not interfere with assertions.
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupPost(PostnomicPostDetail? post)
    {
        _blogServiceMock
            .Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
    }

    private static PostnomicPostDetail CreateDetail(
        string slug = "test-post",
        string title = "Test Post",
        string author = "Jane Doe",
        string? content = "<p>Hello world</p>",
        bool commentsEnabled = true,
        ICollection<PostnomicComment>? comments = null) =>
        new()
        {
            Slug = slug,
            Title = title,
            AuthorName = author,
            PublishedAt = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            Content = content,
            CommentsEnabled = commentsEnabled,
            Comments = comments ?? []
        };

    private static PostnomicComment CreateComment(
        string publicId = "cmt-1",
        string body = "Nice post!",
        string? authorName = "Reader") =>
        new()
        {
            PublicId = publicId,
            Body = body,
            AuthorName = authorName,
            CreatedAt = DateTime.UtcNow
        };

    // ── Loading state ─────────────────────────────────────────────────────────

    [Fact]
    public void PostPage_BeforeDataLoads_RendersLoadingIndicator()
    {
        // Arrange
        var tcs = new TaskCompletionSource<PostnomicPostDetail?>();
        _blogServiceMock
            .Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "test-post"));

        // Assert
        cut.Markup.Should().Contain("Loading");
    }

    // ── Post detail rendering ─────────────────────────────────────────────────

    [Fact]
    public void PostPage_WhenPostLoaded_RendersPostTitle()
    {
        // Arrange
        SetupPost(CreateDetail(title: "Deep Dive into Blazor"));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "deep-dive-blazor"));

        // Assert
        cut.Find("h1").TextContent.Should().Contain("Deep Dive into Blazor");
    }

    [Fact]
    public void PostPage_WhenPostLoaded_RendersAuthorName()
    {
        // Arrange
        SetupPost(CreateDetail(author: "Alice Wonderland"));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "post"));

        // Assert
        cut.Markup.Should().Contain("Alice Wonderland");
    }

    [Fact]
    public void PostPage_WhenPostLoaded_RendersHtmlContent()
    {
        // Arrange
        SetupPost(CreateDetail(content: "<p>This is the <strong>content</strong>.</p>"));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "content-post"));

        // Assert
        cut.Markup.Should().Contain("content");
    }

    [Fact]
    public void PostPage_WhenPostHasTags_RendersBadgesForEachTag()
    {
        // Arrange
        var detail = CreateDetail() with
        {
            Tags =
            [
                new PostnomicTag { Name = "Blazor", Slug = "blazor", PostCount = 1 },
                new PostnomicTag { Name = ".NET", Slug = "dotnet", PostCount = 3 }
            ]
        };
        SetupPost(detail);

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "tagged"));

        // Assert
        cut.Markup.Should().Contain("Blazor");
        cut.Markup.Should().Contain(".NET");
    }

    [Fact]
    public void PostPage_WhenPostHasCoverImage_RendersCoverImage()
    {
        // Arrange
        var detail = CreateDetail() with { CoverImageUrl = "https://example.com/cover.jpg" };
        SetupPost(detail);

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "with-cover"));

        // Assert
        var img = cut.Find("img[src='https://example.com/cover.jpg']");
        img.Should().NotBeNull();
    }

    [Fact]
    public void PostPage_WhenPostHasNoCoverImage_DoesNotRenderCoverImg()
    {
        // Arrange
        var detail = CreateDetail() with { CoverImageUrl = null };
        SetupPost(detail);

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "no-cover"));

        // Assert — no img element with img-fluid class pointing to a cover URL
        var imgs = cut.FindAll("img.img-fluid");
        imgs.Should().BeEmpty();
    }

    // ── Comments section ──────────────────────────────────────────────────────

    [Fact]
    public void PostPage_WhenPostHasComments_RendersCommentCount()
    {
        // Arrange
        var comments = new List<PostnomicComment>
        {
            CreateComment("c1", "First comment"),
            CreateComment("c2", "Second comment")
        };
        SetupPost(CreateDetail(comments: comments));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "commented"));

        // Assert — the section heading "Comments (2)" should be present
        var heading = cut.Find("h3");
        heading.TextContent.Should().Contain("2");
    }

    [Fact]
    public void PostPage_WhenNoComments_RendersNoCommentsMessage()
    {
        // Arrange
        SetupPost(CreateDetail(commentsEnabled: true, comments: []));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "no-comments"));

        // Assert
        cut.Markup.Should().Contain("No comments yet");
    }

    [Fact]
    public void PostPage_WhenCommentsEnabled_RendersCommentForm()
    {
        // Arrange
        SetupPost(CreateDetail(commentsEnabled: true));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "open-post"));

        // Assert
        cut.FindAll("form").Should().NotBeEmpty();
    }

    [Fact]
    public void PostPage_WhenCommentsDisabled_DoesNotRenderCommentForm()
    {
        // Arrange
        SetupPost(CreateDetail(commentsEnabled: false));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "closed-post"));

        // Assert
        cut.FindAll("form").Should().BeEmpty();
        cut.Markup.Should().Contain("Comments are closed");
    }

    // ── Back link ─────────────────────────────────────────────────────────────

    [Fact]
    public void PostPage_WhenPostLoaded_RendersBackToBlogLink()
    {
        // Arrange
        SetupPost(CreateDetail());

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "any-post"));

        // Assert
        var backLink = cut.FindAll("a[href='/blog']");
        backLink.Should().NotBeEmpty();
    }

    [Fact]
    public void PostPage_WithCustomBasePath_RendersBackLinkToCustomPath()
    {
        // Arrange — register custom BasePath
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(
            Options.Create(new PostnomicClientOptions { BasePath = "/articles" }));
        SetupPost(CreateDetail());

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "any-post"));

        // Assert
        var backLink = cut.FindAll("a[href='/articles']");
        backLink.Should().NotBeEmpty();
    }
}

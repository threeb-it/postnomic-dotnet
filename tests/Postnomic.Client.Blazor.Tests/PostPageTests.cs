using Bunit;
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
            .Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "test-post"));

        // Assert
        Assert.Contains("Loading", cut.Markup);
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
        Assert.Contains("Deep Dive into Blazor", cut.Find("h1").TextContent);
    }

    [Fact]
    public void PostPage_WhenPostLoaded_RendersAuthorName()
    {
        // Arrange
        SetupPost(CreateDetail(author: "Alice Wonderland"));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "post"));

        // Assert
        Assert.Contains("Alice Wonderland", cut.Markup);
    }

    [Fact]
    public void PostPage_WhenPostLoaded_RendersHtmlContent()
    {
        // Arrange
        SetupPost(CreateDetail(content: "<p>This is the <strong>content</strong>.</p>"));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "content-post"));

        // Assert
        Assert.Contains("content", cut.Markup);
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
        Assert.Contains("Blazor", cut.Markup);
        Assert.Contains(".NET", cut.Markup);
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
        Assert.NotNull(img);
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
        Assert.Empty(imgs);
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
        Assert.Contains("2", heading.TextContent);
    }

    [Fact]
    public void PostPage_WhenNoComments_RendersNoCommentsMessage()
    {
        // Arrange
        SetupPost(CreateDetail(commentsEnabled: true, comments: []));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "no-comments"));

        // Assert
        Assert.Contains("No comments yet", cut.Markup);
    }

    [Fact]
    public void PostPage_WhenCommentsEnabled_RendersCommentForm()
    {
        // Arrange
        SetupPost(CreateDetail(commentsEnabled: true));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "open-post"));

        // Assert
        Assert.NotEmpty(cut.FindAll("form"));
    }

    [Fact]
    public void PostPage_WhenCommentsDisabled_DoesNotRenderCommentForm()
    {
        // Arrange
        SetupPost(CreateDetail(commentsEnabled: false));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "closed-post"));

        // Assert
        Assert.Empty(cut.FindAll("form"));
        Assert.Contains("Comments are closed", cut.Markup);
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
        Assert.NotEmpty(backLink);
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
        Assert.NotEmpty(backLink);
    }

    // ── Language parameter ───────────────────────────────────────────────────

    [Fact]
    public void PostPage_WithLanguage_ForwardsLanguageToGetPostAsync()
    {
        // Arrange
        SetupPost(CreateDetail());

        // Act
        Render<PostPage>(p => p
            .Add(x => x.PostSlug, "any-post")
            .Add(x => x.Language, "de"));

        // Assert
        _blogServiceMock.Verify(
            s => s.GetPostAsync("any-post", "de", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}

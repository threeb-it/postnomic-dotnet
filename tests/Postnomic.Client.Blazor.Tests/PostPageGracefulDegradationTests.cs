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
/// bUnit tests for graceful degradation in <see cref="PostPage"/>.
/// <para>
/// The component used to load the post and the blog info under one bare <see cref="Task.WhenAll"/>
/// inside a single blanket <c>catch</c>, so a failing <c>GetBlogAsync</c> nulled the post as well
/// and left the visitor on a permanent "Loading…" for a post that had loaded perfectly well —
/// with nothing logged anywhere. The post's own failure behaviour is unchanged.
/// </para>
/// </summary>
public class PostPageGracefulDegradationTests : BunitContext
{
    private readonly Mock<IPostnomicBlogService> _blogServiceMock;

    public PostPageGracefulDegradationTests()
    {
        _blogServiceMock = new Mock<IPostnomicBlogService>();
        Services.AddSingleton(_blogServiceMock.Object);
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(
            Options.Create(new PostnomicClientOptions { ShowBranding = true }));

        // Stub analytics so the fire-and-forget calls cannot interfere with assertions.
        _blogServiceMock
            .Setup(s => s.RecordPageViewAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blogServiceMock
            .Setup(s => s.UpdateReadDurationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SetupPost(CreateDetail());
        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Test Blog", Slug = "test-blog" });
    }

    // ── Decorative calls degrade ──────────────────────────────────────────────

    [Fact]
    public void PostPage_WhenBlogInfoFails_StillRendersThePost()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("blog endpoint is down"));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "test-post"));

        // Assert — the post is what the visitor came for, and it loaded.
        // (The page's own sidebar widgets are separate components with their own loading state,
        // so "Loading" elsewhere in the markup says nothing about the post.)
        Assert.Equal("Test Post", cut.Find("h1").TextContent);
        Assert.Contains("Hello world", cut.Markup);
    }

    [Fact]
    public void PostPage_WhenBlogInfoThrowsSynchronously_StillRendersThePost()
    {
        // Arrange — a synchronous throw, not a faulted task.
        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("blew up before returning a task"));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "test-post"));

        // Assert
        Assert.Equal("Test Post", cut.Find("h1").TextContent);
    }

    [Fact]
    public void PostPage_WhenBlogInfoAndAlternateUrlProviderBothFail_StillRendersThePost()
    {
        // Arrange — several decorative calls failing at once.
        Services.AddPostnomicAlternateUrlProvider<ThrowingAlternateUrlProvider>();
        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("blog endpoint is down"));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "test-post"));

        // Assert
        Assert.Equal("Test Post", cut.Find("h1").TextContent);
        Assert.Contains("Hello world", cut.Markup);
    }

    [Fact]
    public void PostPage_WhenAlternateUrlProviderFails_StillRendersThePost()
    {
        // Arrange — the provider is host-supplied code; it must not be able to blank the page.
        Services.AddPostnomicAlternateUrlProvider<ThrowingAlternateUrlProvider>();

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "test-post"));

        // Assert
        Assert.Equal("Test Post", cut.Find("h1").TextContent);
    }

    [Fact]
    public void PostPage_WhenBlogInfoTimesOut_DegradesRatherThanBeingTreatedAsCancellation()
    {
        // Arrange — an HttpClient timeout surfaces as TaskCanceledException while the component
        // is very much alive; that is a failure, not a teardown.
        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("The request timed out.", new TimeoutException()));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "test-post"));

        // Assert
        Assert.Equal("Test Post", cut.Find("h1").TextContent);
    }

    [Fact]
    public void PostPage_WhenBlogInfoFails_BrandingFallsBackToClientOptions()
    {
        // Arrange — ShowBranding reads blogInfo first and falls back to options; the fallback is
        // exactly why blog info is decorative on this page.
        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "test-post"));

        // Assert — rendered, and the configured branding still applied.
        Assert.Equal("Test Post", cut.Find("h1").TextContent);
        Assert.Contains("Postnomic", cut.Markup);
    }

    // ── The post itself keeps its existing behaviour ──────────────────────────

    [Fact]
    public void PostPage_WhenThePostCallFails_RendersTheEmptyStateWithoutThrowing()
    {
        // Arrange — pre-existing contract: a failed post load renders the empty state rather than
        // tearing down the circuit. Deliberately preserved; only the logging is new.
        _blogServiceMock
            .Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("post endpoint is down"));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "test-post"));

        // Assert
        Assert.Contains("Loading", cut.Markup);
        Assert.Empty(cut.FindAll("h1"));
    }

    [Fact]
    public void PostPage_WhenThePostIsMissing_RendersTheEmptyState()
    {
        // Arrange — a null post (the API's not-found) behaves exactly as it did before.
        SetupPost(null);

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "no-such-post"));

        // Assert
        Assert.Contains("Loading", cut.Markup);
        Assert.Empty(cut.FindAll("h1"));
    }

    [Fact]
    public void PostPage_WhenThePostFailsAndBlogInfoSucceeds_StillRendersTheEmptyState()
    {
        // Arrange — the split must not accidentally render a post-less page as if it had content.
        _blogServiceMock
            .Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        // Act
        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "test-post"));

        // Assert
        Assert.Contains("Loading", cut.Markup);
    }

    // ── Teardown ──────────────────────────────────────────────────────────────

    [Fact]
    public void PostPage_WhenDisposedWhileALoadIsPending_DoesNotThrow()
    {
        // Arrange — the visitor navigates away mid-load. The pending call then completes as
        // cancelled, which must be swallowed quietly rather than surfacing as a widget failure.
        var gate = new TaskCompletionSource<PostnomicBlogInfo?>();
        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .Returns(gate.Task);

        var cut = Render<PostPage>(p => p.Add(x => x.PostSlug, "test-post"));
        Assert.Contains("Loading", cut.Markup);

        // Act & Assert — disposing and then failing the in-flight call must not throw.
        cut.Instance.Dispose();
        gate.SetCanceled();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupPost(PostnomicPostDetail? post) =>
        _blogServiceMock
            .Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

    private static PostnomicPostDetail CreateDetail() => new()
    {
        Slug = "test-post",
        Title = "Test Post",
        AuthorName = "Jane Doe",
        PublishedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Content = "<p>Hello world</p>",
        CommentsEnabled = true,
        Comments = []
    };
}

/// <summary>
/// A host-supplied hreflang provider that fails, standing in for third-party code the SDK has no
/// control over.
/// </summary>
internal sealed class ThrowingAlternateUrlProvider : IPostnomicAlternateUrlProvider
{
    public ValueTask<IReadOnlyList<(string Language, string Url)>?> GetAlternatesAsync(
        PostnomicPostDetail post, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("the host's alternate URL provider blew up");
}

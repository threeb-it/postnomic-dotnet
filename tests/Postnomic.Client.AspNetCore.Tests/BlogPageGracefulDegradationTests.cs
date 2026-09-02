using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.AspNetCore.Areas.Blog.Pages;
using Postnomic.Client.AspNetCore.Tests.TestSupport;

namespace Postnomic.Client.AspNetCore.Tests;

/// <summary>
/// Tests that one failing decorative sidebar widget degrades to an empty widget instead of
/// taking the whole blog page down, while genuinely essential data keeps failing loudly.
/// <para>
/// Regression cover for a production incident: an API outage turned every widget call on the
/// blog page into its own error-tracker issue and a hard 500 for visitors, even though the
/// posts themselves were perfectly renderable.
/// </para>
/// </summary>
public class BlogPageGracefulDegradationTests
{
    // ── Index page: decorative widgets ────────────────────────────────────────

    [Fact]
    public async Task Index_WhenTagsWidgetFails_StillRendersPageWithPosts()
    {
        // Arrange
        var mock = CreateHealthyIndexService();
        mock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("tags endpoint is down"));
        var sut = CreateIndexModel(mock);

        // Act
        var result = await sut.OnGetAsync();

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.Single(sut.Posts.Items);
        Assert.Empty(sut.Tags);
    }

    [Fact]
    public async Task Index_WhenWidgetThrowsSynchronously_StillRendersPage()
    {
        // Arrange — a synchronous throw (not a faulted task) must degrade identically.
        var mock = CreateHealthyIndexService();
        mock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("blew up before returning a task"));
        var sut = CreateIndexModel(mock);

        // Act
        var result = await sut.OnGetAsync();

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.Empty(sut.Categories);
        Assert.Single(sut.Posts.Items);
    }

    [Fact]
    public async Task Index_WhenEveryDecorativeWidgetFails_StillRendersPageWithPosts()
    {
        // Arrange
        var mock = CreateHealthyIndexService();
        mock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        mock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        mock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        var sut = CreateIndexModel(mock);

        // Act
        var result = await sut.OnGetAsync();

        // Assert — the page and its actual content survive a total sidebar outage.
        Assert.IsType<PageResult>(result);
        Assert.Single(sut.Posts.Items);
        Assert.NotNull(sut.BlogInfo);
        Assert.Empty(sut.Tags);
        Assert.Empty(sut.Categories);
        Assert.Empty(sut.Authors);
        Assert.Empty(sut.TopCommented);
        Assert.Empty(sut.MostRead);
    }

    [Fact]
    public async Task Index_WhenOneWidgetFails_TheOtherWidgetsAreStillRequestedAndPopulated()
    {
        // Arrange — proves the calls still fan out rather than short-circuiting at the first failure.
        var mock = CreateHealthyIndexService();
        mock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        mock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicAuthor { Name = "Jane Doe", PostCount = 2 }]);
        var sut = CreateIndexModel(mock);

        // Act
        await sut.OnGetAsync();

        // Assert
        Assert.Empty(sut.Tags);
        Assert.Single(sut.Authors);
        mock.Verify(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()), Times.Once);
        mock.Verify(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        mock.Verify(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Index_WhenWidgetFails_LogsAWarning()
    {
        // Arrange
        var mock = CreateHealthyIndexService();
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        var logger = new Mock<ILogger<IndexModel>>();
        var sut = CreateIndexModel(mock, logger: logger.Object);

        // Act
        await sut.OnGetAsync();

        // Assert — degraded, but never silently.
        VerifyWarningLogged(logger, Times.Once());
    }

    [Fact]
    public async Task Index_WhenNothingFails_LogsNoWarning()
    {
        // Arrange
        var logger = new Mock<ILogger<IndexModel>>();
        var sut = CreateIndexModel(CreateHealthyIndexService(), logger: logger.Object);

        // Act
        await sut.OnGetAsync();

        // Assert
        VerifyWarningLogged(logger, Times.Never());
    }

    // ── Index page: essential data stays fatal ────────────────────────────────

    [Fact]
    public async Task Index_WhenPostListFails_StillThrows()
    {
        // Arrange
        var mock = CreateHealthyIndexService();
        mock.Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("posts endpoint is down"));
        var sut = CreateIndexModel(mock);

        // Act & Assert — there is no meaningful blog index without the posts.
        await Assert.ThrowsAsync<HttpRequestException>(() => sut.OnGetAsync());
    }

    [Fact]
    public async Task Index_WhenBlogMetadataFails_StillThrows()
    {
        // Arrange
        var mock = CreateHealthyIndexService();
        mock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("blog endpoint is down"));
        var sut = CreateIndexModel(mock);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => sut.OnGetAsync());
    }

    // ── Index page: cancellation ──────────────────────────────────────────────

    [Fact]
    public async Task Index_WhenRequestIsAborted_PropagatesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var mock = CreateHealthyIndexService();
        mock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));
        var logger = new Mock<ILogger<IndexModel>>();
        var sut = CreateIndexModel(mock, logger: logger.Object);

        // Act & Assert — an aborted request is not a widget failure.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.OnGetAsync(cts.Token));
        VerifyWarningLogged(logger, Times.Never());
    }

    [Fact]
    public async Task Index_WhenWidgetTimesOutWithoutRequestAbort_DegradesLikeAnyOtherFailure()
    {
        // Arrange — HttpClient timeouts surface as TaskCanceledException even though the request
        // itself is very much alive; that is a widget failure, not a visitor navigating away.
        var mock = CreateHealthyIndexService();
        mock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("The request timed out.", new TimeoutException()));
        var sut = CreateIndexModel(mock);

        // Act
        var result = await sut.OnGetAsync(CancellationToken.None);

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.Empty(sut.Tags);
        Assert.Single(sut.Posts.Items);
    }

    // ── Post page: decorative widgets ─────────────────────────────────────────

    [Fact]
    public async Task Post_WhenSidebarWidgetsFail_StillRendersThePost()
    {
        // Arrange
        var mock = CreateHealthyPostService();
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        var sut = CreatePostModel(mock);

        // Act
        var result = await sut.OnGetAsync();

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.Equal("test-post", sut.Post.Slug);
        Assert.Empty(sut.TopCommented);
        Assert.Empty(sut.MostRead);
    }

    [Fact]
    public async Task Post_WhenBlogMetadataFails_StillRendersThePostAndFallsBackToClientBranding()
    {
        // Arrange — BlogInfo only supplies the branding flag, which has a client-options fallback.
        var mock = CreateHealthyPostService();
        mock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("blog endpoint is down"));
        var sut = CreatePostModel(mock, new PostnomicClientOptions { BasePath = "/blog", ShowBranding = true });

        // Act
        var result = await sut.OnGetAsync();

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.Null(sut.BlogInfo);
        Assert.True(sut.ShowBranding);
    }

    [Fact]
    public async Task Post_WhenEveryDecorativeCallFails_StillRendersThePost()
    {
        // Arrange
        var mock = CreateHealthyPostService();
        mock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        var logger = new Mock<ILogger<PostModel>>();
        var sut = CreatePostModel(mock, logger: logger.Object);

        // Act
        var result = await sut.OnGetAsync();

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.Equal("test-post", sut.Post.Slug);
        VerifyWarningLogged(logger, Times.Exactly(3));
    }

    // ── Post page: essential data stays fatal / 404 contract preserved ────────

    [Fact]
    public async Task Post_WhenPostIsMissing_StillReturnsNotFoundEvenWithFailingWidgets()
    {
        // Arrange — the pre-existing contract: a null post is a 404, not an empty page.
        var mock = CreateHealthyPostService(missingPost: true);
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        var sut = CreatePostModel(mock);

        // Act
        var result = await sut.OnGetAsync();

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Post_WhenThePostCallFails_StillThrows()
    {
        // Arrange
        var mock = CreateHealthyPostService();
        mock.Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("post endpoint is down"));
        var sut = CreatePostModel(mock);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => sut.OnGetAsync());
    }

    [Fact]
    public async Task Post_WhenRequestIsAborted_PropagatesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var mock = CreateHealthyPostService();
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));
        var logger = new Mock<ILogger<PostModel>>();
        var sut = CreatePostModel(mock, logger: logger.Object);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.OnGetAsync(cts.Token));
        VerifyWarningLogged(logger, Times.Never());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void VerifyWarningLogged<T>(Mock<ILogger<T>> logger, Times times) =>
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            times);

    private static Mock<IPostnomicBlogService> CreateHealthyIndexService()
    {
        var mock = new Mock<IPostnomicBlogService>();

        mock.Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items =
                [
                    new PostnomicPostSummary
                    {
                        Slug = "still-here",
                        Title = "Still Here",
                        AuthorName = "Jane Doe",
                        PublishedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    }
                ],
                Page = 1,
                PageSize = 5,
                TotalCount = 1,
                TotalPages = 1
            });

        mock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Test Blog", Slug = "test-blog" });
        mock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        mock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        mock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return mock;
    }

    private static Mock<IPostnomicBlogService> CreateHealthyPostService(bool missingPost = false)
    {
        var mock = new Mock<IPostnomicBlogService>();

        mock.Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(missingPost
                ? null
                : new PostnomicPostDetail
                {
                    Slug = "test-post",
                    Title = "Test Post",
                    AuthorName = "Jane Doe",
                    PublishedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Content = "<p>Content here.</p>"
                });

        mock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Test Blog", Slug = "test-blog" });
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return mock;
    }

    private static IndexModel CreateIndexModel(
        Mock<IPostnomicBlogService> mock,
        PostnomicClientOptions? clientOptions = null,
        ILogger<IndexModel>? logger = null)
    {
        var model = new IndexModel(
            mock.Object,
            Mock.Of<IServiceProvider>(),
            CreateResolver(),
            Options.Create(clientOptions ?? new PostnomicClientOptions { BasePath = "/blog" }),
            Mock.Of<IOptionsMonitor<PostnomicClientOptions>>(),
            logger);

        return WirePageContext(model);
    }

    private static PostModel CreatePostModel(
        Mock<IPostnomicBlogService> mock,
        PostnomicClientOptions? clientOptions = null,
        ILogger<PostModel>? logger = null)
    {
        var model = new PostModel(
            mock.Object,
            Mock.Of<IServiceProvider>(),
            CreateResolver(),
            Options.Create(clientOptions ?? new PostnomicClientOptions { BasePath = "/blog" }),
            Mock.Of<IOptionsMonitor<PostnomicClientOptions>>(),
            TestStringLocalizers.Post(),
            logger)
        {
            PostSlug = "test-post"
        };

        return WirePageContext(model);
    }

    private static IPostnomicBlogResolver CreateResolver()
    {
        var resolver = new Mock<IPostnomicBlogResolver>();
        resolver.Setup(r => r.ResolveBlogName(It.IsAny<string>())).Returns((string?)null);
        return resolver.Object;
    }

    private static TModel WirePageContext<TModel>(TModel model) where TModel : PageModel
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new PageActionDescriptor(),
            new ModelStateDictionary());

        model.PageContext = new PageContext(actionContext);
        model.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        model.Url = Mock.Of<IUrlHelper>();

        return model;
    }
}

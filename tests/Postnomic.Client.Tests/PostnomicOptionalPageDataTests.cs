using Microsoft.Extensions.Logging;
using Moq;

namespace Postnomic.Client.Tests;

/// <summary>
/// Unit tests for <see cref="PostnomicOptionalPageData"/>, the helper both hosting models use to
/// load decorative page data. The cancellation rule lives here, so this is where it is pinned
/// down deterministically: a cancelled request token propagates, while any other failure —
/// including an <see cref="System.Net.Http.HttpClient"/> timeout, which also surfaces as an
/// <see cref="OperationCanceledException"/> — degrades to the fallback.
/// </summary>
public class PostnomicOptionalPageDataTests
{
    // ── Success ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_WhenTheCallSucceeds_ReturnsItsValue()
    {
        // Act
        var result = await PostnomicOptionalPageData.LoadAsync(
            () => Task.FromResult(new List<string> { "tag" }),
            fallback: [],
            "tags",
            logger: null,
            CancellationToken.None);

        // Assert
        Assert.Equal(["tag"], result);
    }

    [Fact]
    public async Task LoadAsync_WhenTheCallSucceeds_LogsNothing()
    {
        // Arrange
        var logger = new Mock<ILogger>();

        // Act
        await PostnomicOptionalPageData.LoadAsync(
            () => Task.FromResult("ok"), "fallback", "widget", logger.Object, CancellationToken.None);

        // Assert
        VerifyWarningLogged(logger, Times.Never());
    }

    // ── Failure degrades ──────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_WhenTheCallFaults_ReturnsTheFallback()
    {
        // Act
        var result = await PostnomicOptionalPageData.LoadAsync<List<string>>(
            () => Task.FromException<List<string>>(new HttpRequestException("down")),
            fallback: [],
            "tags",
            logger: null,
            CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadAsync_WhenTheCallThrowsSynchronously_ReturnsTheFallback()
    {
        // Arrange — an async method never throws synchronously, but a mock or a decorator can.
        // Act
        var result = await PostnomicOptionalPageData.LoadAsync<string>(
            () => throw new InvalidOperationException("blew up before returning a task"),
            "fallback",
            "widget",
            logger: null,
            CancellationToken.None);

        // Assert
        Assert.Equal("fallback", result);
    }

    [Fact]
    public async Task LoadAsync_WhenTheCallFails_LogsAWarningWithTheException()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var failure = new HttpRequestException("down");

        // Act
        await PostnomicOptionalPageData.LoadAsync(
            () => Task.FromException<string>(failure), "fallback", "widget", logger.Object, CancellationToken.None);

        // Assert — degraded, but never silently.
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                failure,
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_WhenTheRequestTokenIsCancelled_RethrowsInsteadOfDegrading()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var logger = new Mock<ILogger>();

        // Act & Assert — the visitor went away; that is not a widget failure.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PostnomicOptionalPageData.LoadAsync(
                () => Task.FromException<string>(new OperationCanceledException(cts.Token)),
                "fallback",
                "widget",
                logger.Object,
                cts.Token));

        VerifyWarningLogged(logger, Times.Never());
    }

    [Fact]
    public async Task LoadAsync_WhenCancellationComesFromATimeoutNotTheRequest_DegradesAndLogs()
    {
        // Arrange — HttpClient signals its own timeout as TaskCanceledException even though the
        // request token was never cancelled. That is a genuine widget failure.
        var logger = new Mock<ILogger>();

        // Act
        var result = await PostnomicOptionalPageData.LoadAsync(
            () => Task.FromException<string>(
                new TaskCanceledException("The request timed out.", new TimeoutException())),
            "fallback",
            "widget",
            logger.Object,
            CancellationToken.None);

        // Assert
        Assert.Equal("fallback", result);
        VerifyWarningLogged(logger, Times.Once());
    }

    [Fact]
    public async Task LoadAsync_WhenAnUnrelatedTokenIsCancelled_StillDegrades()
    {
        // Arrange — only *this* request's token means "the visitor went away".
        using var unrelated = new CancellationTokenSource();
        await unrelated.CancelAsync();

        // Act
        var result = await PostnomicOptionalPageData.LoadAsync(
            () => Task.FromException<string>(new OperationCanceledException(unrelated.Token)),
            "fallback",
            "widget",
            logger: null,
            CancellationToken.None);

        // Assert
        Assert.Equal("fallback", result);
    }

    // ── Parallelism ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_StartsTheWorkBeforeItIsAwaited()
    {
        // Arrange — callers rely on this to fan out: they start every widget, then await them all.
        var started = false;
        var gate = new TaskCompletionSource<string>();

        // Act
        var task = PostnomicOptionalPageData.LoadAsync(
            () => { started = true; return gate.Task; },
            "fallback",
            "widget",
            logger: null,
            CancellationToken.None);

        // Assert — the call is already in flight while the returned task is still pending.
        Assert.True(started);
        Assert.False(task.IsCompleted);

        gate.SetResult("done");
        Assert.Equal("done", await task);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void VerifyWarningLogged(Mock<ILogger> logger, Times times) =>
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            times);
}

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.Tests;

/// <summary>
/// A <see langword="null"/> ("not found") result is cached for at most
/// <see cref="CachingPostnomicBlogService.NotFoundMaxDuration"/>, however long the configured
/// duration is; found results keep the configured duration (threeb-it/postnomic-dotnet#16, item 3).
/// </summary>
public class CachingNotFoundTtlTests : IDisposable
{
    private readonly TestClock _clock = new();
    private readonly MemoryCache _cache;
    private readonly Mock<IPostnomicBlogService> _inner = new();

    public CachingNotFoundTtlTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions { Clock = _clock });
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task NotFoundPost_ExpiresAfterOneMinute_EvenWhenPostDetailDurationIsLonger()
    {
        var sut = CreateSut(postDetail: TimeSpan.FromMinutes(5));
        _inner.Setup(s => s.GetPostAsync("missing", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostnomicPostDetail?)null);

        await sut.GetPostAsync("missing", cancellationToken: TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromSeconds(30));
        await sut.GetPostAsync("missing", cancellationToken: TestContext.Current.CancellationToken);
        _inner.Verify(s => s.GetPostAsync("missing", null, It.IsAny<CancellationToken>()), Times.Once);

        _clock.Advance(TimeSpan.FromSeconds(31));
        await sut.GetPostAsync("missing", cancellationToken: TestContext.Current.CancellationToken);
        _inner.Verify(s => s.GetPostAsync("missing", null, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task FoundPost_KeepsTheConfiguredDuration()
    {
        var sut = CreateSut(postDetail: TimeSpan.FromMinutes(5));
        _inner.Setup(s => s.GetPostAsync("hello", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPostDetail { Slug = "hello", Title = "Hello", AuthorName = "Jane" });

        await sut.GetPostAsync("hello", cancellationToken: TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromMinutes(4));
        await sut.GetPostAsync("hello", cancellationToken: TestContext.Current.CancellationToken);

        _inner.Verify(s => s.GetPostAsync("hello", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotFoundAuthorAndBlog_ExpireAfterOneMinute()
    {
        var sut = CreateSut(metadata: TimeSpan.FromMinutes(5));
        _inner.Setup(s => s.GetAuthorProfileAsync("ghost", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostnomicAuthorProfile?)null);
        _inner.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostnomicBlogInfo?)null);

        await sut.GetAuthorProfileAsync("ghost", TestContext.Current.CancellationToken);
        await sut.GetBlogAsync(TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromSeconds(61));
        await sut.GetAuthorProfileAsync("ghost", TestContext.Current.CancellationToken);
        await sut.GetBlogAsync(TestContext.Current.CancellationToken);

        _inner.Verify(s => s.GetAuthorProfileAsync("ghost", It.IsAny<CancellationToken>()), Times.Exactly(2));
        _inner.Verify(s => s.GetBlogAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task NotFoundPost_UsesTheConfiguredDuration_WhenItIsShorterThanOneMinute()
    {
        var sut = CreateSut(postDetail: TimeSpan.FromSeconds(20));
        _inner.Setup(s => s.GetPostAsync("missing", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostnomicPostDetail?)null);

        await sut.GetPostAsync("missing", cancellationToken: TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromSeconds(21));
        await sut.GetPostAsync("missing", cancellationToken: TestContext.Current.CancellationToken);

        _inner.Verify(s => s.GetPostAsync("missing", null, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private CachingPostnomicBlogService CreateSut(TimeSpan? postDetail = null, TimeSpan? metadata = null) =>
        new(_inner.Object, _cache, Options.Create(new PostnomicClientOptions
        {
            BaseUrl = "https://api.test.com",
            ApiKey = "test-key",
            BlogSlug = "test-blog",
            Cache = new PostnomicCacheOptions
            {
                Enabled = true,
                PostDetailDuration = postDetail ?? TimeSpan.FromMinutes(5),
                MetadataDuration = metadata ?? TimeSpan.FromMinutes(5)
            }
        }));

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => UtcNow += by;
    }
}

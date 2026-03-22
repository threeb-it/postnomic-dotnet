using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.Tests;

/// <summary>
/// Unit tests for <see cref="CachingPostnomicBlogService"/>.
/// Uses a real <see cref="MemoryCache"/> and a <see cref="Mock{T}"/> of
/// <see cref="IPostnomicBlogService"/> to verify that read operations are served from the
/// cache on repeated calls, write operations always flow through to the inner service, and
/// the <see cref="IPostnomicCacheControl"/> invalidation methods clear the correct entries.
/// </summary>
public class CachingPostnomicBlogServiceTests : IDisposable
{
    private const string BlogSlug = "test-blog";

    private readonly Mock<IPostnomicBlogService> _innerMock;
    private readonly MemoryCache _cache;
    private readonly CachingPostnomicBlogService _sut;

    public CachingPostnomicBlogServiceTests()
    {
        _innerMock = new Mock<IPostnomicBlogService>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        var options = Options.Create(new PostnomicClientOptions
        {
            BaseUrl = "https://api.test.com",
            ApiKey = "test-key",
            BlogSlug = BlogSlug,
            Cache = new PostnomicCacheOptions
            {
                Enabled = true,
                MetadataDuration = TimeSpan.FromMinutes(5),
                PostListDuration = TimeSpan.FromMinutes(2),
                PostDetailDuration = TimeSpan.FromMinutes(5),
                PopularPostsDuration = TimeSpan.FromMinutes(10)
            }
        });

        _sut = new CachingPostnomicBlogService(_innerMock.Object, _cache, options);
    }

    public void Dispose() => _cache.Dispose();

    // ── GetBlogAsync — caching ────────────────────────────────────────────────

    [Fact]
    public async Task GetBlogAsync_ReturnsCachedValue_OnSecondCall()
    {
        // Arrange
        var blog = new PostnomicBlogInfo { Name = "Test Blog", Slug = BlogSlug };
        _innerMock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(blog);

        // Act
        var first = await _sut.GetBlogAsync();
        var second = await _sut.GetBlogAsync();

        // Assert
        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first!.Name.Should().Be(second!.Name);
        _innerMock.Verify(s => s.GetBlogAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBlogAsync_ReturnsValueFromInner_OnFirstCall()
    {
        // Arrange
        var blog = new PostnomicBlogInfo { Name = "My Blog", Slug = BlogSlug, Description = "A blog" };
        _innerMock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(blog);

        // Act
        var result = await _sut.GetBlogAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("My Blog");
        result.Description.Should().Be("A blog");
    }

    // ── GetTagsAsync — caching ────────────────────────────────────────────────

    [Fact]
    public async Task GetTagsAsync_ReturnsCachedValue_OnSecondCall()
    {
        // Arrange
        var tags = new List<PostnomicTag>
        {
            new() { Name = "C#", Slug = "csharp", PostCount = 5 }
        };
        _innerMock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tags);

        // Act
        var first = await _sut.GetTagsAsync();
        var second = await _sut.GetTagsAsync();

        // Assert
        first.Should().HaveCount(1);
        second.Should().HaveCount(1);
        _innerMock.Verify(s => s.GetTagsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetCategoriesAsync — caching ──────────────────────────────────────────

    [Fact]
    public async Task GetCategoriesAsync_ReturnsCachedValue_OnSecondCall()
    {
        // Arrange
        var categories = new List<PostnomicCategory>
        {
            new() { Name = "Tutorials", Slug = "tutorials", PostCount = 8 }
        };
        _innerMock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        // Act
        var first = await _sut.GetCategoriesAsync();
        var second = await _sut.GetCategoriesAsync();

        // Assert
        first.Should().HaveCount(1);
        second.Should().HaveCount(1);
        _innerMock.Verify(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetAuthorsAsync — caching ─────────────────────────────────────────────

    [Fact]
    public async Task GetAuthorsAsync_ReturnsCachedValue_OnSecondCall()
    {
        // Arrange
        var authors = new List<PostnomicAuthor>
        {
            new() { Name = "Jane Doe", PostCount = 10 }
        };
        _innerMock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(authors);

        // Act
        var first = await _sut.GetAuthorsAsync();
        var second = await _sut.GetAuthorsAsync();

        // Assert
        first.Should().HaveCount(1);
        second.Should().HaveCount(1);
        _innerMock.Verify(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetAuthorProfileAsync — caching ───────────────────────────────────────

    [Fact]
    public async Task GetAuthorProfileAsync_ReturnsCachedValue_OnSecondCall()
    {
        // Arrange
        const string authorSlug = "jane-doe";
        var profile = new PostnomicAuthorProfile { Name = "Jane Doe", Slug = authorSlug };
        _innerMock.Setup(s => s.GetAuthorProfileAsync(authorSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var first = await _sut.GetAuthorProfileAsync(authorSlug);
        var second = await _sut.GetAuthorProfileAsync(authorSlug);

        // Assert
        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first!.Name.Should().Be(second!.Name);
        _innerMock.Verify(
            s => s.GetAuthorProfileAsync(authorSlug, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAuthorProfileAsync_CacheIsKeyedPerAuthorSlug()
    {
        // Arrange — two different author slugs must each produce a separate cache entry
        var profileJane = new PostnomicAuthorProfile { Name = "Jane Doe", Slug = "jane-doe" };
        var profileJohn = new PostnomicAuthorProfile { Name = "John Smith", Slug = "john-smith" };

        _innerMock.Setup(s => s.GetAuthorProfileAsync("jane-doe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileJane);
        _innerMock.Setup(s => s.GetAuthorProfileAsync("john-smith", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileJohn);

        // Act — call each author twice
        var jane1 = await _sut.GetAuthorProfileAsync("jane-doe");
        var jane2 = await _sut.GetAuthorProfileAsync("jane-doe");
        var john1 = await _sut.GetAuthorProfileAsync("john-smith");
        var john2 = await _sut.GetAuthorProfileAsync("john-smith");

        // Assert — inner called once per distinct slug
        jane1!.Name.Should().Be("Jane Doe");
        jane2!.Name.Should().Be("Jane Doe");
        john1!.Name.Should().Be("John Smith");
        john2!.Name.Should().Be("John Smith");
        _innerMock.Verify(
            s => s.GetAuthorProfileAsync("jane-doe", It.IsAny<CancellationToken>()),
            Times.Once);
        _innerMock.Verify(
            s => s.GetAuthorProfileAsync("john-smith", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── GetPostsAsync — caching ───────────────────────────────────────────────

    [Fact]
    public async Task GetPostsAsync_ReturnsCachedValue_ForSameParameters()
    {
        // Arrange
        var pagedResult = CreateEmptyPagedResult(page: 1, pageSize: 5);
        _innerMock.Setup(s => s.GetPostsAsync(1, 5, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var first = await _sut.GetPostsAsync(page: 1, pageSize: 5);
        var second = await _sut.GetPostsAsync(page: 1, pageSize: 5);

        // Assert
        first.Should().NotBeNull();
        second.Should().NotBeNull();
        _innerMock.Verify(
            s => s.GetPostsAsync(1, 5, null, null, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPostsAsync_CallsInner_ForDifferentPageNumber()
    {
        // Arrange — page 1 and page 2 must map to different cache keys
        var page1Result = CreateEmptyPagedResult(page: 1, pageSize: 5);
        var page2Result = CreateEmptyPagedResult(page: 2, pageSize: 5);

        _innerMock.Setup(s => s.GetPostsAsync(1, 5, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page1Result);
        _innerMock.Setup(s => s.GetPostsAsync(2, 5, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page2Result);

        // Act
        await _sut.GetPostsAsync(page: 1, pageSize: 5);
        await _sut.GetPostsAsync(page: 2, pageSize: 5);

        // Assert — inner called once for each distinct page
        _innerMock.Verify(
            s => s.GetPostsAsync(1, 5, null, null, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
        _innerMock.Verify(
            s => s.GetPostsAsync(2, 5, null, null, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPostsAsync_CallsInner_ForDifferentTagFilter()
    {
        // Arrange — tag "csharp" vs tag "dotnet" must produce separate cache keys
        var csharpResult = CreateEmptyPagedResult();
        var dotnetResult = CreateEmptyPagedResult();

        _innerMock.Setup(s => s.GetPostsAsync(1, 5, "csharp", null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(csharpResult);
        _innerMock.Setup(s => s.GetPostsAsync(1, 5, "dotnet", null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dotnetResult);

        // Act — call each tag combination twice
        await _sut.GetPostsAsync(tag: "csharp");
        await _sut.GetPostsAsync(tag: "csharp");
        await _sut.GetPostsAsync(tag: "dotnet");
        await _sut.GetPostsAsync(tag: "dotnet");

        // Assert — inner called once per distinct filter value
        _innerMock.Verify(
            s => s.GetPostsAsync(1, 5, "csharp", null, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
        _innerMock.Verify(
            s => s.GetPostsAsync(1, 5, "dotnet", null, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── GetPostAsync — caching ────────────────────────────────────────────────

    [Fact]
    public async Task GetPostAsync_ReturnsCachedValue_OnSecondCall()
    {
        // Arrange
        const string postSlug = "my-post";
        var post = new PostnomicPostDetail { Slug = postSlug, Title = "My Post", AuthorName = "Jane" };
        _innerMock.Setup(s => s.GetPostAsync(postSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        // Act
        var first = await _sut.GetPostAsync(postSlug);
        var second = await _sut.GetPostAsync(postSlug);

        // Assert
        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first!.Title.Should().Be(second!.Title);
        _innerMock.Verify(
            s => s.GetPostAsync(postSlug, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPostAsync_CacheIsKeyedPerPostSlug()
    {
        // Arrange
        var postA = new PostnomicPostDetail { Slug = "post-a", Title = "Post A", AuthorName = "Jane" };
        var postB = new PostnomicPostDetail { Slug = "post-b", Title = "Post B", AuthorName = "John" };

        _innerMock.Setup(s => s.GetPostAsync("post-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(postA);
        _innerMock.Setup(s => s.GetPostAsync("post-b", It.IsAny<CancellationToken>()))
            .ReturnsAsync(postB);

        // Act — each post fetched twice
        var a1 = await _sut.GetPostAsync("post-a");
        var a2 = await _sut.GetPostAsync("post-a");
        var b1 = await _sut.GetPostAsync("post-b");
        var b2 = await _sut.GetPostAsync("post-b");

        // Assert — inner called once per distinct slug
        a1!.Title.Should().Be("Post A");
        a2!.Title.Should().Be("Post A");
        b1!.Title.Should().Be("Post B");
        b2!.Title.Should().Be("Post B");
        _innerMock.Verify(s => s.GetPostAsync("post-a", It.IsAny<CancellationToken>()), Times.Once);
        _innerMock.Verify(s => s.GetPostAsync("post-b", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetTopCommentedPostsAsync — caching ───────────────────────────────────

    [Fact]
    public async Task GetTopCommentedPostsAsync_ReturnsCachedValue_OnSecondCall()
    {
        // Arrange
        var posts = new List<PostnomicPopularPost>
        {
            new() { Slug = "top-post", Title = "Top Post", Count = 50 }
        };
        _innerMock.Setup(s => s.GetTopCommentedPostsAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);

        // Act
        var first = await _sut.GetTopCommentedPostsAsync();
        var second = await _sut.GetTopCommentedPostsAsync();

        // Assert
        first.Should().HaveCount(1);
        second.Should().HaveCount(1);
        _innerMock.Verify(
            s => s.GetTopCommentedPostsAsync(3, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTopCommentedPostsAsync_CacheIsKeyedPerCount()
    {
        // Arrange — count=3 and count=5 must use separate cache keys
        var top3 = new List<PostnomicPopularPost>
        {
            new() { Slug = "post-1", Title = "Post 1", Count = 10 }
        };
        var top5 = new List<PostnomicPopularPost>
        {
            new() { Slug = "post-1", Title = "Post 1", Count = 10 },
            new() { Slug = "post-2", Title = "Post 2", Count = 8 }
        };
        _innerMock.Setup(s => s.GetTopCommentedPostsAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(top3);
        _innerMock.Setup(s => s.GetTopCommentedPostsAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(top5);

        // Act — each count fetched twice
        await _sut.GetTopCommentedPostsAsync(count: 3);
        await _sut.GetTopCommentedPostsAsync(count: 3);
        await _sut.GetTopCommentedPostsAsync(count: 5);
        await _sut.GetTopCommentedPostsAsync(count: 5);

        // Assert — inner called once per distinct count
        _innerMock.Verify(s => s.GetTopCommentedPostsAsync(3, It.IsAny<CancellationToken>()), Times.Once);
        _innerMock.Verify(s => s.GetTopCommentedPostsAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetMostReadPostsAsync — caching ───────────────────────────────────────

    [Fact]
    public async Task GetMostReadPostsAsync_ReturnsCachedValue_OnSecondCall()
    {
        // Arrange
        var posts = new List<PostnomicPopularPost>
        {
            new() { Slug = "popular-post", Title = "Popular Post", Count = 9999 }
        };
        _innerMock.Setup(s => s.GetMostReadPostsAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);

        // Act
        var first = await _sut.GetMostReadPostsAsync();
        var second = await _sut.GetMostReadPostsAsync();

        // Assert
        first.Should().HaveCount(1);
        second.Should().HaveCount(1);
        _innerMock.Verify(
            s => s.GetMostReadPostsAsync(3, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetMostReadPostsAsync_CacheIsKeyedPerCount()
    {
        // Arrange
        _innerMock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _sut.GetMostReadPostsAsync(count: 3);
        await _sut.GetMostReadPostsAsync(count: 3);
        await _sut.GetMostReadPostsAsync(count: 10);
        await _sut.GetMostReadPostsAsync(count: 10);

        // Assert
        _innerMock.Verify(s => s.GetMostReadPostsAsync(3, It.IsAny<CancellationToken>()), Times.Once);
        _innerMock.Verify(s => s.GetMostReadPostsAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Write operations — pass-through ───────────────────────────────────────

    [Fact]
    public async Task CreateCommentAsync_AlwaysCallsInner()
    {
        // Arrange
        const string postSlug = "a-post";
        var request = new PostnomicCreateCommentRequest { Body = "Great article!" };
        var comment = new PostnomicComment
        {
            PublicId = "cmt-1",
            Body = "Great article!",
            CreatedAt = DateTime.UtcNow
        };
        _innerMock.Setup(s => s.CreateCommentAsync(postSlug, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        // Act — call three times; inner must be called every time (no caching)
        await _sut.CreateCommentAsync(postSlug, request);
        await _sut.CreateCommentAsync(postSlug, request);
        await _sut.CreateCommentAsync(postSlug, request);

        // Assert
        _innerMock.Verify(
            s => s.CreateCommentAsync(postSlug, request, It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task RecordPageViewAsync_AlwaysCallsInner()
    {
        // Arrange
        _innerMock.Setup(s => s.RecordPageViewAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act — call twice; both must reach the inner service
        await _sut.RecordPageViewAsync("session-1", "my-post", "https://referrer.com");
        await _sut.RecordPageViewAsync("session-1", "my-post", "https://referrer.com");

        // Assert
        _innerMock.Verify(
            s => s.RecordPageViewAsync("session-1", "my-post", "https://referrer.com",
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateReadDurationAsync_AlwaysCallsInner()
    {
        // Arrange
        _innerMock.Setup(s => s.UpdateReadDurationAsync(
                It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act — call twice; both must reach the inner service
        await _sut.UpdateReadDurationAsync("session-1", 120);
        await _sut.UpdateReadDurationAsync("session-1", 120);

        // Assert
        _innerMock.Verify(
            s => s.UpdateReadDurationAsync("session-1", 120, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ── CreateCommentAsync — post-detail invalidation ─────────────────────────

    [Fact]
    public async Task CreateCommentAsync_WhenSuccessful_InvalidatesPostDetailCache()
    {
        // Arrange — prime the post detail cache, then create a comment and verify the cache
        // was busted by checking that the inner service is called again for the same slug.
        const string postSlug = "commented-post";
        var post = new PostnomicPostDetail
        {
            Slug = postSlug,
            Title = "Commented Post",
            AuthorName = "Jane"
        };
        var comment = new PostnomicComment
        {
            PublicId = "cmt-99",
            Body = "Nice post!",
            CreatedAt = DateTime.UtcNow
        };
        var request = new PostnomicCreateCommentRequest { Body = "Nice post!" };

        _innerMock.Setup(s => s.GetPostAsync(postSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _innerMock.Setup(s => s.CreateCommentAsync(postSlug, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        // Prime cache
        await _sut.GetPostAsync(postSlug);
        _innerMock.Verify(s => s.GetPostAsync(postSlug, It.IsAny<CancellationToken>()), Times.Once);

        // Act — create a comment, which should bust the post detail cache
        await _sut.CreateCommentAsync(postSlug, request);

        // Fetch post again — must call inner again because the cache was invalidated
        await _sut.GetPostAsync(postSlug);

        // Assert — inner called a second time for GetPostAsync after invalidation
        _innerMock.Verify(s => s.GetPostAsync(postSlug, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateCommentAsync_WhenInnerReturnsNull_DoesNotInvalidatePostDetailCache()
    {
        // Arrange — when CreateCommentAsync returns null (failure) the cache must not be touched
        const string postSlug = "a-post";
        var post = new PostnomicPostDetail { Slug = postSlug, Title = "A Post", AuthorName = "Jane" };
        var request = new PostnomicCreateCommentRequest { Body = "Bad comment" };

        _innerMock.Setup(s => s.GetPostAsync(postSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _innerMock.Setup(s => s.CreateCommentAsync(postSlug, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostnomicComment?)null);

        // Prime cache
        await _sut.GetPostAsync(postSlug);

        // Act — failed comment creation
        await _sut.CreateCommentAsync(postSlug, request);

        // Fetch post again — cache should still be warm (inner must NOT be called again)
        await _sut.GetPostAsync(postSlug);

        // Assert — inner called only once; cache was not invalidated
        _innerMock.Verify(s => s.GetPostAsync(postSlug, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── IPostnomicCacheControl.InvalidateAll ──────────────────────────────────

    [Fact]
    public async Task InvalidateAll_CausesAllCachedEntriesToBeRefetched()
    {
        // Arrange — prime every read-through operation once
        var blog = new PostnomicBlogInfo { Name = "Blog", Slug = BlogSlug };
        var tags = new List<PostnomicTag> { new() { Name = "tag", Slug = "tag", PostCount = 1 } };
        var categories = new List<PostnomicCategory>
            { new() { Name = "cat", Slug = "cat", PostCount = 1 } };
        var authors = new List<PostnomicAuthor> { new() { Name = "Jane", PostCount = 3 } };

        _innerMock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(blog);
        _innerMock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tags);
        _innerMock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(categories);
        _innerMock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(authors);

        await _sut.GetBlogAsync();
        await _sut.GetTagsAsync();
        await _sut.GetCategoriesAsync();
        await _sut.GetAuthorsAsync();

        // Act
        _sut.InvalidateAll();

        // Fetch again after invalidation — each must hit the inner service
        await _sut.GetBlogAsync();
        await _sut.GetTagsAsync();
        await _sut.GetCategoriesAsync();
        await _sut.GetAuthorsAsync();

        // Assert — inner was called twice for each (once before invalidate, once after)
        _innerMock.Verify(s => s.GetBlogAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _innerMock.Verify(s => s.GetTagsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _innerMock.Verify(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _innerMock.Verify(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task InvalidateAll_AllowsSubsequentCallsToBeCachedAgain()
    {
        // Arrange
        _innerMock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Blog", Slug = BlogSlug });

        await _sut.GetBlogAsync();    // primes cache
        _sut.InvalidateAll();         // clears cache

        // Act — two more calls after invalidation
        await _sut.GetBlogAsync();
        await _sut.GetBlogAsync();

        // Assert — inner called once before invalidation + once more after (second call re-cached)
        _innerMock.Verify(s => s.GetBlogAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── IPostnomicCacheControl.InvalidatePost ─────────────────────────────────

    [Fact]
    public async Task InvalidatePost_ClearsOnlyTheNamedPostDetailEntry()
    {
        // Arrange — prime two different post slugs
        const string slugA = "post-a";
        const string slugB = "post-b";
        var postA = new PostnomicPostDetail { Slug = slugA, Title = "Post A", AuthorName = "Jane" };
        var postB = new PostnomicPostDetail { Slug = slugB, Title = "Post B", AuthorName = "John" };

        _innerMock.Setup(s => s.GetPostAsync(slugA, It.IsAny<CancellationToken>())).ReturnsAsync(postA);
        _innerMock.Setup(s => s.GetPostAsync(slugB, It.IsAny<CancellationToken>())).ReturnsAsync(postB);

        await _sut.GetPostAsync(slugA);
        await _sut.GetPostAsync(slugB);

        // Act — invalidate only post A
        _sut.InvalidatePost(slugA);

        // Fetch both again
        await _sut.GetPostAsync(slugA);
        await _sut.GetPostAsync(slugB);

        // Assert — post A re-fetched (2×), post B still cached (1×)
        _innerMock.Verify(s => s.GetPostAsync(slugA, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _innerMock.Verify(s => s.GetPostAsync(slugB, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidatePost_DoesNotAffectOtherCacheEntries()
    {
        // Arrange — prime blog metadata and a specific post
        const string postSlug = "my-post";
        _innerMock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Blog", Slug = BlogSlug });
        _innerMock.Setup(s => s.GetPostAsync(postSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPostDetail { Slug = postSlug, Title = "T", AuthorName = "A" });

        await _sut.GetBlogAsync();
        await _sut.GetPostAsync(postSlug);

        // Act — invalidate just the post
        _sut.InvalidatePost(postSlug);

        // Blog info should still come from cache
        await _sut.GetBlogAsync();

        // Assert — blog info not re-fetched (still cached), post re-fetched
        _innerMock.Verify(s => s.GetBlogAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── IPostnomicCacheControl.InvalidateMetadata ─────────────────────────────

    [Fact]
    public async Task InvalidateMetadata_ClearsBlogInfoTagsCategoriesAndAuthors()
    {
        // Arrange — prime all metadata entries
        _innerMock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Blog", Slug = BlogSlug });
        _innerMock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicTag { Name = "tag", Slug = "tag", PostCount = 1 }]);
        _innerMock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicCategory { Name = "cat", Slug = "cat", PostCount = 1 }]);
        _innerMock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PostnomicAuthor { Name = "Jane", PostCount = 3 }]);

        await _sut.GetBlogAsync();
        await _sut.GetTagsAsync();
        await _sut.GetCategoriesAsync();
        await _sut.GetAuthorsAsync();

        // Act
        _sut.InvalidateMetadata();

        // Fetch all metadata again
        await _sut.GetBlogAsync();
        await _sut.GetTagsAsync();
        await _sut.GetCategoriesAsync();
        await _sut.GetAuthorsAsync();

        // Assert — each metadata entry re-fetched after invalidation
        _innerMock.Verify(s => s.GetBlogAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _innerMock.Verify(s => s.GetTagsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _innerMock.Verify(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _innerMock.Verify(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task InvalidateMetadata_ClearsAuthorProfileEntries()
    {
        // Arrange — prime an author profile entry
        const string authorSlug = "jane-doe";
        var profile = new PostnomicAuthorProfile { Name = "Jane Doe", Slug = authorSlug };
        _innerMock.Setup(s => s.GetAuthorProfileAsync(authorSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        await _sut.GetAuthorProfileAsync(authorSlug);

        // Act
        _sut.InvalidateMetadata();

        // Fetch again — must hit the inner service
        await _sut.GetAuthorProfileAsync(authorSlug);

        // Assert
        _innerMock.Verify(
            s => s.GetAuthorProfileAsync(authorSlug, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task InvalidateMetadata_DoesNotAffectPostDetailOrPostListCache()
    {
        // Arrange — prime a post detail and post list
        const string postSlug = "my-post";
        _innerMock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Blog", Slug = BlogSlug });
        _innerMock.Setup(s => s.GetPostAsync(postSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPostDetail { Slug = postSlug, Title = "T", AuthorName = "A" });
        _innerMock.Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyPagedResult());

        await _sut.GetPostAsync(postSlug);
        await _sut.GetPostsAsync();

        // Act — only metadata should be cleared
        _sut.InvalidateMetadata();

        // Fetch posts and post detail again — should still come from cache
        await _sut.GetPostAsync(postSlug);
        await _sut.GetPostsAsync();

        // Assert — post detail and post list NOT re-fetched (still cached)
        _innerMock.Verify(s => s.GetPostAsync(postSlug, It.IsAny<CancellationToken>()), Times.Once);
        _innerMock.Verify(
            s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Null caching ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBlogAsync_CachesNullResult_WhenInnerReturnsNull()
    {
        // Arrange — API returns null (blog not found)
        _innerMock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostnomicBlogInfo?)null);

        // Act
        var first = await _sut.GetBlogAsync();
        var second = await _sut.GetBlogAsync();

        // Assert — null result is cached; inner called only once
        first.Should().BeNull();
        second.Should().BeNull();
        _innerMock.Verify(s => s.GetBlogAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPostAsync_CachesNullResult_WhenInnerReturnsNull()
    {
        // Arrange — post does not exist
        const string postSlug = "missing-post";
        _innerMock.Setup(s => s.GetPostAsync(postSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostnomicPostDetail?)null);

        // Act
        var first = await _sut.GetPostAsync(postSlug);
        var second = await _sut.GetPostAsync(postSlug);

        // Assert — null result is cached; inner called only once
        first.Should().BeNull();
        second.Should().BeNull();
        _innerMock.Verify(
            s => s.GetPostAsync(postSlug, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAuthorProfileAsync_CachesNullResult_WhenInnerReturnsNull()
    {
        // Arrange — author does not exist
        const string authorSlug = "ghost-author";
        _innerMock.Setup(s => s.GetAuthorProfileAsync(authorSlug, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostnomicAuthorProfile?)null);

        // Act
        var first = await _sut.GetAuthorProfileAsync(authorSlug);
        var second = await _sut.GetAuthorProfileAsync(authorSlug);

        // Assert — null result is cached; inner called only once
        first.Should().BeNull();
        second.Should().BeNull();
        _innerMock.Verify(
            s => s.GetAuthorProfileAsync(authorSlug, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── IPostnomicCacheControl implementation ─────────────────────────────────

    [Fact]
    public void Sut_ImplementsIPostnomicCacheControl()
    {
        // Assert — the service must also satisfy the cache-control contract
        _sut.Should().BeAssignableTo<IPostnomicCacheControl>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PostnomicPagedResult<PostnomicPostSummary> CreateEmptyPagedResult(
        int page = 1, int pageSize = 5) =>
        new()
        {
            Items = [],
            Page = page,
            PageSize = pageSize,
            TotalCount = 0,
            TotalPages = 0
        };
}

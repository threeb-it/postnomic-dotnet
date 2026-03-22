using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using RichardSzalay.MockHttp;

namespace Postnomic.Client.Tests;

/// <summary>
/// Unit tests for <see cref="PostnomicBlogService"/>.
/// Uses <see cref="MockHttpMessageHandler"/> from RichardSzalay.MockHttp to intercept all
/// outgoing HTTP calls and verify correct URL construction, query parameter handling,
/// response deserialization, and null/error fallback behaviour.
/// </summary>
public class PostnomicBlogServiceTests : IDisposable
{
    private const string BaseUrl = "https://api.postnomic.com";
    private const string BlogSlug = "test-blog";

    private readonly MockHttpMessageHandler _mockHttp;
    private readonly PostnomicBlogService _sut;

    public PostnomicBlogServiceTests()
    {
        _mockHttp = new MockHttpMessageHandler();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri(BaseUrl + "/");

        var options = Options.Create(new PostnomicClientOptions
        {
            BaseUrl = BaseUrl,
            ApiKey = "test-key",
            BlogSlug = BlogSlug
        });

        _sut = new PostnomicBlogService(httpClient, options);
    }

    public void Dispose() => _mockHttp.Dispose();

    // ── GetBlogAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBlogAsync_WhenApiReturnsOk_ReturnsBlogInfo()
    {
        // Arrange
        var expected = new PostnomicBlogInfo
        {
            Name = "Test Blog",
            Slug = BlogSlug,
            Description = "A test blog"
        };

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}")
            .Respond(HttpStatusCode.OK, JsonContent.Create(expected));

        // Act
        var result = await _sut.GetBlogAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Blog");
        result.Slug.Should().Be(BlogSlug);
        result.Description.Should().Be("A test blog");
    }

    [Fact]
    public async Task GetBlogAsync_WhenApiReturns404_ReturnsNull()
    {
        // Arrange
        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}")
            .Respond(HttpStatusCode.NotFound);

        // Act
        var result = await _sut.GetBlogAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBlogAsync_CallsCorrectUrl()
    {
        // Arrange
        _mockHttp
            .Expect(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}")
            .Respond(HttpStatusCode.OK, JsonContent.Create(
                new PostnomicBlogInfo { Name = "Blog", Slug = BlogSlug }));

        // Act
        await _sut.GetBlogAsync();

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    // ── GetTagsAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTagsAsync_WhenApiReturnsOk_ReturnsTagList()
    {
        // Arrange
        var tags = new List<PostnomicTag>
        {
            new() { Name = "C#", Slug = "csharp", PostCount = 5 },
            new() { Name = ".NET", Slug = "dotnet", PostCount = 3 }
        };

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/tags")
            .Respond(HttpStatusCode.OK, JsonContent.Create(tags));

        // Act
        var result = await _sut.GetTagsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(t => t.Slug == "csharp");
    }

    [Fact]
    public async Task GetTagsAsync_WhenApiReturnsNull_ReturnsEmptyList()
    {
        // Arrange — server returns JSON null
        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/tags")
            .Respond(HttpStatusCode.OK, new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        // Act
        var result = await _sut.GetTagsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // ── GetCategoriesAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetCategoriesAsync_WhenApiReturnsOk_ReturnsCategoryList()
    {
        // Arrange
        var categories = new List<PostnomicCategory>
        {
            new() { Name = "Tutorials", Slug = "tutorials", PostCount = 8 }
        };

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/categories")
            .Respond(HttpStatusCode.OK, JsonContent.Create(categories));

        // Act
        var result = await _sut.GetCategoriesAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("tutorials");
    }

    [Fact]
    public async Task GetCategoriesAsync_WhenApiReturnsNull_ReturnsEmptyList()
    {
        // Arrange
        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/categories")
            .Respond(HttpStatusCode.OK, new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        // Act
        var result = await _sut.GetCategoriesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // ── GetAuthorsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuthorsAsync_WhenApiReturnsOk_ReturnsAuthorList()
    {
        // Arrange
        var authors = new List<PostnomicAuthor>
        {
            new() { Name = "Jane Doe", PostCount = 10 },
            new() { Name = "John Smith", PostCount = 4 }
        };

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/authors")
            .Respond(HttpStatusCode.OK, JsonContent.Create(authors));

        // Act
        var result = await _sut.GetAuthorsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(a => a.Name == "Jane Doe");
    }

    [Fact]
    public async Task GetAuthorsAsync_WhenApiReturnsNull_ReturnsEmptyList()
    {
        // Arrange
        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/authors")
            .Respond(HttpStatusCode.OK, new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        // Act
        var result = await _sut.GetAuthorsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // ── GetPostsAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPostsAsync_WithDefaultParameters_SendsCorrectUrl()
    {
        // Arrange
        var pagedResult = CreateEmptyPagedResult();

        _mockHttp
            .Expect(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts")
            .WithQueryString("page", "1")
            .WithQueryString("pageSize", "5")
            .Respond(HttpStatusCode.OK, JsonContent.Create(pagedResult));

        // Act
        await _sut.GetPostsAsync();

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPostsAsync_WithTagFilter_IncludesTagQueryParameter()
    {
        // Arrange
        var pagedResult = CreateEmptyPagedResult();

        _mockHttp
            .Expect(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts")
            .WithQueryString("tag", "csharp")
            .Respond(HttpStatusCode.OK, JsonContent.Create(pagedResult));

        // Act
        await _sut.GetPostsAsync(tag: "csharp");

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPostsAsync_WithCategoryFilter_IncludesCategoryQueryParameter()
    {
        // Arrange
        var pagedResult = CreateEmptyPagedResult();

        _mockHttp
            .Expect(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts")
            .WithQueryString("category", "tutorials")
            .Respond(HttpStatusCode.OK, JsonContent.Create(pagedResult));

        // Act
        await _sut.GetPostsAsync(category: "tutorials");

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPostsAsync_WithAuthorFilter_IncludesAuthorQueryParameter()
    {
        // Arrange
        var pagedResult = CreateEmptyPagedResult();

        _mockHttp
            .Expect(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts")
            .WithQueryString("author", "Jane Doe")
            .Respond(HttpStatusCode.OK, JsonContent.Create(pagedResult));

        // Act
        await _sut.GetPostsAsync(author: "Jane Doe");

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPostsAsync_WithSearchTerm_IncludesSearchQueryParameter()
    {
        // Arrange
        var pagedResult = CreateEmptyPagedResult();

        _mockHttp
            .Expect(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts")
            .WithQueryString("search", "hello world")
            .Respond(HttpStatusCode.OK, JsonContent.Create(pagedResult));

        // Act
        await _sut.GetPostsAsync(search: "hello world");

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPostsAsync_WithCustomPageAndSize_IncludesPagingParameters()
    {
        // Arrange
        var pagedResult = CreateEmptyPagedResult(page: 3, pageSize: 10);

        _mockHttp
            .Expect(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts")
            .WithQueryString("page", "3")
            .WithQueryString("pageSize", "10")
            .Respond(HttpStatusCode.OK, JsonContent.Create(pagedResult));

        // Act
        await _sut.GetPostsAsync(page: 3, pageSize: 10);

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPostsAsync_WhenApiReturnsOk_DeserializesResult()
    {
        // Arrange
        var pagedResult = new PostnomicPagedResult<PostnomicPostSummary>
        {
            Items =
            [
                new PostnomicPostSummary
                {
                    Slug = "post-one",
                    Title = "Post One",
                    AuthorName = "Jane",
                    PublishedAt = DateTime.UtcNow
                }
            ],
            Page = 1,
            PageSize = 5,
            TotalCount = 1,
            TotalPages = 1
        };

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts")
            .Respond(HttpStatusCode.OK, JsonContent.Create(pagedResult));

        // Act
        var result = await _sut.GetPostsAsync();

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items.First().Slug.Should().Be("post-one");
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPostsAsync_WhenApiReturnsNull_ReturnsFallbackResult()
    {
        // Arrange
        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts")
            .Respond(HttpStatusCode.OK, new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        // Act
        var result = await _sut.GetPostsAsync(page: 2, pageSize: 10);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetPostsAsync_WithNullFilters_OmitsFilterQueryParameters()
    {
        // Arrange — we verify the request URL does NOT contain tag/category/author/search
        var pagedResult = CreateEmptyPagedResult();
        string? capturedUrl = null;

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts")
            .Respond(req =>
            {
                capturedUrl = req.RequestUri?.ToString();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(pagedResult)
                };
            });

        // Act
        await _sut.GetPostsAsync(tag: null, category: null, author: null, search: null);

        // Assert
        capturedUrl.Should().NotContain("tag=");
        capturedUrl.Should().NotContain("category=");
        capturedUrl.Should().NotContain("author=");
        capturedUrl.Should().NotContain("search=");
    }

    // ── GetPostAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPostAsync_WhenApiReturnsOk_ReturnsPostDetail()
    {
        // Arrange
        const string postSlug = "my-post";
        var detail = new PostnomicPostDetail
        {
            Slug = postSlug,
            Title = "My Post",
            AuthorName = "Jane",
            CommentsEnabled = true
        };

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts/{postSlug}")
            .Respond(HttpStatusCode.OK, JsonContent.Create(detail));

        // Act
        var result = await _sut.GetPostAsync(postSlug);

        // Assert
        result.Should().NotBeNull();
        result!.Slug.Should().Be(postSlug);
        result.Title.Should().Be("My Post");
        result.CommentsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetPostAsync_WhenApiReturns404_ReturnsNull()
    {
        // Arrange
        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts/missing-post")
            .Respond(HttpStatusCode.NotFound);

        // Act
        var result = await _sut.GetPostAsync("missing-post");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPostAsync_CallsCorrectUrl()
    {
        // Arrange
        const string postSlug = "specific-post";

        _mockHttp
            .Expect(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts/{postSlug}")
            .Respond(HttpStatusCode.OK, JsonContent.Create(
                new PostnomicPostDetail { Slug = postSlug, Title = "T", AuthorName = "A" }));

        // Act
        await _sut.GetPostAsync(postSlug);

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    // ── CreateCommentAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCommentAsync_WhenApiReturnsCreated_ReturnsComment()
    {
        // Arrange
        const string postSlug = "a-post";
        var request = new PostnomicCreateCommentRequest { Body = "Great!" };
        var created = new PostnomicComment
        {
            PublicId = "cmt-1",
            Body = "Great!",
            CreatedAt = DateTime.UtcNow
        };

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUrl}/public/blogs/{BlogSlug}/posts/{postSlug}/comments")
            .Respond(HttpStatusCode.Created, JsonContent.Create(created));

        // Act
        var result = await _sut.CreateCommentAsync(postSlug, request);

        // Assert
        result.Should().NotBeNull();
        result!.PublicId.Should().Be("cmt-1");
        result.Body.Should().Be("Great!");
    }

    [Fact]
    public async Task CreateCommentAsync_WhenApiReturnsBadRequest_ReturnsNull()
    {
        // Arrange
        const string postSlug = "a-post";
        var request = new PostnomicCreateCommentRequest { Body = "" };

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUrl}/public/blogs/{BlogSlug}/posts/{postSlug}/comments")
            .Respond(HttpStatusCode.BadRequest);

        // Act
        var result = await _sut.CreateCommentAsync(postSlug, request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateCommentAsync_WhenApiReturns404_ReturnsNull()
    {
        // Arrange
        const string postSlug = "nonexistent";
        var request = new PostnomicCreateCommentRequest { Body = "Body" };

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUrl}/public/blogs/{BlogSlug}/posts/{postSlug}/comments")
            .Respond(HttpStatusCode.NotFound);

        // Act
        var result = await _sut.CreateCommentAsync(postSlug, request);

        // Assert
        result.Should().BeNull();
    }

    // ── GetTopCommentedPostsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetTopCommentedPostsAsync_WithDefaultCount_UsesCountThree()
    {
        // Arrange
        _mockHttp
            .Expect(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts/top-commented")
            .WithQueryString("count", "3")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<PostnomicPopularPost>()));

        // Act
        await _sut.GetTopCommentedPostsAsync();

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetTopCommentedPostsAsync_WithCustomCount_PassesCount()
    {
        // Arrange
        _mockHttp
            .Expect(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts/top-commented")
            .WithQueryString("count", "5")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<PostnomicPopularPost>()));

        // Act
        await _sut.GetTopCommentedPostsAsync(count: 5);

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetTopCommentedPostsAsync_WhenApiReturnsOk_ReturnsPopularPosts()
    {
        // Arrange
        var posts = new List<PostnomicPopularPost>
        {
            new() { Slug = "top-post", Title = "Top Post", Count = 100 }
        };

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts/top-commented")
            .Respond(HttpStatusCode.OK, JsonContent.Create(posts));

        // Act
        var result = await _sut.GetTopCommentedPostsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("top-post");
        result[0].Count.Should().Be(100);
    }

    [Fact]
    public async Task GetTopCommentedPostsAsync_WhenApiReturnsNull_ReturnsEmptyList()
    {
        // Arrange
        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts/top-commented")
            .Respond(HttpStatusCode.OK, new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        // Act
        var result = await _sut.GetTopCommentedPostsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // ── GetMostReadPostsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetMostReadPostsAsync_WithDefaultCount_UsesCountThree()
    {
        // Arrange
        _mockHttp
            .Expect(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts/most-read")
            .WithQueryString("count", "3")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<PostnomicPopularPost>()));

        // Act
        await _sut.GetMostReadPostsAsync();

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetMostReadPostsAsync_WhenApiReturnsOk_ReturnsPopularPosts()
    {
        // Arrange
        var posts = new List<PostnomicPopularPost>
        {
            new() { Slug = "most-read", Title = "Most Read", Count = 9999 }
        };

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts/most-read")
            .Respond(HttpStatusCode.OK, JsonContent.Create(posts));

        // Act
        var result = await _sut.GetMostReadPostsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Count.Should().Be(9999);
    }

    [Fact]
    public async Task GetMostReadPostsAsync_WhenApiReturnsNull_ReturnsEmptyList()
    {
        // Arrange
        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/posts/most-read")
            .Respond(HttpStatusCode.OK, new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        // Act
        var result = await _sut.GetMostReadPostsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // ── RecordPageViewAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task RecordPageViewAsync_WithAllParameters_PostsToCorrectUrl()
    {
        // Arrange
        _mockHttp
            .Expect(HttpMethod.Post, $"{BaseUrl}/public/blogs/{BlogSlug}/analytics/pageview")
            .Respond(HttpStatusCode.OK);

        // Act
        await _sut.RecordPageViewAsync("session-abc", "my-post", "https://referrer.com");

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task RecordPageViewAsync_WithNullPostSlug_PostsSuccessfully()
    {
        // Arrange
        _mockHttp
            .When(HttpMethod.Post, $"{BaseUrl}/public/blogs/{BlogSlug}/analytics/pageview")
            .Respond(HttpStatusCode.OK);

        // Act — should not throw
        var act = () => _sut.RecordPageViewAsync("session-xyz", postSlug: null);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordPageViewAsync_WhenApiReturnsError_ThrowsHttpRequestException()
    {
        // Arrange
        _mockHttp
            .When(HttpMethod.Post, $"{BaseUrl}/public/blogs/{BlogSlug}/analytics/pageview")
            .Respond(HttpStatusCode.InternalServerError);

        // Act
        var act = () => _sut.RecordPageViewAsync("session-xyz");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── UpdateReadDurationAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateReadDurationAsync_WithValidParameters_PatchesToCorrectUrl()
    {
        // Arrange
        _mockHttp
            .Expect(HttpMethod.Patch, $"{BaseUrl}/public/blogs/{BlogSlug}/analytics/pageview")
            .Respond(HttpStatusCode.OK);

        // Act
        await _sut.UpdateReadDurationAsync("session-abc", 120);

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task UpdateReadDurationAsync_WhenApiReturnsError_ThrowsHttpRequestException()
    {
        // Arrange
        _mockHttp
            .When(HttpMethod.Patch, $"{BaseUrl}/public/blogs/{BlogSlug}/analytics/pageview")
            .Respond(HttpStatusCode.InternalServerError);

        // Act
        var act = () => _sut.UpdateReadDurationAsync("session-abc", 60);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── GetAuthorProfileAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAuthorProfileAsync_WhenProfileExists_ReturnsProfile()
    {
        // Arrange
        const string authorSlug = "jane-doe";
        var profile = new PostnomicAuthorProfile
        {
            Name = "Jane Doe",
            Slug = authorSlug,
            Headline = "Senior Engineer",
            Bio = "Writes about .NET",
            Location = "Berlin",
            PostCount = 12
        };

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/authors/{authorSlug}")
            .Respond(HttpStatusCode.OK, JsonContent.Create(profile));

        // Act
        var result = await _sut.GetAuthorProfileAsync(authorSlug);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Jane Doe");
        result.Slug.Should().Be(authorSlug);
        result.Headline.Should().Be("Senior Engineer");
        result.Bio.Should().Be("Writes about .NET");
        result.Location.Should().Be("Berlin");
        result.PostCount.Should().Be(12);
    }

    [Fact]
    public async Task GetAuthorProfileAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        const string authorSlug = "nonexistent-author";

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/authors/{authorSlug}")
            .Respond(HttpStatusCode.NotFound);

        // Act
        var result = await _sut.GetAuthorProfileAsync(authorSlug);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAuthorProfileAsync_ResolvesRelativeProfileImageUrl()
    {
        // Arrange
        const string authorSlug = "jane-doe";
        var profile = new PostnomicAuthorProfile
        {
            Name = "Jane Doe",
            ProfileImageUrl = "/media/blob/avatar.jpg"
        };

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/authors/{authorSlug}")
            .Respond(HttpStatusCode.OK, JsonContent.Create(profile));

        // Act
        var result = await _sut.GetAuthorProfileAsync(authorSlug);

        // Assert
        result.Should().NotBeNull();
        result!.ProfileImageUrl.Should().Be($"{BaseUrl}/media/blob/avatar.jpg");
    }

    [Fact]
    public async Task GetAuthorProfileAsync_ResolvesRelativeHeaderImageUrl()
    {
        // Arrange
        const string authorSlug = "jane-doe";
        var profile = new PostnomicAuthorProfile
        {
            Name = "Jane Doe",
            HeaderImageUrl = "/media/blob/header.jpg"
        };

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/authors/{authorSlug}")
            .Respond(HttpStatusCode.OK, JsonContent.Create(profile));

        // Act
        var result = await _sut.GetAuthorProfileAsync(authorSlug);

        // Assert
        result.Should().NotBeNull();
        result!.HeaderImageUrl.Should().Be($"{BaseUrl}/media/blob/header.jpg");
    }

    [Fact]
    public async Task GetAuthorProfileAsync_ResolvesRelativeThumbnailUrlsInRecentPosts()
    {
        // Arrange
        const string authorSlug = "jane-doe";
        var profile = new PostnomicAuthorProfile
        {
            Name = "Jane Doe",
            RecentPosts =
            [
                new PostnomicPostSummary
                {
                    Slug = "post-one",
                    Title = "Post One",
                    AuthorName = "Jane Doe",
                    ThumbnailImageUrl = "/media/blob/post-one-thumb.jpg",
                    PublishedAt = DateTime.UtcNow
                },
                new PostnomicPostSummary
                {
                    Slug = "post-two",
                    Title = "Post Two",
                    AuthorName = "Jane Doe",
                    ThumbnailImageUrl = "/media/blob/post-two-thumb.jpg",
                    PublishedAt = DateTime.UtcNow
                }
            ]
        };

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/authors/{authorSlug}")
            .Respond(HttpStatusCode.OK, JsonContent.Create(profile));

        // Act
        var result = await _sut.GetAuthorProfileAsync(authorSlug);

        // Assert
        result.Should().NotBeNull();
        result!.RecentPosts.Should().HaveCount(2);
        result.RecentPosts.Should().OnlyContain(
            p => p.ThumbnailImageUrl!.StartsWith(BaseUrl));
        result.RecentPosts.First(p => p.Slug == "post-one").ThumbnailImageUrl
            .Should().Be($"{BaseUrl}/media/blob/post-one-thumb.jpg");
        result.RecentPosts.First(p => p.Slug == "post-two").ThumbnailImageUrl
            .Should().Be($"{BaseUrl}/media/blob/post-two-thumb.jpg");
    }

    [Fact]
    public async Task GetAuthorProfileAsync_CallsCorrectEndpoint()
    {
        // Arrange
        const string authorSlug = "jane-doe";

        _mockHttp
            .Expect(HttpMethod.Get, $"{BaseUrl}/public/blogs/{BlogSlug}/authors/{authorSlug}")
            .Respond(HttpStatusCode.OK, JsonContent.Create(
                new PostnomicAuthorProfile { Name = "Jane Doe" }));

        // Act
        await _sut.GetAuthorProfileAsync(authorSlug);

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
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

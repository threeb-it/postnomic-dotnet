using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using RichardSzalay.MockHttp;

namespace Postnomic.Client.Tests;

/// <summary>
/// Unit tests for <see cref="PostnomicAuthoringService"/>.
/// Uses <see cref="MockHttpMessageHandler"/> from RichardSzalay.MockHttp to intercept all
/// outgoing HTTP calls and verify correct URL construction, request bodies, response
/// deserialization, and the <see cref="PostnomicApiException"/> error-mapping behaviour.
/// </summary>
public class PostnomicAuthoringServiceTests : IDisposable
{
    private const string BaseUrl = "https://api.postnomic.com";
    private const string BlogId = "3f2a1c9e-1111-2222-3333-444455556666";

    private readonly MockHttpMessageHandler _mockHttp;
    private readonly PostnomicAuthoringService _sut;

    public PostnomicAuthoringServiceTests()
    {
        _mockHttp = new MockHttpMessageHandler();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri(BaseUrl + "/");

        var options = Options.Create(new PostnomicClientOptions
        {
            BaseUrl = BaseUrl,
            PersonalAccessToken = "pnp_test-token",
            BlogId = BlogId
        });

        _sut = new PostnomicAuthoringService(httpClient, options);
    }

    public void Dispose() => _mockHttp.Dispose();

    // ── CreatePostAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePostAsync_WhenApiReturnsCreated_ReturnsPost()
    {
        // Arrange
        var request = new PostnomicCreatePostRequest { Title = "Hello", Slug = "hello" };
        var created = CreatePost(status: PostnomicPostStatus.Draft);

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/posts")
            .Respond(HttpStatusCode.Created, JsonContent.Create(created));

        // Act
        var result = await _sut.CreatePostAsync(request);

        // Assert
        Assert.Equal(created.PublicId, result.PublicId);
        Assert.Equal(PostnomicPostStatus.Draft, result.Status);
    }

    [Fact]
    public async Task CreatePostAsync_WithPublishImmediately_CallsPublishAfterCreate()
    {
        // Arrange
        var request = new PostnomicCreatePostRequest { Title = "Hello", Slug = "hello", PublishImmediately = true };
        var draft = CreatePost(status: PostnomicPostStatus.Draft);
        var published = draft with { Status = PostnomicPostStatus.Published, PublishedAt = DateTime.UtcNow };

        _mockHttp
            .Expect(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/posts")
            .Respond(HttpStatusCode.Created, JsonContent.Create(draft));

        _mockHttp
            .Expect(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/posts/{draft.PublicId}/publish")
            .Respond(HttpStatusCode.OK, JsonContent.Create(published));

        // Act
        var result = await _sut.CreatePostAsync(request);

        // Assert
        Assert.Equal(PostnomicPostStatus.Published, result.Status);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task CreatePostAsync_WithoutPublishImmediately_DoesNotCallPublish()
    {
        // Arrange
        var request = new PostnomicCreatePostRequest { Title = "Hello", Slug = "hello" };
        var draft = CreatePost(status: PostnomicPostStatus.Draft);

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/posts")
            .Respond(HttpStatusCode.Created, JsonContent.Create(draft));

        // Act
        var result = await _sut.CreatePostAsync(request);

        // Assert — no expectation set on /publish, so any call there would 404 from MockHttp
        // and surface as a thrown exception; reaching this line proves it wasn't called.
        Assert.Equal(PostnomicPostStatus.Draft, result.Status);
    }

    [Fact]
    public async Task CreatePostAsync_WhenApiReturnsConflict_ThrowsWithBodyAsMessage()
    {
        // Arrange
        var request = new PostnomicCreatePostRequest { Title = "Hello", Slug = "taken" };

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/posts")
            .Respond(HttpStatusCode.Conflict, new StringContent(
                "A post with this slug already exists on this blog.", Encoding.UTF8, "text/plain"));

        // Act
        var ex = await Assert.ThrowsAsync<PostnomicApiException>(() => _sut.CreatePostAsync(request));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal("A post with this slug already exists on this blog.", ex.Message);
    }

    [Fact]
    public async Task CreatePostAsync_CallsCorrectUrl()
    {
        // Arrange
        var request = new PostnomicCreatePostRequest { Title = "Hello", Slug = "hello" };

        _mockHttp
            .Expect(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/posts")
            .Respond(HttpStatusCode.Created, JsonContent.Create(CreatePost()));

        // Act
        await _sut.CreatePostAsync(request);

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    // ── UpdatePostAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePostAsync_WhenApiReturnsOk_ReturnsUpdatedPost()
    {
        // Arrange
        const string postId = "post-1";
        var request = new PostnomicUpdatePostRequest { Title = "Updated", Slug = "updated" };
        var updated = CreatePost(publicId: postId, title: "Updated", slug: "updated");

        _mockHttp
            .When(HttpMethod.Put, $"{BaseUrl}/blogs/{BlogId}/posts/{postId}")
            .Respond(HttpStatusCode.OK, JsonContent.Create(updated));

        // Act
        var result = await _sut.UpdatePostAsync(postId, request);

        // Assert
        Assert.Equal("Updated", result.Title);
        Assert.Equal("updated", result.Slug);
    }

    [Fact]
    public async Task UpdatePostAsync_WhenApiReturnsNotFound_ThrowsPostnomicApiException()
    {
        // Arrange
        const string postId = "missing";
        var request = new PostnomicUpdatePostRequest { Title = "T", Slug = "s" };

        _mockHttp
            .When(HttpMethod.Put, $"{BaseUrl}/blogs/{BlogId}/posts/{postId}")
            .Respond(HttpStatusCode.NotFound, new StringContent("Post not found.", Encoding.UTF8, "text/plain"));

        // Act
        var ex = await Assert.ThrowsAsync<PostnomicApiException>(() => _sut.UpdatePostAsync(postId, request));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("Post not found.", ex.Message);
    }

    // ── GetPostAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPostAsync_WhenApiReturnsOk_ReturnsPost()
    {
        // Arrange
        const string postId = "post-1";
        var post = CreatePost(publicId: postId);

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/blogs/{BlogId}/posts/{postId}")
            .Respond(HttpStatusCode.OK, JsonContent.Create(post));

        // Act
        var result = await _sut.GetPostAsync(postId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(postId, result!.PublicId);
    }

    [Fact]
    public async Task GetPostAsync_WhenApiReturns404_ReturnsNull()
    {
        // Arrange
        const string postId = "missing";

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/blogs/{BlogId}/posts/{postId}")
            .Respond(HttpStatusCode.NotFound);

        // Act
        var result = await _sut.GetPostAsync(postId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPostAsync_WhenApiReturnsForbidden_ThrowsPostnomicApiException()
    {
        // Arrange
        const string postId = "post-1";

        _mockHttp
            .When(HttpMethod.Get, $"{BaseUrl}/blogs/{BlogId}/posts/{postId}")
            .Respond(HttpStatusCode.Forbidden);

        // Act
        var ex = await Assert.ThrowsAsync<PostnomicApiException>(() => _sut.GetPostAsync(postId));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    // ── PublishPostAsync / UnpublishPostAsync / ArchivePostAsync ────────────────

    [Fact]
    public async Task PublishPostAsync_WhenApiReturnsOk_ReturnsPublishedPost()
    {
        // Arrange
        const string postId = "post-1";
        var published = CreatePost(publicId: postId, status: PostnomicPostStatus.Published);

        _mockHttp
            .Expect(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/posts/{postId}/publish")
            .Respond(HttpStatusCode.OK, JsonContent.Create(published));

        // Act
        var result = await _sut.PublishPostAsync(postId);

        // Assert
        Assert.Equal(PostnomicPostStatus.Published, result.Status);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task PublishPostAsync_WhenApiReturnsConflict_ThrowsWithReason()
    {
        // Arrange
        const string postId = "post-1";

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/posts/{postId}/publish")
            .Respond(HttpStatusCode.Conflict, new StringContent(
                "Cannot publish a post with status 'Published'. Post must be Draft, Scheduled, or InReview.",
                Encoding.UTF8, "text/plain"));

        // Act
        var ex = await Assert.ThrowsAsync<PostnomicApiException>(() => _sut.PublishPostAsync(postId));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Contains("Draft, Scheduled, or InReview", ex.Message);
    }

    [Fact]
    public async Task UnpublishPostAsync_WhenApiReturnsOk_ReturnsUnpublishedPost()
    {
        // Arrange
        const string postId = "post-1";
        var unpublished = CreatePost(publicId: postId, status: PostnomicPostStatus.Unpublished);

        _mockHttp
            .Expect(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/posts/{postId}/unpublish")
            .Respond(HttpStatusCode.OK, JsonContent.Create(unpublished));

        // Act
        var result = await _sut.UnpublishPostAsync(postId);

        // Assert
        Assert.Equal(PostnomicPostStatus.Unpublished, result.Status);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task ArchivePostAsync_WhenApiReturnsOk_ReturnsArchivedPost()
    {
        // Arrange
        const string postId = "post-1";
        var archived = CreatePost(publicId: postId, status: PostnomicPostStatus.Archived);

        _mockHttp
            .Expect(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/posts/{postId}/archive")
            .Respond(HttpStatusCode.OK, JsonContent.Create(archived));

        // Act
        var result = await _sut.ArchivePostAsync(postId);

        // Assert
        Assert.Equal(PostnomicPostStatus.Archived, result.Status);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    // ── UploadImageAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task UploadImageAsync_WhenApiReturnsOk_ReturnsFirstMediaItem()
    {
        // Arrange
        var items = new List<PostnomicMediaItem>
        {
            new()
            {
                Name = "cover.jpg",
                Path = "cover.jpg",
                Size = 1234,
                ContentType = "image/jpeg",
                Url = "/media/blob/blog-media/test-blog/cover.jpg"
            }
        };

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/media/upload")
            .Respond(HttpStatusCode.OK, JsonContent.Create(items));

        using var content = new MemoryStream([1, 2, 3, 4]);

        // Act
        var result = await _sut.UploadImageAsync(content, "cover.jpg", "image/jpeg");

        // Assert
        Assert.Equal("cover.jpg", result.Name);
        Assert.Equal("/media/blob/blog-media/test-blog/cover.jpg", result.Url);
    }

    [Fact]
    public async Task UploadImageAsync_WithPath_IncludesPathQueryParameter()
    {
        // Arrange
        var items = new List<PostnomicMediaItem> { new() { Name = "a.jpg", Path = "posts/2026-08/a.jpg" } };

        _mockHttp
            .Expect(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/media/upload")
            .WithQueryString("path", "posts/2026-08")
            .Respond(HttpStatusCode.OK, JsonContent.Create(items));

        using var content = new MemoryStream([1, 2, 3]);

        // Act
        await _sut.UploadImageAsync(content, "a.jpg", "image/jpeg", path: "posts/2026-08");

        // Assert
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task UploadImageAsync_WhenApiReturnsBadRequest_ThrowsWithReason()
    {
        // Arrange
        _mockHttp
            .When(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/media/upload")
            .Respond(HttpStatusCode.BadRequest, new StringContent(
                "File 'malware.exe' has a disallowed extension '.exe'.", Encoding.UTF8, "text/plain"));

        using var content = new MemoryStream([1, 2, 3]);

        // Act
        var ex = await Assert.ThrowsAsync<PostnomicApiException>(
            () => _sut.UploadImageAsync(content, "malware.exe", "application/octet-stream"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("disallowed extension", ex.Message);
    }

    [Fact]
    public async Task UploadImageAsync_WhenApiReturnsEmptyList_ThrowsPostnomicApiException()
    {
        // Arrange
        _mockHttp
            .When(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/media/upload")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<PostnomicMediaItem>()));

        using var content = new MemoryStream([1, 2, 3]);

        // Act
        var ex = await Assert.ThrowsAsync<PostnomicApiException>(
            () => _sut.UploadImageAsync(content, "a.jpg", "image/jpeg"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PostnomicPost CreatePost(
        string publicId = "post-1",
        string title = "Hello",
        string slug = "hello",
        PostnomicPostStatus status = PostnomicPostStatus.Draft) =>
        new()
        {
            PublicId = publicId,
            Title = title,
            Slug = slug,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            AuthorIdentityUserId = "auth0|abc123",
            PrimaryBlogPublicId = BlogId,
            CanonicalUrl = $"https://example.com/{slug}"
        };
}

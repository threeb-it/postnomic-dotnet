using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using RichardSzalay.MockHttp;

namespace Postnomic.Client.Tests;

/// <summary>
/// Blog management on <see cref="PostnomicAuthoringService"/> (create, read, rename) and the
/// authoring gaps closed alongside it: a post looked up by slug, a real delete, and
/// <c>publishedAt</c> on create and update.
/// </summary>
public class PostnomicAuthoringBlogManagementTests : IDisposable
{
    private const string BaseUrl = "https://api.postnomic.com";
    private const string BlogId = "3f2a1c9e-1111-2222-3333-444455556666";
    private const string OtherBlogId = "99999999-1111-2222-3333-444455556666";

    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly PostnomicAuthoringService _sut;

    public PostnomicAuthoringBlogManagementTests()
    {
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri(BaseUrl + "/");

        _sut = new PostnomicAuthoringService(httpClient, Options.Create(new PostnomicClientOptions
        {
            BaseUrl = BaseUrl,
            PersonalAccessToken = "pnp_test-token",
            BlogId = BlogId
        }));
    }

    public void Dispose() => _mockHttp.Dispose();

    private static string Body(HttpRequestMessage request) =>
        request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

    private const string BlogJson = """
        {"publicId":"3f2a1c9e-1111-2222-3333-444455556666","name":"BrickDb Blog","slug":"brickdb",
         "description":"Bricks","canonicalBaseUrl":"https://www.brickdb.de/blog","createdAt":"2026-01-02T03:04:05Z",
         "updatedAt":null,"defaultLayout":"Masonry","sidebarTagLimit":7,"defaultLanguage":"de","pendingSlug":null,
         "isFavorite":false,"commentRequireModeration":true}
        """;

    // ── GetBlogAsync / GetBlogsAsync ────────────────────────────────────────

    [Fact]
    public async Task GetBlogAsync_ReturnsTheConfiguredBlog_WithItsDefaultLanguage()
    {
        _mockHttp.Expect(HttpMethod.Get, $"{BaseUrl}/blogs/{BlogId}").Respond("application/json", BlogJson);

        var blog = await _sut.GetBlogAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(blog);
        Assert.Equal(BlogId, blog.PublicId);
        Assert.Equal("BrickDb Blog", blog.Name);
        Assert.Equal("brickdb", blog.Slug);
        Assert.Equal("de", blog.DefaultLanguage);
        Assert.Equal("https://www.brickdb.de/blog", blog.CanonicalBaseUrl);
        Assert.Equal("Masonry", blog.DefaultLayout);
        Assert.Equal(7, blog.SidebarTagLimit);
        Assert.Null(blog.PendingSlug);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetBlogAsync_WithAnExplicitBlogId_ReadsThatBlog()
    {
        _mockHttp.Expect(HttpMethod.Get, $"{BaseUrl}/blogs/{OtherBlogId}").Respond("application/json", BlogJson);

        await _sut.GetBlogAsync(OtherBlogId, TestContext.Current.CancellationToken);

        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetBlogAsync_WhenTheBlogDoesNotExist_ReturnsNull()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/blogs/{BlogId}").Respond(HttpStatusCode.NotFound);

        Assert.Null(await _sut.GetBlogAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetBlogAsync_ABlogFromAnApiThatPredatesDefaultLanguage_ReadsAsEnglish()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/blogs/{BlogId}")
            .Respond("application/json", """{"publicId":"x","name":"N","slug":"n","canonicalBaseUrl":"https://n"}""");

        var blog = await _sut.GetBlogAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("en", blog!.DefaultLanguage);
    }

    [Fact]
    public async Task GetBlogsAsync_ListsEveryBlogTheTokenOwnerIsAMemberOf()
    {
        _mockHttp.Expect(HttpMethod.Get, $"{BaseUrl}/blogs").Respond("application/json", $"[{BlogJson}]");

        var blogs = await _sut.GetBlogsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("de", Assert.Single(blogs).DefaultLanguage);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    // ── CreateBlogAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBlogAsync_PostsTheBlog_AndReturnsWhatWasCreated()
    {
        string? body = null;
        _mockHttp.Expect(HttpMethod.Post, $"{BaseUrl}/blogs")
            .With(request => { body = Body(request); return true; })
            .Respond(HttpStatusCode.Created, "application/json", BlogJson);

        var blog = await _sut.CreateBlogAsync(new PostnomicCreateBlogRequest
        {
            Name = "BrickDb Blog",
            Slug = "brickdb",
            CanonicalBaseUrl = "https://www.brickdb.de/blog",
            DefaultLanguage = "de"
        }, TestContext.Current.CancellationToken);

        Assert.Equal("brickdb", blog.Slug);
        using var sent = JsonDocument.Parse(body!);
        Assert.Equal("BrickDb Blog", sent.RootElement.GetProperty("name").GetString());
        Assert.Equal("brickdb", sent.RootElement.GetProperty("slug").GetString());
        Assert.Equal("https://www.brickdb.de/blog", sent.RootElement.GetProperty("canonicalBaseUrl").GetString());
        Assert.Equal("de", sent.RootElement.GetProperty("defaultLanguage").GetString());
        Assert.Equal("Default", sent.RootElement.GetProperty("defaultLayout").GetString());
        Assert.Equal(15, sent.RootElement.GetProperty("sidebarTagLimit").GetInt32());
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "Blog limit reached")]
    [InlineData(HttpStatusCode.Conflict, "A blog with this slug already exists.")]
    public async Task CreateBlogAsync_SurfacesThePlanLimitAndATakenSlug(HttpStatusCode status, string reason)
    {
        _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/blogs").Respond(status, "text/plain", reason);

        var ex = await Assert.ThrowsAsync<PostnomicApiException>(() => _sut.CreateBlogAsync(
            new PostnomicCreateBlogRequest { Name = "N", Slug = "n", CanonicalBaseUrl = "https://n" },
            TestContext.Current.CancellationToken));

        Assert.Equal(status, ex.StatusCode);
        Assert.Contains(reason, ex.Message);
    }

    // ── RenameBlogAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RenameBlogAsync_PostsToTheRenameEndpoint_AndReturnsWhatTheRenameDid()
    {
        string? body = null;
        _mockHttp.Expect(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/rename")
            .With(request => { body = Body(request); return true; })
            .Respond("application/json", $$"""
                {"blog":{{BlogJson}},"previousSlug":"thimobaut","formerSlugExpiresAt":"2027-01-08T12:00:00Z",
                 "mediaFiles":42,"mediaBytes":123456,"postsRewritten":17,"translationsRewritten":9}
                """);

        var result = await _sut.RenameBlogAsync(
            new PostnomicRenameBlogRequest { Name = "BrickDb Blog", Slug = "brickdb" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("brickdb", result.Blog.Slug);
        Assert.Equal("thimobaut", result.PreviousSlug);
        Assert.Equal(new DateTime(2027, 1, 8, 12, 0, 0, DateTimeKind.Utc), result.FormerSlugExpiresAt);
        Assert.Equal(42, result.MediaFiles);
        Assert.Equal(123456, result.MediaBytes);
        Assert.Equal(17, result.PostsRewritten);
        Assert.Equal(9, result.TranslationsRewritten);
        using var sent = JsonDocument.Parse(body!);
        Assert.Equal("BrickDb Blog", sent.RootElement.GetProperty("name").GetString());
        Assert.Equal("brickdb", sent.RootElement.GetProperty("slug").GetString());
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task RenameBlogAsync_WithAnExplicitBlogId_RenamesThatBlog()
    {
        _mockHttp.Expect(HttpMethod.Post, $"{BaseUrl}/blogs/{OtherBlogId}/rename")
            .Respond("application/json", $$"""{"blog":{{BlogJson}}}""");

        var result = await _sut.RenameBlogAsync(new PostnomicRenameBlogRequest { Name = "Only The Name" }, OtherBlogId, TestContext.Current.CancellationToken);

        Assert.Null(result.PreviousSlug);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task RenameBlogAsync_WithNeitherNameNorSlug_ThrowsBeforeCallingTheApi()
    {
        var route = _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/rename").Respond("application/json", "{}");

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.RenameBlogAsync(
            new PostnomicRenameBlogRequest { Name = " " }, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(0, _mockHttp.GetMatchCount(route));
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "Another blog has this slug, or has it reserved.")]
    [InlineData(HttpStatusCode.BadRequest, "A blog slug must be 1-200 characters")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "2 of 40 media file(s) did not arrive intact")]
    public async Task RenameBlogAsync_SurfacesARefusal_WithItsStatusAndReason(HttpStatusCode status, string reason)
    {
        _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/rename").Respond(status, "text/plain", reason);

        var ex = await Assert.ThrowsAsync<PostnomicApiException>(() => _sut.RenameBlogAsync(
            new PostnomicRenameBlogRequest { Slug = "brickdb" }, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(status, ex.StatusCode);
        Assert.Contains(reason, ex.Message);
    }

    // ── GetPostBySlugAsync ──────────────────────────────────────────────────

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public async Task GetPostBySlugAsync_ReadsThePost_IncludingUnpublishedByDefault(bool includeUnpublished, string flag)
    {
        _mockHttp.Expect(HttpMethod.Get, $"{BaseUrl}/blogs/{BlogId}/posts/by-slug/hello-world?includeUnpublished={flag}")
            .Respond("application/json", """
                {"publicId":"post-1","title":"Hello","slug":"hello-world","status":"Draft","createdAt":"2026-01-02T03:04:05Z",
                 "authorIdentityUserId":"auth0|a","primaryBlogPublicId":"b","canonicalUrl":"https://x/hello-world"}
                """);

        var post = includeUnpublished
            ? await _sut.GetPostBySlugAsync("hello-world", cancellationToken: TestContext.Current.CancellationToken)
            : await _sut.GetPostBySlugAsync("hello-world", includeUnpublished: false, TestContext.Current.CancellationToken);

        Assert.Equal("post-1", post!.PublicId);
        Assert.Equal(PostnomicPostStatus.Draft, post.Status);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPostBySlugAsync_EscapesTheSlug()
    {
        _mockHttp.Expect(HttpMethod.Get, $"{BaseUrl}/blogs/{BlogId}/posts/by-slug/a%2Fb%3Fc?includeUnpublished=true")
            .Respond(HttpStatusCode.NotFound);

        await _sut.GetPostBySlugAsync("a/b?c", cancellationToken: TestContext.Current.CancellationToken);

        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPostBySlugAsync_WhenThereIsNoSuchPost_ReturnsNull()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/blogs/{BlogId}/posts/by-slug/nope").Respond(HttpStatusCode.NotFound);

        Assert.Null(await _sut.GetPostBySlugAsync("nope", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPostBySlugAsync_WhenTheRoleIsTooLow_Throws()
    {
        _mockHttp.When(HttpMethod.Get, $"{BaseUrl}/blogs/{BlogId}/posts/by-slug/hello").Respond(HttpStatusCode.Forbidden);

        var ex = await Assert.ThrowsAsync<PostnomicApiException>(() =>
            _sut.GetPostBySlugAsync("hello", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    // ── DeletePostAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePostAsync_SendsADelete_NotAnArchive()
    {
        _mockHttp.Expect(HttpMethod.Delete, $"{BaseUrl}/blogs/{BlogId}/posts/post-1").Respond(HttpStatusCode.NoContent);
        var archive = _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/posts/post-1/archive").Respond("application/json", "{}");

        await _sut.DeletePostAsync("post-1", TestContext.Current.CancellationToken);

        _mockHttp.VerifyNoOutstandingExpectation();
        Assert.Equal(0, _mockHttp.GetMatchCount(archive));
    }

    [Fact]
    public async Task DeletePostAsync_WhenThePostDoesNotExist_Throws()
    {
        _mockHttp.When(HttpMethod.Delete, $"{BaseUrl}/blogs/{BlogId}/posts/missing")
            .Respond(HttpStatusCode.NotFound, "text/plain", "Post not found.");

        var ex = await Assert.ThrowsAsync<PostnomicApiException>(() =>
            _sut.DeletePostAsync("missing", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    // ── publishedAt on create and update ────────────────────────────────────

    private const string PublishedPostJson = """
        {"publicId":"post-1","title":"Hello","slug":"hello","status":"Published","publishedAt":"2024-03-09T08:15:00Z",
         "createdAt":"2026-01-02T03:04:05Z","authorIdentityUserId":"auth0|a","primaryBlogPublicId":"b","canonicalUrl":"https://x/hello"}
        """;

    [Fact]
    public async Task CreatePostAsync_WithPublishedAt_SendsIt_AndDoesNotPublishASecondTime()
    {
        string? body = null;
        var publishedAt = new DateTime(2024, 3, 9, 8, 15, 0, DateTimeKind.Utc);
        _mockHttp.Expect(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/posts")
            .With(request => { body = Body(request); return true; })
            .Respond(HttpStatusCode.Created, "application/json", PublishedPostJson);
        var publish = _mockHttp.When(HttpMethod.Post, $"{BaseUrl}/blogs/{BlogId}/posts/post-1/publish").Respond("application/json", PublishedPostJson);

        var post = await _sut.CreatePostAsync(
            new PostnomicCreatePostRequest { Title = "Hello", Slug = "hello", PublishedAt = publishedAt, PublishImmediately = true },
            TestContext.Current.CancellationToken);

        Assert.Equal(PostnomicPostStatus.Published, post.Status);
        Assert.Equal(publishedAt, post.PublishedAt);
        using var sent = JsonDocument.Parse(body!);
        Assert.Equal(publishedAt, sent.RootElement.GetProperty("publishedAt").GetDateTime());
        Assert.Equal(0, _mockHttp.GetMatchCount(publish));
    }

    [Fact]
    public async Task UpdatePostAsync_WithPublishedAt_SendsIt()
    {
        string? body = null;
        var publishedAt = new DateTime(2024, 3, 9, 8, 15, 0, DateTimeKind.Utc);
        _mockHttp.Expect(HttpMethod.Put, $"{BaseUrl}/blogs/{BlogId}/posts/post-1")
            .With(request => { body = Body(request); return true; })
            .Respond("application/json", PublishedPostJson);

        await _sut.UpdatePostAsync("post-1",
            new PostnomicUpdatePostRequest { Title = "Hello", Slug = "hello", PublishedAt = publishedAt },
            TestContext.Current.CancellationToken);

        using var sent = JsonDocument.Parse(body!);
        Assert.Equal(publishedAt, sent.RootElement.GetProperty("publishedAt").GetDateTime());
    }

    [Fact]
    public async Task UpdatePostAsync_WithoutPublishedAt_SendsNull_SoTheStoredDateIsLeftAlone()
    {
        string? body = null;
        _mockHttp.Expect(HttpMethod.Put, $"{BaseUrl}/blogs/{BlogId}/posts/post-1")
            .With(request => { body = Body(request); return true; })
            .Respond("application/json", PublishedPostJson);

        await _sut.UpdatePostAsync("post-1",
            new PostnomicUpdatePostRequest { Title = "Hello", Slug = "hello" },
            TestContext.Current.CancellationToken);

        using var sent = JsonDocument.Parse(body!);
        Assert.Equal(JsonValueKind.Null, sent.RootElement.GetProperty("publishedAt").ValueKind);
    }
}

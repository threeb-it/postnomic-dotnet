using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.Tests;

/// <summary>
/// Every JSON-reading method of <see cref="PostnomicBlogService"/> and
/// <see cref="PostnomicAuthoringService"/> must turn a success status with an unusable body into a
/// <see cref="PostnomicUpstreamException"/> — never a bare <see cref="JsonException"/>
/// (threeb-it/postnomic-dotnet#16). The method lists are theories, and
/// <see cref="EveryReadingMethodIsCovered"/> fails when a new interface method that returns data is
/// added without being added here, so a future method cannot silently bypass the shared read helper.
/// </summary>
public class UpstreamFailureTests
{
    private const string BaseUrl = "https://api.postnomic.com";
    private const string BlogSlug = "test-blog";
    private const string BlogId = "3f2a1c9e-1111-2222-3333-444455556666";

    // ── The call sites ───────────────────────────────────────────────────────

    private static readonly Dictionary<string, Func<IPostnomicBlogService, CancellationToken, Task>> BlogReads = new()
    {
        [nameof(IPostnomicBlogService.GetBlogAsync)] = (s, ct) => s.GetBlogAsync(ct),
        [nameof(IPostnomicBlogService.GetTagsAsync)] = (s, ct) => s.GetTagsAsync(ct),
        [nameof(IPostnomicBlogService.GetCategoriesAsync)] = (s, ct) => s.GetCategoriesAsync(ct),
        [nameof(IPostnomicBlogService.GetAuthorsAsync)] = (s, ct) => s.GetAuthorsAsync(ct),
        [nameof(IPostnomicBlogService.GetAuthorProfileAsync)] = (s, ct) => s.GetAuthorProfileAsync("jane", ct),
        [nameof(IPostnomicBlogService.GetPostsAsync)] = (s, ct) => s.GetPostsAsync(search: "secret", cancellationToken: ct),
        [nameof(IPostnomicBlogService.GetPostAsync)] = (s, ct) => s.GetPostAsync("hello", cancellationToken: ct),
        [nameof(IPostnomicBlogService.CreateCommentAsync)] = (s, ct) =>
            s.CreateCommentAsync("hello", new PostnomicCreateCommentRequest { Body = "Hi" }, ct),
        [nameof(IPostnomicBlogService.GetTopCommentedPostsAsync)] = (s, ct) => s.GetTopCommentedPostsAsync(3, ct),
        [nameof(IPostnomicBlogService.GetMostReadPostsAsync)] = (s, ct) => s.GetMostReadPostsAsync(3, ct),
    };

    private static readonly Dictionary<string, Func<IPostnomicAuthoringService, CancellationToken, Task>> AuthoringReads = new()
    {
        [nameof(IPostnomicAuthoringService.CreatePostAsync)] = (s, ct) =>
            s.CreatePostAsync(new PostnomicCreatePostRequest { Title = "T", Slug = "t" }, ct),
        [nameof(IPostnomicAuthoringService.UpdatePostAsync)] = (s, ct) =>
            s.UpdatePostAsync("p1", new PostnomicUpdatePostRequest { Title = "T", Slug = "t" }, ct),
        [nameof(IPostnomicAuthoringService.GetPostAsync)] = (s, ct) => s.GetPostAsync("p1", ct),
        [nameof(IPostnomicAuthoringService.PublishPostAsync)] = (s, ct) => s.PublishPostAsync("p1", ct),
        [nameof(IPostnomicAuthoringService.UnpublishPostAsync)] = (s, ct) => s.UnpublishPostAsync("p1", ct),
        [nameof(IPostnomicAuthoringService.ArchivePostAsync)] = (s, ct) => s.ArchivePostAsync("p1", ct),
        [nameof(IPostnomicAuthoringService.UploadImageAsync)] = (s, ct) =>
            s.UploadImageAsync(new MemoryStream([1, 2, 3]), "a.png", "image/png", cancellationToken: ct),
        [nameof(IPostnomicAuthoringService.GetPostTranslationsAsync)] = (s, ct) => s.GetPostTranslationsAsync("p1", ct),
        [nameof(IPostnomicAuthoringService.SetPostTranslationAsync)] = (s, ct) =>
            s.SetPostTranslationAsync("p1", "de", new PostnomicUpsertTranslationRequest { Title = "T", Slug = "t" }, ct),
    };

    /// <summary>The unusable success responses, by name.</summary>
    private static readonly Dictionary<string, (Func<HttpResponseMessage> Response, PostnomicUpstreamFailure Failure)> BadBodies = new()
    {
        ["200 + empty body"] = (() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) }, PostnomicUpstreamFailure.EmptyBody),
        ["204 No Content"] = (() => new HttpResponseMessage(HttpStatusCode.NoContent), PostnomicUpstreamFailure.EmptyBody),
        ["200 + whitespace"] = (() => new HttpResponseMessage(HttpStatusCode.OK) { Content = Json("   \r\n\t ") }, PostnomicUpstreamFailure.EmptyBody),
        ["200 + empty stream, unknown length"] = (() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new NonSeekableEmptyStream()) }, PostnomicUpstreamFailure.EmptyBody),
        ["200 + malformed JSON"] = (() => new HttpResponseMessage(HttpStatusCode.OK) { Content = Json("{\"items\": [") }, PostnomicUpstreamFailure.MalformedBody),
    };

    public static TheoryData<string, string> BlogReadCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var method in BlogReads.Keys)
            foreach (var body in BadBodies.Keys)
                data.Add(method, body);
        return data;
    }

    public static TheoryData<string, string> AuthoringReadCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var method in AuthoringReads.Keys)
            foreach (var body in BadBodies.Keys)
                data.Add(method, body);
        return data;
    }

    public static TheoryData<string> BlogReadMethods() => new(BlogReads.Keys);

    // ── Guard: no method may slip past the theories ──────────────────────────

    [Fact]
    public void EveryReadingMethodIsCovered()
    {
        static IEnumerable<string> Reading(Type t) => t.GetMethods()
            .Where(m => m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            .Select(m => m.Name);

        Assert.Equal(
            Reading(typeof(IPostnomicBlogService)).Order(),
            BlogReads.Keys.Order());
        Assert.Equal(
            Reading(typeof(IPostnomicAuthoringService)).Order(),
            AuthoringReads.Keys.Order());
    }

    // ── Unusable success body → PostnomicUpstreamException ───────────────────

    [Theory]
    [MemberData(nameof(BlogReadCases))]
    public async Task BlogService_UnusableSuccessBody_ThrowsUpstreamException(string method, string body)
    {
        var (respond, failure) = BadBodies[body];
        var sut = CreateBlogService(_ => respond());

        var ex = await Assert.ThrowsAsync<PostnomicUpstreamException>(
            () => BlogReads[method](sut, TestContext.Current.CancellationToken));

        AssertUpstream(ex, failure, respond().StatusCode, $"/public/blogs/{BlogSlug}");
    }

    [Theory]
    [MemberData(nameof(AuthoringReadCases))]
    public async Task AuthoringService_UnusableSuccessBody_ThrowsUpstreamException(string method, string body)
    {
        var (respond, failure) = BadBodies[body];
        var sut = CreateAuthoringService(_ => respond());

        var ex = await Assert.ThrowsAsync<PostnomicUpstreamException>(
            () => AuthoringReads[method](sut, TestContext.Current.CancellationToken));

        AssertUpstream(ex, failure, respond().StatusCode, $"/blogs/{BlogId}/");
    }

    [Fact]
    public async Task UpstreamException_IsCatchableAsHttpRequestException()
    {
        var sut = CreateBlogService(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) });

        var ex = await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => sut.GetPostsAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.IsType<PostnomicUpstreamException>(ex);
        Assert.Equal(HttpRequestError.InvalidResponse, ex.HttpRequestError);
    }

    [Fact]
    public async Task UpstreamException_PathOmitsTheQueryString()
    {
        var sut = CreateBlogService(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) });

        var ex = await Assert.ThrowsAsync<PostnomicUpstreamException>(
            () => sut.GetPostsAsync(search: "secret", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal($"/public/blogs/{BlogSlug}/posts", ex.RequestPath);
        Assert.DoesNotContain("secret", ex.Message);
    }

    // ── Existing semantics are untouched ─────────────────────────────────────

    [Theory]
    [InlineData(nameof(IPostnomicBlogService.GetBlogAsync))]
    [InlineData(nameof(IPostnomicBlogService.GetAuthorProfileAsync))]
    [InlineData(nameof(IPostnomicBlogService.GetPostAsync))]
    [InlineData(nameof(IPostnomicBlogService.CreateCommentAsync))]
    public async Task BlogService_404OnNullableRead_StillReturnsNull(string method)
    {
        var sut = CreateBlogService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var task = (Task)BlogReads[method](sut, TestContext.Current.CancellationToken);
        await task;

        var result = task.GetType().GetProperty("Result")!.GetValue(task);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(nameof(IPostnomicBlogService.GetTagsAsync), HttpStatusCode.NotFound)]
    [InlineData(nameof(IPostnomicBlogService.GetCategoriesAsync), HttpStatusCode.NotFound)]
    [InlineData(nameof(IPostnomicBlogService.GetAuthorsAsync), HttpStatusCode.NotFound)]
    [InlineData(nameof(IPostnomicBlogService.GetPostsAsync), HttpStatusCode.NotFound)]
    [InlineData(nameof(IPostnomicBlogService.GetTopCommentedPostsAsync), HttpStatusCode.NotFound)]
    [InlineData(nameof(IPostnomicBlogService.GetMostReadPostsAsync), HttpStatusCode.NotFound)]
    [InlineData(nameof(IPostnomicBlogService.GetBlogAsync), HttpStatusCode.InternalServerError)]
    [InlineData(nameof(IPostnomicBlogService.GetPostsAsync), HttpStatusCode.ServiceUnavailable)]
    [InlineData(nameof(IPostnomicBlogService.GetPostAsync), HttpStatusCode.BadGateway)]
    [InlineData(nameof(IPostnomicBlogService.GetAuthorProfileAsync), HttpStatusCode.InternalServerError)]
    public async Task BlogService_NonSuccessStatus_StillThrowsPlainHttpRequestException(string method, HttpStatusCode status)
    {
        var sut = CreateBlogService(_ => new HttpResponseMessage(status));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => BlogReads[method](sut, TestContext.Current.CancellationToken));

        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task BlogService_CreateCommentRejected_StillReturnsNull()
    {
        var sut = CreateBlogService(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        var result = await sut.CreateCommentAsync(
            "hello", new PostnomicCreateCommentRequest { Body = "Hi" }, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task AuthoringService_404OnGetPost_StillReturnsNull()
    {
        var sut = CreateAuthoringService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Null(await sut.GetPostAsync("p1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AuthoringService_NonSuccessStatus_StillThrowsPostnomicApiException()
    {
        var sut = CreateAuthoringService(_ => new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("Already published") });

        var ex = await Assert.ThrowsAsync<PostnomicApiException>(
            () => sut.PublishPostAsync("p1", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal("Already published", ex.Message);
    }

    [Fact]
    public async Task AuthoringService_JsonNullBody_StillThrowsPostnomicApiException()
    {
        var sut = CreateAuthoringService(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = Json("null") });

        await Assert.ThrowsAsync<PostnomicApiException>(
            () => sut.PublishPostAsync("p1", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(nameof(IPostnomicBlogService.GetTagsAsync))]
    [InlineData(nameof(IPostnomicBlogService.GetCategoriesAsync))]
    [InlineData(nameof(IPostnomicBlogService.GetAuthorsAsync))]
    [InlineData(nameof(IPostnomicBlogService.GetTopCommentedPostsAsync))]
    [InlineData(nameof(IPostnomicBlogService.GetMostReadPostsAsync))]
    public async Task BlogService_ValidJsonList_StillDeserializes(string method)
    {
        // A list whose items carry every name-ish property the list models use; unknown
        // properties are ignored, so one payload serves all list endpoints.
        const string payload = """[{"name":"One","slug":"one","title":"One","postCount":1}]""";
        var sut = CreateBlogService(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(payload) });

        var task = (Task)BlogReads[method](sut, TestContext.Current.CancellationToken);
        await task;

        var result = (System.Collections.ICollection)task.GetType().GetProperty("Result")!.GetValue(task)!;
        Assert.Single(result);
    }

    [Fact]
    public async Task BlogService_ValidJsonPosts_StillDeserializes()
    {
        const string payload = """{"items":[{"title":"Hello","slug":"hello","authorName":"Jane"}],"page":1,"pageSize":5,"totalCount":1,"totalPages":1}""";
        var sut = CreateBlogService(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(payload) });

        var result = await sut.GetPostsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Hello", Assert.Single(result.Items).Title);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task BlogService_ValidJsonPost_StillDeserializes()
    {
        const string payload = """{"title":"Hello","slug":"hello","authorName":"Jane","content":"<p>Hi</p>"}""";
        var sut = CreateBlogService(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(payload) });

        var result = await sut.GetPostAsync("hello", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Hello", result!.Title);
    }

    [Fact]
    public async Task BlogService_JsonNullList_StillFallsBackToEmpty()
    {
        var sut = CreateBlogService(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = Json("null") });

        Assert.Empty(await sut.GetTagsAsync(TestContext.Current.CancellationToken));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void AssertUpstream(
        PostnomicUpstreamException ex, PostnomicUpstreamFailure failure, HttpStatusCode status, string pathPrefix)
    {
        Assert.Equal(failure, ex.Failure);
        Assert.Equal(status, ex.StatusCode);
        Assert.NotNull(ex.RequestPath);
        Assert.StartsWith(pathPrefix, ex.RequestPath);
        Assert.DoesNotContain("?", ex.RequestPath);
        Assert.Contains(ex.RequestPath!, ex.Message);
        Assert.Contains(((int)status).ToString(), ex.Message);

        if (failure == PostnomicUpstreamFailure.MalformedBody)
            Assert.IsAssignableFrom<JsonException>(ex.InnerException);
        else
            Assert.Null(ex.InnerException);
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static PostnomicBlogService CreateBlogService(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(Client(respond), Options.Create(new PostnomicClientOptions
        {
            BaseUrl = BaseUrl,
            ApiKey = "test-key",
            BlogSlug = BlogSlug
        }));

    private static PostnomicAuthoringService CreateAuthoringService(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(Client(respond), Options.Create(new PostnomicClientOptions
        {
            BaseUrl = BaseUrl,
            PersonalAccessToken = "pnp_test-token",
            BlogId = BlogId
        }));

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new StubHandler(respond)) { BaseAddress = new Uri(BaseUrl + "/") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = respond(request);
            return Task.FromResult(response);
        }
    }

    /// <summary>An empty stream that cannot report its length, so no Content-Length is known.</summary>
    private sealed class NonSeekableEmptyStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

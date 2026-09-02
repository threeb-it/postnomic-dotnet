using System.Net;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client.Tests;

/// <summary>
/// Unit tests for <see cref="PostnomicPersonalAccessTokenHandler"/>.
/// Verifies that the delegating handler injects the <c>Authorization: Bearer</c> header when a
/// Personal Access Token is configured, and omits it when the token is absent or whitespace-only.
/// </summary>
public class PostnomicPersonalAccessTokenHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="HttpClient"/> whose pipeline is:
    ///   PostnomicPersonalAccessTokenHandler → CapturingHandler → returns 200 OK.
    /// The <paramref name="getCapturedRequest"/> out parameter is set once a request is sent.
    /// </summary>
    private static HttpClient BuildClient(
        string? token,
        out Func<HttpRequestMessage?> getCapturedRequest)
    {
        HttpRequestMessage? captured = null;

        var innerHandler = new CapturingHandler(req =>
        {
            captured = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var options = Options.Create(new PostnomicClientOptions
        {
            BaseUrl = "https://api.example.com",
            PersonalAccessToken = token,
            BlogId = "blog-id"
        });

        var handler = new PostnomicPersonalAccessTokenHandler(options)
        {
            InnerHandler = innerHandler
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com/")
        };

        getCapturedRequest = () => captured;
        return client;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_WhenTokenIsConfigured_AddsBearerAuthorizationHeader()
    {
        // Arrange
        var client = BuildClient("pnp_my-secret-token", out var getCaptured);

        // Act
        await client.GetAsync("/test", TestContext.Current.CancellationToken);

        // Assert
        var request = getCaptured();
        Assert.NotNull(request);
        Assert.NotNull(request!.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("pnp_my-secret-token", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task SendAsync_WhenTokenIsNull_DoesNotAddAuthorizationHeader()
    {
        // Arrange
        var client = BuildClient(null, out var getCaptured);

        // Act
        await client.GetAsync("/test", TestContext.Current.CancellationToken);

        // Assert
        var request = getCaptured();
        Assert.NotNull(request);
        Assert.Null(request!.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_WhenTokenIsWhitespaceOnly_DoesNotAddAuthorizationHeader()
    {
        // Arrange
        var client = BuildClient("   ", out var getCaptured);

        // Act
        await client.GetAsync("/test", TestContext.Current.CancellationToken);

        // Assert
        var request = getCaptured();
        Assert.NotNull(request);
        Assert.Null(request!.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_WithToken_DoesNotBlockOtherHeaders()
    {
        // Arrange
        var client = BuildClient("pnp_abc", out var getCaptured);

        // Act
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/test");
        requestMessage.Headers.Add("X-Custom-Header", "custom-value");
        await client.SendAsync(requestMessage, TestContext.Current.CancellationToken);

        // Assert
        var captured = getCaptured();
        Assert.NotNull(captured!.Headers.Authorization);
        Assert.True(captured.Headers.TryGetValues("X-Custom-Header", out var custom));
        var value = Assert.Single(custom);
        Assert.Equal("custom-value", value);
    }

    [Fact]
    public async Task SendAsync_MultipleRequests_AddsHeaderToEachRequest()
    {
        // Arrange
        var capturedTokens = new List<string?>();

        var innerHandler = new CapturingHandler(req =>
        {
            capturedTokens.Add(req.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var options = Options.Create(new PostnomicClientOptions { PersonalAccessToken = "pnp_repeated" });
        var handler = new PostnomicPersonalAccessTokenHandler(options) { InnerHandler = innerHandler };
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };

        // Act
        await client.GetAsync("/one", TestContext.Current.CancellationToken);
        await client.GetAsync("/two", TestContext.Current.CancellationToken);
        await client.GetAsync("/three", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, capturedTokens.Count);
        Assert.All(capturedTokens, t => Assert.Equal("pnp_repeated", t));
    }

    // ── CapturingHandler ──────────────────────────────────────────────────────

    private sealed class CapturingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
    }
}

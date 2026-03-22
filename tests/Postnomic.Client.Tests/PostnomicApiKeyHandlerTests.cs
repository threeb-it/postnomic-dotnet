using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client.Tests;

/// <summary>
/// Unit tests for <see cref="PostnomicApiKeyHandler"/>.
/// Verifies that the delegating handler injects the <c>X-Api-Key</c> header when an API key is
/// configured, and omits the header when the key is absent or whitespace-only.
/// </summary>
public class PostnomicApiKeyHandlerTests
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="HttpClient"/> whose pipeline is:
    ///   PostnomicApiKeyHandler → CapturingHandler → returns 200 OK.
    /// The <paramref name="capturedRequest"/> out parameter is set once a request is sent.
    /// </summary>
    private static HttpClient BuildClient(
        string apiKey,
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
            ApiKey = apiKey,
            BlogSlug = "blog"
        });

        var handler = new PostnomicApiKeyHandler(options)
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
    public async Task SendAsync_WhenApiKeyIsConfigured_AddsXApiKeyHeader()
    {
        // Arrange
        var client = BuildClient("my-secret-key", out var getCaptured);

        // Act
        await client.GetAsync("/test");

        // Assert
        var request = getCaptured();
        request.Should().NotBeNull();
        request!.Headers.TryGetValues(ApiKeyHeaderName, out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be("my-secret-key");
    }

    [Fact]
    public async Task SendAsync_WhenApiKeyIsEmpty_DoesNotAddXApiKeyHeader()
    {
        // Arrange
        var client = BuildClient(string.Empty, out var getCaptured);

        // Act
        await client.GetAsync("/test");

        // Assert
        var request = getCaptured();
        request.Should().NotBeNull();
        request!.Headers.Contains(ApiKeyHeaderName).Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_WhenApiKeyIsWhitespaceOnly_DoesNotAddXApiKeyHeader()
    {
        // Arrange
        var client = BuildClient("   ", out var getCaptured);

        // Act
        await client.GetAsync("/test");

        // Assert
        var request = getCaptured();
        request.Should().NotBeNull();
        request!.Headers.Contains(ApiKeyHeaderName).Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_WithApiKey_DoesNotBlockOtherHeaders()
    {
        // Arrange
        var client = BuildClient("key-abc", out var getCaptured);

        // Act
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/test");
        requestMessage.Headers.Add("X-Custom-Header", "custom-value");
        await client.SendAsync(requestMessage);

        // Assert
        var captured = getCaptured();
        captured!.Headers.TryGetValues(ApiKeyHeaderName, out _).Should().BeTrue();
        captured.Headers.TryGetValues("X-Custom-Header", out var custom).Should().BeTrue();
        custom.Should().ContainSingle().Which.Should().Be("custom-value");
    }

    [Fact]
    public async Task SendAsync_MultipleRequests_AddsHeaderToEachRequest()
    {
        // Arrange
        var capturedHeaders = new List<string>();

        var innerHandler = new CapturingHandler(req =>
        {
            if (req.Headers.TryGetValues(ApiKeyHeaderName, out var vals))
                capturedHeaders.AddRange(vals);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var options = Options.Create(new PostnomicClientOptions { ApiKey = "repeated-key" });
        var handler = new PostnomicApiKeyHandler(options) { InnerHandler = innerHandler };
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };

        // Act
        await client.GetAsync("/one");
        await client.GetAsync("/two");
        await client.GetAsync("/three");

        // Assert
        capturedHeaders.Should().HaveCount(3);
        capturedHeaders.Should().AllBe("repeated-key");
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

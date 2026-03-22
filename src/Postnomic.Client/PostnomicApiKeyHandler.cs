using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client;

/// <summary>
/// A <see cref="DelegatingHandler"/> that injects the Postnomic API key into every outgoing
/// HTTP request as the <c>X-Api-Key</c> header. Register this handler via
/// <c>services.AddTransient&lt;PostnomicApiKeyHandler&gt;()</c> and attach it to the typed
/// <see cref="HttpClient"/> used by <see cref="PostnomicBlogService"/>.
/// </summary>
public class PostnomicApiKeyHandler(IOptions<PostnomicClientOptions> options) : DelegatingHandler
{
    private readonly PostnomicClientOptions _options = options.Value;

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", _options.ApiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

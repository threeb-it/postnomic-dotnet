using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client;

/// <summary>
/// A <see cref="DelegatingHandler"/> that injects the configured Personal Access Token into
/// every outgoing HTTP request as an <c>Authorization: Bearer</c> header. Register this handler
/// via <c>services.AddTransient&lt;PostnomicPersonalAccessTokenHandler&gt;()</c> and attach it
/// to the typed <see cref="HttpClient"/> used by <see cref="PostnomicAuthoringService"/>.
/// </summary>
public class PostnomicPersonalAccessTokenHandler(IOptions<PostnomicClientOptions> options) : DelegatingHandler
{
    private readonly PostnomicClientOptions _options = options.Value;

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.PersonalAccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.PersonalAccessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

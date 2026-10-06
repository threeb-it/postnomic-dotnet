using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client;

/// <summary>
/// The single path by which the client services turn a <b>successful</b> HTTP response into a
/// deserialized result. Status-code handling (404 → <see langword="null"/>,
/// <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>, <see cref="PostnomicApiException"/>)
/// stays at the call sites, because it differs per method; what happens to the body once the status
/// is a success is the same everywhere and lives here.
/// </summary>
internal static class PostnomicResponseReader
{
    /// <summary>
    /// Deserializes the JSON body of a successful <paramref name="response"/>.
    /// </summary>
    /// <param name="response">A response whose status code the caller has already checked.</param>
    /// <param name="requestPath">
    /// The relative request URI as the caller sent it. Only its path is reported; the query string
    /// is dropped so search terms never reach exception messages or logs.
    /// </param>
    /// <param name="cancellationToken">Cancels reading the body.</param>
    /// <returns>
    /// The deserialized value, or <see langword="null"/> when the body is the JSON literal
    /// <c>null</c> (unchanged from <see cref="HttpContentJsonExtensions.ReadFromJsonAsync{T}(HttpContent, CancellationToken)"/>).
    /// </returns>
    /// <exception cref="PostnomicUpstreamException">
    /// The body is empty (<c>204</c>, <c>Content-Length: 0</c>, zero bytes or whitespace only) —
    /// <see cref="PostnomicUpstreamFailure.EmptyBody"/> — or is not valid JSON for
    /// <typeparamref name="T"/> — <see cref="PostnomicUpstreamFailure.MalformedBody"/>, with the
    /// <see cref="JsonException"/> as the inner exception.
    /// </exception>
    public static async Task<T?> ReadJsonAsync<T>(
        HttpResponseMessage response,
        string requestPath,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
            throw Upstream(PostnomicUpstreamFailure.EmptyBody, response, requestPath);

        // The default HttpCompletionOption already buffers the body, so this is a no-op in practice;
        // it guarantees the content can be inspected and then read again by the JSON reader.
        await response.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (IsBlank(bytes))
            throw Upstream(PostnomicUpstreamFailure.EmptyBody, response, requestPath);

        try
        {
            // Deliberately the same reader the services used before, so charset handling and the
            // web serializer defaults are unchanged for every valid body.
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw Upstream(PostnomicUpstreamFailure.MalformedBody, response, requestPath, ex);
        }
    }

    /// <summary>
    /// Strips the query string from a relative request URI, leaving a rooted path.
    /// </summary>
    internal static string PathOf(string requestUri)
    {
        var queryStart = requestUri.IndexOf('?');
        var path = queryStart < 0 ? requestUri : requestUri[..queryStart];
        return path.StartsWith('/') ? path : "/" + path;
    }

    private static PostnomicUpstreamException Upstream(
        PostnomicUpstreamFailure failure,
        HttpResponseMessage response,
        string requestPath,
        Exception? inner = null) =>
        new(failure, response.StatusCode, PathOf(requestPath), inner);

    /// <summary>
    /// <see langword="true"/> when the body has no JSON token at all: zero bytes, or only JSON
    /// whitespace (space, tab, CR, LF), optionally after a UTF-8 byte order mark.
    /// </summary>
    private static bool IsBlank(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
            bytes = bytes[3..];

        foreach (var b in bytes)
        {
            if (b is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
                return false;
        }

        return true;
    }
}

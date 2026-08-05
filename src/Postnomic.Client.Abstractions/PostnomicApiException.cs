using System.Net;

namespace Postnomic.Client.Abstractions;

/// <summary>
/// Thrown by <see cref="IPostnomicAuthoringService"/> when the API rejects a request. Unlike the
/// read-only <see cref="IPostnomicBlogService"/> (which mostly maps 404s and soft failures to
/// <see langword="null"/>), authoring calls carry business-rule rejections — e.g. a 409 when
/// publishing a post that is already published, or a 403 when a subscription quota is
/// exhausted — that a caller needs to distinguish and act on. The exception's <c>Message</c>
/// carries the API's response body verbatim when present (it responds with a plain-text reason
/// on most rejections), falling back to the HTTP reason phrase.
/// </summary>
public class PostnomicApiException : Exception
{
    /// <summary>The HTTP status code returned by the API.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Creates a new <see cref="PostnomicApiException"/>.</summary>
    /// <param name="statusCode">The HTTP status code returned by the API.</param>
    /// <param name="message">The rejection reason, taken from the API's response body or reason phrase.</param>
    public PostnomicApiException(HttpStatusCode statusCode, string? message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

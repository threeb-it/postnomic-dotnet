using System.Net;

namespace Postnomic.Client.Abstractions;

/// <summary>
/// Thrown by the Postnomic client services when the API answered with a <b>success</b> status code
/// but the response body could not be turned into the expected result: the body was empty
/// (<c>204 No Content</c>, <c>Content-Length: 0</c>, a zero-length or whitespace-only stream) or it
/// was not valid JSON for the expected type.
/// </summary>
/// <remarks>
/// <para>
/// A success status with an unusable body means the upstream is misbehaving (a proxy, a gateway or
/// the API itself returned a truncated response), not that the content does not exist. Before this
/// type existed the SDK surfaced it as a bare <see cref="System.Text.Json.JsonException"/>
/// ("The input does not contain any JSON tokens"), which most hosts treated as an application bug
/// rather than an upstream outage.
/// </para>
/// <para>
/// It derives from <see cref="HttpRequestException"/>, so existing
/// <c>catch (HttpRequestException)</c> blocks — the same ones that already handle non-success status
/// codes from the SDK — handle it too. <see cref="HttpRequestException.StatusCode"/> carries the
/// (successful) status code the API answered with, and
/// <see cref="HttpRequestException.HttpRequestError"/> is
/// <see cref="HttpRequestError.InvalidResponse"/>.
/// </para>
/// <para>
/// It is thrown <i>after</i> the <see cref="HttpClient"/> handler pipeline has returned the
/// response, so an <c>AddStandardResilienceHandler()</c> (or any other
/// <see cref="DelegatingHandler"/>-based retry) attached to the SDK's <see cref="HttpClient"/>
/// does <b>not</b> retry it: to the handler pipeline the exchange looked like a success. Retry
/// at the call site if you need to.
/// </para>
/// <para>
/// Non-success status codes keep their existing behaviour: a plain
/// <see cref="HttpRequestException"/> from <see cref="IPostnomicBlogService"/> (or
/// <see langword="null"/> where a 404 is documented to mean "not found"), and
/// <see cref="PostnomicApiException"/> from <see cref="IPostnomicAuthoringService"/>.
/// </para>
/// </remarks>
public class PostnomicUpstreamException : HttpRequestException
{
    /// <summary>
    /// Creates a new <see cref="PostnomicUpstreamException"/>.
    /// </summary>
    /// <param name="failure">What was wrong with the response body.</param>
    /// <param name="statusCode">The (successful) HTTP status code the API answered with.</param>
    /// <param name="requestPath">The request's URI path, without the query string.</param>
    /// <param name="innerException">
    /// The underlying deserialization error for <see cref="PostnomicUpstreamFailure.MalformedBody"/>;
    /// <see langword="null"/> otherwise.
    /// </param>
    public PostnomicUpstreamException(
        PostnomicUpstreamFailure failure,
        HttpStatusCode statusCode,
        string? requestPath,
        Exception? innerException = null)
        : base(
            HttpRequestError.InvalidResponse,
            BuildMessage(failure, statusCode, requestPath),
            innerException,
            statusCode)
    {
        Failure = failure;
        RequestPath = requestPath;
    }

    /// <summary>What was wrong with the response body.</summary>
    public PostnomicUpstreamFailure Failure { get; }

    /// <summary>
    /// The request's URI path (for example <c>/public/blogs/my-blog/posts</c>), without the query
    /// string, so search terms never end up in logs. <see langword="null"/> when unknown.
    /// </summary>
    public string? RequestPath { get; }

    private static string BuildMessage(PostnomicUpstreamFailure failure, HttpStatusCode statusCode, string? requestPath)
    {
        var what = failure switch
        {
            PostnomicUpstreamFailure.EmptyBody => "an empty response body",
            PostnomicUpstreamFailure.MalformedBody => "a response body that is not valid JSON for the expected type",
            _ => "an unusable response body"
        };

        return $"The Postnomic API answered {(int)statusCode} ({statusCode}) with {what} for '{requestPath ?? "(unknown path)"}'. "
            + "This is an upstream failure, not a missing resource.";
    }
}

/// <summary>
/// Why a <see cref="PostnomicUpstreamException"/> was thrown.
/// </summary>
public enum PostnomicUpstreamFailure
{
    /// <summary>
    /// The API answered with a success status but no content: <c>204 No Content</c>,
    /// <c>Content-Length: 0</c>, or a body that was empty or contained only whitespace.
    /// </summary>
    EmptyBody = 1,

    /// <summary>
    /// The API answered with a success status and a body that could not be deserialized as JSON
    /// into the expected type. The original <see cref="System.Text.Json.JsonException"/> is the
    /// <see cref="Exception.InnerException"/>.
    /// </summary>
    MalformedBody = 2
}

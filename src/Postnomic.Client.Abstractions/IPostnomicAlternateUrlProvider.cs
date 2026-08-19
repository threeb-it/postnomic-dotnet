using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.Abstractions;

/// <summary>
/// Supplies a blog post's real per-language URLs (its <c>hreflang</c> cluster), replacing the
/// obsolete <see cref="PostnomicClientOptions.AlternateUrlResolver"/>.
/// <para>
/// Implement this in the host application and register it with
/// <c>services.AddPostnomicAlternateUrlProvider&lt;TProvider&gt;()</c>. The SDK resolves it from
/// dependency injection at the point of render — <b>not</b> while building
/// <see cref="PostnomicClientOptions"/> — so an implementation may freely depend on
/// <see cref="IPostnomicBlogService"/>, <see cref="IPostnomicAuthoringService"/>, or anything
/// else that itself consumes <c>IOptions&lt;PostnomicClientOptions&gt;</c>.
/// </para>
/// <para>
/// <b>Why this exists as a service rather than an options callback.</b> Every service this SDK
/// registers — <c>PostnomicBlogService</c>, <c>CachingPostnomicBlogService</c>,
/// <c>PostnomicAuthoringService</c>, <c>PostnomicApiKeyHandler</c> and the typed
/// <see cref="System.Net.Http.HttpClient"/> registrations behind them — takes
/// <c>IOptions&lt;PostnomicClientOptions&gt;</c>. Configuring a callback on the options object
/// with the DI-aware <c>OptionsBuilder.Configure&lt;TDep&gt;</c> overload therefore cannot use a
/// dependency that touches the SDK: building the options would construct the dependency, which
/// constructs an SDK service, which reads the options again, and the underlying
/// <see cref="Lazy{T}"/> throws
/// <c>InvalidOperationException: ValueFactory attempted to access the Value property of this
/// instance.</c> Resolving this provider from DI at the point of use sidesteps that cycle
/// entirely.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Ordering matters.</b> The FIRST entry of a non-<see langword="null"/> result is used as the
/// <c>hreflang="x-default"</c> target (see
/// <see cref="Seo.PostnomicSeoModel.XDefaultUrl"/>), so return the blog's default-language entry
/// first.
/// </para>
/// <para>
/// <b>Duplicate URLs are collapsed.</b> When two or more languages genuinely resolve to the same
/// URL, only the first is kept — see the "shared-URL case" remarks on
/// <see cref="Seo.PostnomicSeoBuilder.ForPost"/>. Returning the same URL under two hreflang
/// values does not describe a language split and is dropped rather than emitted.
/// </para>
/// <para>
/// Each URL may be root-relative (e.g. <c>"/blog/post/kurze-hoerbuecher"</c>) or absolute; both
/// are normalized the same way the SDK's composed alternates already are.
/// </para>
/// <para>
/// Returning <see langword="null"/> for a post falls back to
/// <see cref="PostnomicRouteBuilder.BuildPostAlternates"/>'s composed alternates for that post
/// specifically, exactly as if no provider were registered.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class BlogAlternateUrlProvider(IPostnomicBlogService blog)
///     : IPostnomicAlternateUrlProvider
/// {
///     public async ValueTask&lt;IReadOnlyList&lt;(string Language, string Url)&gt;?&gt; GetAlternatesAsync(
///         PostnomicPostDetail post, CancellationToken cancellationToken = default)
///     {
///         var alternates = new List&lt;(string, string)&gt;();
///         foreach (var language in post.AvailableLanguages)
///         {
///             // Ask the API for the translation so its REAL slug is used - a translated slug
///             // is not derivable from the original's.
///             var translated = await blog.GetPostAsync(post.Slug, language, cancellationToken);
///             if (translated is not null)
///                 alternates.Add((language, $"/blog/post/{translated.Slug}"));
///         }
///
///         return alternates.Count &gt; 0 ? alternates : null;
///     }
/// }
/// </code>
/// </example>
public interface IPostnomicAlternateUrlProvider
{
    /// <summary>
    /// Returns this post's per-language URLs, or <see langword="null"/> to fall back to the SDK's
    /// composed alternates for this post.
    /// </summary>
    /// <param name="post">The post being rendered, as returned by the API.</param>
    /// <param name="cancellationToken">Propagates notification that the request should be cancelled.</param>
    /// <returns>
    /// An ordered list of (ISO-639-1 language code, URL) pairs whose first entry is the
    /// <c>x-default</c> target, or <see langword="null"/> to use the composed alternates.
    /// </returns>
    ValueTask<IReadOnlyList<(string Language, string Url)>?> GetAlternatesAsync(
        PostnomicPostDetail post,
        CancellationToken cancellationToken = default);
}

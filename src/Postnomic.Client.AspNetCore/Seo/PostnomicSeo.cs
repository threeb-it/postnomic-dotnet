using Microsoft.AspNetCore.Http;
using Postnomic.Client.Abstractions.Seo;
using Postnomic.Client.AspNetCore.Areas.Blog.Pages;

namespace Postnomic.Client.AspNetCore.Seo;

/// <summary>
/// Adapts <see cref="HttpRequest"/> + the Blog area's Razor Page models to
/// <see cref="Postnomic.Client.Abstractions.Seo.PostnomicSeoBuilder"/>, the shared SEO-model
/// builder also used by <c>Postnomic.Client.Blazor</c>. Consumed by <c>_SeoHead.cshtml</c>.
/// </summary>
public static class PostnomicSeo
{
    /// <summary>Builds the SEO model for the blog index (listing) page.</summary>
    public static PostnomicSeoModel ForIndex(HttpRequest request, IndexModel model)
        => PostnomicSeoBuilder.ForIndex(
            BaseUri(request), model.BasePath, model.RouteStyle, model.Lang, model.BlogInfo, model.Posts.Items);

    /// <summary>Builds the SEO model for a single blog post page.</summary>
    public static PostnomicSeoModel ForPost(HttpRequest request, PostModel model)
        => PostnomicSeoBuilder.ForPost(
            BaseUri(request), model.BasePath, model.RouteStyle, model.Lang, model.PostSlug, model.Post, model.BlogInfo,
            model.AlternateUrls);

    /// <summary>Builds the SEO model for an author profile page.</summary>
    public static PostnomicSeoModel ForAuthor(HttpRequest request, AuthorModel model)
        => PostnomicSeoBuilder.ForAuthor(
            BaseUri(request), model.BasePath, model.RouteStyle, model.Lang, model.AuthorSlug, model.Profile);

    /// <summary>
    /// Converts a path-relative or already-absolute, non-empty URL to an absolute URL using
    /// <paramref name="request"/>'s scheme and host.
    /// </summary>
    public static string ToAbsoluteUrl(HttpRequest request, string pathOrUrl)
        => PostnomicSeoBuilder.ToAbsoluteUrl(BaseUri(request), pathOrUrl);

    /// <summary>
    /// Same as <see cref="ToAbsoluteUrl(HttpRequest, string)"/> but returns
    /// <see langword="null"/> unchanged when <paramref name="pathOrUrl"/> is null or empty.
    /// </summary>
    public static string? ToAbsoluteUrlOrNull(HttpRequest request, string? pathOrUrl)
        => PostnomicSeoBuilder.ToAbsoluteUrlOrNull(BaseUri(request), pathOrUrl);

    private static string BaseUri(HttpRequest request) => $"{request.Scheme}://{request.Host}";
}

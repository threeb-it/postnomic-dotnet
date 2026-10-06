using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client.Blazor;

/// <summary>
/// Resolves <see cref="PostnomicClientOptions.FilterLinkRel"/> for a Blazor component: the cascaded
/// <see cref="PostnomicBlogContext"/> (a named blog rendered inside <c>PostnomicBlogScope</c>) wins,
/// otherwise the default registration's options. Every component that renders a filter, archive or
/// pagination <em>link</em> uses this as <c>rel="@FilterRel"</c> — Blazor omits the attribute when the
/// value is <see langword="null"/>, so the markup is unchanged while the option is unset.
/// </summary>
internal static class PostnomicFilterLinks
{
    /// <summary>The <c>rel</c> value for a filter / archive / pagination link, or <see langword="null"/>.</summary>
    public static string? Rel(PostnomicBlogContext? blogContext, IOptions<PostnomicClientOptions> defaultOptions) =>
        PostnomicLinkRel.ForFilterLink((blogContext?.Options ?? defaultOptions.Value).FilterLinkRel);
}

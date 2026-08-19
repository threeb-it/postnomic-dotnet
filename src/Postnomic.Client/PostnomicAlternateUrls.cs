using Microsoft.Extensions.DependencyInjection;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client;

/// <summary>
/// Resolves a post's hreflang alternates from dependency injection, applying the same precedence
/// in both hosting models so <c>Postnomic.Client.AspNetCore</c> and <c>Postnomic.Client.Blazor</c>
/// emit identical SEO output.
/// </summary>
public static class PostnomicAlternateUrls
{
    /// <summary>
    /// Resolves this post's per-language URLs, in precedence order:
    /// <list type="number">
    /// <item><description>
    /// an <see cref="IPostnomicAlternateUrlProvider"/> registered for <paramref name="blogName"/>
    /// as a keyed service (multi-blog hosts), matching how the SDK already resolves a named
    /// blog's <see cref="IPostnomicBlogService"/>;
    /// </description></item>
    /// <item><description>
    /// an unkeyed <see cref="IPostnomicAlternateUrlProvider"/> (the single-blog case, and the
    /// fallback for a named blog with no provider of its own);
    /// </description></item>
    /// <item><description>
    /// the obsolete <see cref="PostnomicClientOptions.AlternateUrlResolver"/>, still honoured so
    /// existing consumers keep working across the upgrade;
    /// </description></item>
    /// <item><description>
    /// <see langword="null"/>, which leaves
    /// <see cref="Abstractions.Seo.PostnomicSeoBuilder.ForPost"/> to compose alternates from
    /// <see cref="PostnomicRouteBuilder.BuildPostAlternates"/> as before.
    /// </description></item>
    /// </list>
    /// A registered provider that returns <see langword="null"/> for a given post falls through to
    /// the composed alternates for that post — it does <b>not</b> fall through to the obsolete
    /// resolver, so a host migrating one post at a time sees one source of truth per post.
    /// </summary>
    /// <param name="services">The scope to resolve the provider from (the request or component scope).</param>
    /// <param name="options">The effective options for the blog being rendered.</param>
    /// <param name="blogName">
    /// The registered name of the blog being rendered in a multi-blog host, or
    /// <see langword="null"/> for the single unnamed default blog.
    /// </param>
    /// <param name="post">The post being rendered.</param>
    /// <param name="cancellationToken">Propagates notification that the request should be cancelled.</param>
    public static async ValueTask<IReadOnlyList<(string Language, string Url)>?> ResolveAsync(
        IServiceProvider services,
        PostnomicClientOptions options,
        string? blogName,
        PostnomicPostDetail post,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(post);

        var provider = ResolveProvider(services, blogName);
        if (provider is not null)
            return await provider.GetAlternatesAsync(post, cancellationToken).ConfigureAwait(false);

#pragma warning disable CS0618 // Still honoured on purpose: existing consumers must not break.
        return options.AlternateUrlResolver?.Invoke(post);
#pragma warning restore CS0618
    }

    private static IPostnomicAlternateUrlProvider? ResolveProvider(IServiceProvider services, string? blogName)
    {
        if (blogName is not null)
        {
            var keyed = services.GetKeyedService<IPostnomicAlternateUrlProvider>(blogName);
            if (keyed is not null)
                return keyed;
        }

        return services.GetService<IPostnomicAlternateUrlProvider>();
    }
}

using Microsoft.Extensions.DependencyInjection;
using Postnomic.Client;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client.Blazor;

/// <summary>
/// Extension methods for registering the Postnomic blog Blazor components and the underlying
/// HTTP client with an <see cref="IServiceCollection"/>.
/// </summary>
public static class PostnomicBlazorExtensions
{
    /// <summary>
    /// Adds the Postnomic blog Blazor components and underlying client services to the service
    /// collection. After calling this method, <see cref="IPostnomicBlogService"/> is injectable
    /// and the Postnomic Blazor components (<c>BlogPage</c>, <c>PostPage</c>) can be rendered in
    /// your own routable pages. Configure <see cref="PostnomicClientOptions.BasePath"/> to set the
    /// URL prefix used by internal links (default: <c>/blog</c>).
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">A delegate that configures <see cref="PostnomicClientOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddPostnomicBlog(
        this IServiceCollection services,
        Action<PostnomicClientOptions> configure)
    {
        services.AddPostnomicClient(configure);
        return services;
    }

    /// <summary>
    /// Adds a named Postnomic blog as a keyed service. Call this method multiple times with
    /// different <paramref name="name"/> values to host several blogs in a single Blazor application.
    /// Wrap your blog pages in a <c>&lt;PostnomicBlogScope BlogName="name"&gt;</c> component to
    /// scope the blog service for child components.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="name">
    /// A unique name for this blog (e.g. <c>"free"</c>, <c>"enterprise"</c>).
    /// Pass the same value to <c>PostnomicBlogScope.BlogName</c>.
    /// </param>
    /// <param name="configure">A delegate that configures <see cref="PostnomicClientOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddPostnomicBlog(
        this IServiceCollection services,
        string name,
        Action<PostnomicClientOptions> configure)
    {
        services.AddPostnomicClient(name, configure);
        return services;
    }
}

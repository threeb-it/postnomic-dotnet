using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Postnomic.Client;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client.AspNetCore;

/// <summary>
/// Extension methods for integrating the Postnomic blog Razor Pages Area into an ASP.NET Core
/// application.
/// </summary>
public static class PostnomicAspNetCoreExtensions
{
    /// <summary>
    /// Adds Postnomic blog Razor Pages and the underlying HTTP client services to the DI container.
    /// Call this in <c>Program.cs</c> before <c>builder.Build()</c>.
    /// </summary>
    /// <remarks>
    /// This method registers <see cref="IPostnomicBlogService"/> and configures the named
    /// <see cref="System.Net.Http.HttpClient"/> used to communicate with the Postnomic API.
    /// The host application must also call <c>services.AddRazorPages()</c> (or
    /// <c>services.AddControllersWithViews()</c>) so that the Area pages are discovered.
    /// The Blog area pages are served at <see cref="PostnomicClientOptions.BasePath"/>
    /// (default: <c>/blog</c>).
    /// </remarks>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">
    /// A delegate that configures <see cref="PostnomicClientOptions"/> — at minimum set
    /// <see cref="PostnomicClientOptions.BaseUrl"/>, <see cref="PostnomicClientOptions.ApiKey"/>,
    /// and <see cref="PostnomicClientOptions.BlogSlug"/>.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddPostnomicBlog(
        this IServiceCollection services,
        Action<PostnomicClientOptions> configure)
    {
        var tempOptions = new PostnomicClientOptions();
        configure(tempOptions);

        services.AddPostnomicClient(configure);

        services.TryAddSingleton<IPostnomicBlogResolver, PostnomicBlogResolver>();

        services.PostConfigure<RazorPagesOptions>(razorOptions =>
        {
            razorOptions.Conventions.Add(new PostnomicBlogAreaRouteConvention(tempOptions.BasePath));
        });

        return services;
    }

    /// <summary>
    /// Adds a named Postnomic blog as a keyed service with its own set of Razor Page routes.
    /// Call this method multiple times with different <paramref name="name"/> values to host
    /// several blogs in a single ASP.NET Core application. Each blog's routes are served at its
    /// configured <see cref="PostnomicClientOptions.BasePath"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="name">
    /// A unique name for this blog (e.g. <c>"free"</c>, <c>"enterprise"</c>).
    /// </param>
    /// <param name="configure">
    /// A delegate that configures <see cref="PostnomicClientOptions"/> for this blog.
    /// Each blog should have a distinct <see cref="PostnomicClientOptions.BasePath"/>.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddPostnomicBlog(
        this IServiceCollection services,
        string name,
        Action<PostnomicClientOptions> configure)
    {
        var tempOptions = new PostnomicClientOptions();
        configure(tempOptions);

        services.AddPostnomicClient(name, configure);

        services.TryAddSingleton<IPostnomicBlogResolver, PostnomicBlogResolver>();

        services.Configure<PostnomicBlogResolverOptions>(opts =>
        {
            opts.BasePathToBlogName[tempOptions.BasePath] = name;
        });

        services.PostConfigure<RazorPagesOptions>(razorOptions =>
        {
            razorOptions.Conventions.Add(new PostnomicBlogAreaRouteConvention(tempOptions.BasePath));
        });

        return services;
    }

    /// <summary>
    /// Maps the Postnomic blog Area routes. Call this after <c>app.MapRazorPages()</c>.
    /// </summary>
    /// <remarks>
    /// The Blog Area pages are discovered automatically by the Razor Pages engine when the
    /// <c>Postnomic.Client.AspNetCore</c> assembly is referenced and Razor Pages are enabled.
    /// This method is provided as a hook for any future route customisation and to make the
    /// integration intent explicit in the application's pipeline setup.
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same <paramref name="endpoints"/> instance for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapPostnomicBlog(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}

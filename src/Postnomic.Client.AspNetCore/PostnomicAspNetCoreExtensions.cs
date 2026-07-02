using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Postnomic.Client;
using Postnomic.Client.Abstractions;
using Postnomic.Client.AspNetCore.Seo;

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

        // Track this as a registered blog (Name = null denotes the single, unnamed default
        // registration, whose IPostnomicBlogService is resolved non-keyed) so MapPostnomicBlog
        // can enumerate it alongside any named registrations below.
        services.Configure<PostnomicBlogResolverOptions>(opts =>
        {
            opts.RegisteredBlogs.Add(new PostnomicRegisteredBlog(null, tempOptions.BasePath, tempOptions.LanguageRouteStyle));
        });

        services.PostConfigure<RazorPagesOptions>(razorOptions =>
        {
            razorOptions.Conventions.Add(new PostnomicBlogAreaRouteConvention(tempOptions.BasePath, tempOptions.LanguageRouteStyle));
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
            opts.BasePathToLanguageRouteStyle[tempOptions.BasePath] = tempOptions.LanguageRouteStyle;
            opts.RegisteredBlogs.Add(new PostnomicRegisteredBlog(name, tempOptions.BasePath, tempOptions.LanguageRouteStyle));
        });

        services.PostConfigure<RazorPagesOptions>(razorOptions =>
        {
            razorOptions.Conventions.Add(new PostnomicBlogAreaRouteConvention(tempOptions.BasePath, tempOptions.LanguageRouteStyle));
        });

        return services;
    }

    /// <summary>
    /// Maps the Postnomic blog Area routes, plus a <c>GET {basePath}/sitemap.xml</c> and
    /// <c>GET {basePath}/rss.xml</c> endpoint for every blog registered via
    /// <c>AddPostnomicBlog</c> (the single unnamed default blog and any named blogs alike).
    /// Call this after <c>app.MapRazorPages()</c>.
    /// </summary>
    /// <remarks>
    /// The Blog Area pages are discovered automatically by the Razor Pages engine when the
    /// <c>Postnomic.Client.AspNetCore</c> assembly is referenced and Razor Pages are enabled.
    /// The sitemap and RSS feed for each blog are built from
    /// <see cref="IPostnomicBlogService.GetPostsAsync"/>; see <see cref="PostnomicFeeds"/> for
    /// how many posts each document includes. When no blog has been registered (no
    /// <c>AddPostnomicBlog</c> call was made), this is a no-op beyond returning
    /// <paramref name="endpoints"/> unchanged.
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same <paramref name="endpoints"/> instance for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapPostnomicBlog(this IEndpointRouteBuilder endpoints)
    {
        foreach (var blog in GetRegisteredBlogs(endpoints))
        {
            var basePath = "/" + blog.BasePath.Trim('/');
            var style = blog.Style;
            var name = blog.Name;

            endpoints.MapGet(basePath + "/sitemap.xml", async (HttpContext httpContext) =>
            {
                var blogService = ResolveBlogService(httpContext.RequestServices, name);
                var xml = await PostnomicFeeds.BuildSitemapAsync(
                    blogService, httpContext.Request, basePath, style, httpContext.RequestAborted);
                return Results.Content(xml, "application/xml");
            });

            endpoints.MapGet(basePath + "/rss.xml", async (HttpContext httpContext) =>
            {
                var blogService = ResolveBlogService(httpContext.RequestServices, name);
                var blogInfo = await blogService.GetBlogAsync(httpContext.RequestAborted);
                var xml = await PostnomicFeeds.BuildRssAsync(
                    blogService,
                    httpContext.Request,
                    basePath,
                    style,
                    channelTitle: blogInfo?.Name ?? "Blog",
                    channelDescription: blogInfo?.Description,
                    httpContext.RequestAborted);
                return Results.Content(xml, "application/rss+xml");
            });
        }

        return endpoints;
    }

    /// <summary>
    /// Maps a <c>GET /robots.txt</c> endpoint that allows all crawling and lists a
    /// <c>Sitemap:</c> directive for every blog registered via <c>AddPostnomicBlog</c>. This is
    /// opt-in — call it only when the host application does not already serve its own
    /// <c>/robots.txt</c>; mapping both would register two competing handlers for the same
    /// route (the most recently mapped one wins).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same <paramref name="endpoints"/> instance for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapPostnomicRobots(this IEndpointRouteBuilder endpoints)
    {
        var registeredBlogs = GetRegisteredBlogs(endpoints);

        endpoints.MapGet("/robots.txt", (HttpContext httpContext) =>
        {
            var lines = new List<string> { "User-agent: *", "Allow: /" };

            foreach (var blog in registeredBlogs)
            {
                var basePath = "/" + blog.BasePath.Trim('/');
                var sitemapUrl = PostnomicSeo.ToAbsoluteUrl(httpContext.Request, basePath + "/sitemap.xml");
                lines.Add($"Sitemap: {sitemapUrl}");
            }

            return Results.Text(string.Join('\n', lines) + '\n', "text/plain");
        });

        return endpoints;
    }

    private static IReadOnlyList<PostnomicRegisteredBlog> GetRegisteredBlogs(IEndpointRouteBuilder endpoints)
    {
        var resolverOptions = endpoints.ServiceProvider.GetService<IOptions<PostnomicBlogResolverOptions>>();
        return resolverOptions is null ? [] : resolverOptions.Value.RegisteredBlogs;
    }

    private static IPostnomicBlogService ResolveBlogService(IServiceProvider services, string? blogName) =>
        blogName is null
            ? services.GetRequiredService<IPostnomicBlogService>()
            : services.GetRequiredKeyedService<IPostnomicBlogService>(blogName);
}

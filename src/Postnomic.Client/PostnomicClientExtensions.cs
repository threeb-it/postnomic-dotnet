using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client;

/// <summary>
/// Extension methods for registering Postnomic blog client services with an
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Postnomic blog client services, including the typed
    /// <see cref="IPostnomicBlogService"/> and the <see cref="PostnomicApiKeyHandler"/>
    /// delegating handler that injects the <c>X-Api-Key</c> header on every request.
    /// When <see cref="PostnomicCacheOptions.Enabled"/> is <see langword="true"/> in the
    /// supplied options, a caching decorator backed by <see cref="IMemoryCache"/> is
    /// automatically registered around the HTTP service.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">
    /// A delegate that configures <see cref="PostnomicClientOptions"/>, including the
    /// <see cref="PostnomicClientOptions.BaseUrl"/>, <see cref="PostnomicClientOptions.ApiKey"/>,
    /// and <see cref="PostnomicClientOptions.BlogSlug"/>.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddPostnomicClient(options =>
    /// {
    ///     options.BaseUrl  = "https://api.postnomic.com";
    ///     options.ApiKey   = "my-api-key";
    ///     options.BlogSlug = "my-blog";
    ///     options.Cache = new PostnomicCacheOptions { Enabled = true };
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddPostnomicClient(
        this IServiceCollection services,
        Action<PostnomicClientOptions> configure)
    {
        services.Configure(configure);

        services.AddTransient<PostnomicApiKeyHandler>();

        // Register the concrete HTTP client as a typed client (not against the interface)
        // so it can be resolved directly when building the optional caching decorator.
        services.AddHttpClient<PostnomicBlogService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        })
        .AddHttpMessageHandler<PostnomicApiKeyHandler>();

        // Evaluate options at registration time to determine if caching is requested.
        var tempOptions = new PostnomicClientOptions();
        configure(tempOptions);

        if (tempOptions.Cache?.Enabled == true)
        {
            // IMemoryCache is part of the framework — no external NuGet package needed.
            services.TryAddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));

            services.AddSingleton<IPostnomicBlogService>(sp =>
            {
                var inner = sp.GetRequiredService<PostnomicBlogService>();
                var cache = sp.GetRequiredService<IMemoryCache>();
                var opts = sp.GetRequiredService<IOptions<PostnomicClientOptions>>();
                return new CachingPostnomicBlogService(inner, cache, opts);
            });

            services.AddSingleton<IPostnomicCacheControl>(sp =>
            {
                // The caching service instance implements both interfaces; retrieve it via the
                // blog service registration to guarantee the same singleton is used for both.
                var service = sp.GetRequiredService<IPostnomicBlogService>();
                return service as IPostnomicCacheControl ?? new NoOpCacheControl();
            });
        }
        else
        {
            services.AddSingleton<IPostnomicBlogService>(sp =>
                sp.GetRequiredService<PostnomicBlogService>());
            services.AddSingleton<IPostnomicCacheControl>(new NoOpCacheControl());
        }

        return services;
    }

    /// <summary>
    /// Registers a named Postnomic blog client as a keyed service. Call this method multiple
    /// times with different <paramref name="name"/> values to host several blogs in one application.
    /// The keyed <see cref="IPostnomicBlogService"/> can be resolved with
    /// <c>serviceProvider.GetRequiredKeyedService&lt;IPostnomicBlogService&gt;(name)</c> or
    /// consumed automatically via <c>PostnomicBlogScope</c> in Blazor.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="name">
    /// A unique name for this blog registration (e.g. <c>"free"</c>, <c>"enterprise"</c>).
    /// Used as the keyed-service key.
    /// </param>
    /// <param name="configure">
    /// A delegate that configures <see cref="PostnomicClientOptions"/> for this blog.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddPostnomicClient("free", options =>
    /// {
    ///     options.BaseUrl  = "https://api.postnomic.com";
    ///     options.ApiKey   = "pk_free_key";
    ///     options.BlogSlug = "my-free-blog";
    ///     options.BasePath = "/blog/free";
    /// });
    /// builder.Services.AddPostnomicClient("enterprise", options =>
    /// {
    ///     options.BaseUrl  = "https://api.postnomic.com";
    ///     options.ApiKey   = "pk_enterprise_key";
    ///     options.BlogSlug = "my-enterprise-blog";
    ///     options.BasePath = "/blog/enterprise";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddPostnomicClient(
        this IServiceCollection services,
        string name,
        Action<PostnomicClientOptions> configure)
    {
        // Named options so IOptionsMonitor<PostnomicClientOptions>.Get(name) works.
        services.Configure<PostnomicClientOptions>(name, configure);

        var httpClientName = $"Postnomic_{name}";

        // Named HttpClient with per-blog API key handler.
        services.AddHttpClient(httpClientName, (sp, client) =>
        {
            var monitor = sp.GetRequiredService<IOptionsMonitor<PostnomicClientOptions>>();
            var opts = monitor.Get(name);
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        })
        .AddHttpMessageHandler(sp =>
        {
            var monitor = sp.GetRequiredService<IOptionsMonitor<PostnomicClientOptions>>();
            return new PostnomicApiKeyHandler(Options.Create(monitor.Get(name)));
        });

        var tempOptions = new PostnomicClientOptions();
        configure(tempOptions);

        if (tempOptions.Cache?.Enabled == true)
        {
            services.TryAddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));

            services.AddKeyedSingleton<IPostnomicBlogService>(name, (sp, _) =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient(httpClientName);
                var monitor = sp.GetRequiredService<IOptionsMonitor<PostnomicClientOptions>>();
                var opts = Options.Create(monitor.Get(name));
                var inner = new PostnomicBlogService(httpClient, opts);
                var cache = sp.GetRequiredService<IMemoryCache>();
                return (IPostnomicBlogService)new CachingPostnomicBlogService(inner, cache, opts);
            });
        }
        else
        {
            services.AddKeyedSingleton<IPostnomicBlogService>(name, (sp, _) =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient(httpClientName);
                var monitor = sp.GetRequiredService<IOptionsMonitor<PostnomicClientOptions>>();
                var opts = Options.Create(monitor.Get(name));
                return (IPostnomicBlogService)new PostnomicBlogService(httpClient, opts);
            });
        }

        return services;
    }
}

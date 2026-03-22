namespace Postnomic.Client.Abstractions;

/// <summary>
/// Configuration options for the Postnomic blog client.
/// Bind this class from your application's configuration (e.g. <c>appsettings.json</c>) or
/// supply values directly when calling
/// <c>services.AddPostnomicClient(options => { ... })</c>.
/// </summary>
public class PostnomicClientOptions
{
    /// <summary>
    /// The base URL of the Postnomic API (e.g. <c>"https://api.postnomic.com"</c>).
    /// Must not include a trailing slash.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// The API key used to authenticate with the Postnomic API.
    /// This value is sent as the <c>X-Api-Key</c> HTTP request header on every call.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// The URL-friendly slug of the blog that this client instance targets
    /// (e.g. <c>"my-blog"</c>).
    /// </summary>
    public string BlogSlug { get; set; } = "";

    /// <summary>
    /// The base path at which the blog pages are served (e.g. <c>"/blog"</c> or <c>"/articles"</c>).
    /// Must start with a forward slash and must not include a trailing slash.
    /// Defaults to <c>"/blog"</c>.
    /// </summary>
    public string BasePath { get; set; } = "/blog";

    /// <summary>
    /// When <see langword="true"/>, a "Powered by Postnomic" promotional footer is rendered
    /// below each blog post. This is enabled by default on Free-tier blogs and can be
    /// disabled on paid plans.
    /// </summary>
    public bool ShowBranding { get; set; }

    /// <summary>
    /// Optional cache settings. When <see langword="null"/> or when
    /// <see cref="PostnomicCacheOptions.Enabled"/> is <see langword="false"/>,
    /// no caching is applied and every call hits the API directly.
    /// </summary>
    public PostnomicCacheOptions? Cache { get; set; }
}

/// <summary>
/// Configures client-side in-memory caching for the Postnomic blog client.
/// All durations use absolute expiration relative to the time the entry is created.
/// </summary>
public class PostnomicCacheOptions
{
    /// <summary>
    /// Master switch to enable or disable client-side caching.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// How long blog metadata (info, tags, categories, authors) stays cached.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan MetadataDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long post list pages stay cached. Default: 2 minutes.
    /// </summary>
    public TimeSpan PostListDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long individual post details stay cached. Default: 5 minutes.
    /// </summary>
    public TimeSpan PostDetailDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long popular/most-read post lists stay cached. Default: 10 minutes.
    /// </summary>
    public TimeSpan PopularPostsDuration { get; set; } = TimeSpan.FromMinutes(10);
}

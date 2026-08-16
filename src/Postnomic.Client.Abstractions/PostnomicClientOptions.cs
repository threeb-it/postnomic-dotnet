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
    /// Read-only: scoped to anonymous access to a single blog's published content. Used by
    /// <see cref="IPostnomicBlogService"/> (<c>AddPostnomicClient</c>); ignored by
    /// <see cref="IPostnomicAuthoringService"/>.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// The URL-friendly slug of the blog that this client instance targets
    /// (e.g. <c>"my-blog"</c>). Used by <see cref="IPostnomicBlogService"/>'s read routes
    /// (<c>/public/blogs/{slug}/...</c>). Not the same value as <see cref="BlogId"/>, which the
    /// authoring routes require instead.
    /// </summary>
    public string BlogSlug { get; set; } = "";

    /// <summary>
    /// A user-level Personal Access Token (format <c>pnp_...</c>), minted from the Postnomic
    /// dashboard's "Access Tokens" page. Sent as <c>Authorization: Bearer &lt;token&gt;</c>.
    /// Required by <see cref="IPostnomicAuthoringService"/> (<c>AddPostnomicAuthoringClient</c>)
    /// for every write; ignored by the read-only <see cref="IPostnomicBlogService"/>. The
    /// token's owner must be a member of <see cref="BlogId"/> with at least the <c>Author</c>
    /// role — see the remarks on <see cref="IPostnomicAuthoringService"/> for the exact roles
    /// each operation needs.
    /// </summary>
    public string? PersonalAccessToken { get; set; }

    /// <summary>
    /// The public ID (a GUID) of the blog that <see cref="IPostnomicAuthoringService"/> targets —
    /// as shown in the Postnomic dashboard URL, or returned as <c>BlogResponse.PublicId</c> by
    /// the management API. This is <b>not</b> the same value as <see cref="BlogSlug"/>: the
    /// authoring API's routes (<c>/blogs/{blogId}/...</c>) key on the blog's public ID, not its
    /// slug. Ignored by the read-only <see cref="IPostnomicBlogService"/>.
    /// </summary>
    public string? BlogId { get; set; }

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

    /// <summary>Controls where the language code appears in generated blog URLs and routes.
    /// Default <see cref="PostnomicLanguageRouteStyle.Suffix"/> preserves pre-1.2 behavior.</summary>
    public PostnomicLanguageRouteStyle LanguageRouteStyle { get; set; } = PostnomicLanguageRouteStyle.Suffix;

    /// <summary>
    /// Selects the CSS class vocabulary emitted by Postnomic-rendered markup.
    /// Default <see cref="PostnomicMarkupStyle.Bootstrap"/> preserves pre-theming behavior byte-for-byte;
    /// opt into <see cref="PostnomicMarkupStyle.Semantic"/> to theme the blog via CSS variables.
    /// </summary>
    public PostnomicMarkupStyle MarkupStyle { get; set; } = PostnomicMarkupStyle.Bootstrap;

    /// <summary>
    /// Optional overrides for the Blazor blog components' built-in UI chrome strings (the pager,
    /// the search box, comment-form labels, empty states, and similar SDK-authored copy — not a
    /// post's own content). The SDK ships English and German built-ins; set this to replace
    /// individual keys or add a language of your own without forking the package. <see langword="null"/>
    /// (the default) renders the built-in strings as-is, selected by each page's own
    /// <c>Language</c> parameter.
    /// </summary>
    public PostnomicUiStringOverrides? UiStrings { get; set; }
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

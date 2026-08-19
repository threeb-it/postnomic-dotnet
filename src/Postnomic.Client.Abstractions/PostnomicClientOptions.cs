using Postnomic.Client.Abstractions.Models;

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

    /// <summary>
    /// <b>Obsolete — use <see cref="IPostnomicAlternateUrlProvider"/> instead.</b> This callback
    /// is synchronous, so it cannot make the API call needed to discover a translation's real
    /// slug, and it cannot be configured through <c>OptionsBuilder.Configure&lt;TDep&gt;</c> with
    /// any dependency that touches the SDK (see the remarks on
    /// <see cref="IPostnomicAlternateUrlProvider"/> for why). It remains fully supported until the
    /// next major version; a registered <see cref="IPostnomicAlternateUrlProvider"/> takes
    /// precedence over it.
    /// <para>
    /// Optional host-supplied override for a blog post's hreflang alternates, called once per
    /// post-detail render with the post being rendered.
    /// </para>
    /// <para>
    /// <see cref="Postnomic.Client.Abstractions.PostnomicRouteBuilder.BuildPostAlternates"/> (the
    /// default source of <see cref="Postnomic.Client.Abstractions.Seo.PostnomicSeoModel.Alternates"/>)
    /// can only ever apply ONE <see cref="PostnomicLanguageRouteStyle"/> to the SAME post slug
    /// across every language — see its own XML docs. It has no way to know a translation's real
    /// slug, because neither <see cref="PostnomicPostDetail"/> nor the authoring-side translation
    /// model carries a per-language slug field. In practice a blog's translations rarely follow
    /// one predictable shape: the same slug may serve every language (content negotiated some
    /// other way), a suffix may be appended for one language only, or a translation may carry a
    /// wholly different, hand-translated slug — and under
    /// <see cref="PostnomicLanguageRouteStyle.None"/> specifically, <c>BuildPostAlternates</c>
    /// cannot distinguish any of these cases at all, since no language ever gets its own URL
    /// segment. When any of that applies, set this to look up each language's real URL from
    /// whatever store of translation slugs (or full URLs) the host application itself owns.
    /// </para>
    /// <para>
    /// Return <see langword="null"/> for a post to fall back to <c>BuildPostAlternates</c>'s
    /// composed alternates for that post specifically. Leaving this whole property unset (the
    /// default) preserves the composed-alternates behavior for every post, unchanged.
    /// </para>
    /// <para>
    /// The FIRST entry of a non-null result is used as the <c>hreflang="x-default"</c> target
    /// (see <see cref="Postnomic.Client.Abstractions.Seo.PostnomicSeoModel.XDefaultUrl"/>), so
    /// return the blog's default-language entry first, exactly like <c>BuildPostAlternates</c>
    /// itself does.
    /// </para>
    /// <para>
    /// When two or more languages genuinely share the exact same URL, include it only once — see
    /// the "de-duplicated by URL" remarks on
    /// <see cref="Postnomic.Client.Abstractions.Seo.PostnomicSeoBuilder.ForPost"/> for why a
    /// second hreflang entry pointing at an identical URL is never emitted, even if this resolver
    /// returns one.
    /// </para>
    /// Each URL may be root-relative (e.g. <c>"/blog/post/short-audiobooks-en"</c>) or absolute;
    /// both are normalized the same way the composed alternates already are.
    /// </summary>
    [Obsolete(
        "Set AlternateUrlResolver is obsolete and will be removed in a future major version. " +
        "Implement IPostnomicAlternateUrlProvider and register it with " +
        "services.AddPostnomicAlternateUrlProvider<TProvider>() instead. The replacement is " +
        "async and is resolved from dependency injection at the point of render, so it may " +
        "depend on IPostnomicBlogService; this callback cannot, because every SDK service " +
        "consumes IOptions<PostnomicClientOptions> and the resulting cycle throws " +
        "\"ValueFactory attempted to access the Value property of this instance.\"")]
    public Func<PostnomicPostDetail, IReadOnlyList<(string Language, string Url)>?>? AlternateUrlResolver { get; set; }
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

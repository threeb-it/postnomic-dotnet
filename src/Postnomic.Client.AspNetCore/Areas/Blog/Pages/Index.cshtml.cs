using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.AspNetCore.Resilience;

namespace Postnomic.Client.AspNetCore.Areas.Blog.Pages;

/// <summary>
/// Page model for the blog listing page at <c>/blog</c>.
/// Loads a paginated list of posts together with all sidebar data in a single
/// <see cref="OnGetAsync"/> call.
/// </summary>
public class IndexModel(
    IPostnomicBlogService defaultBlogService,
    IServiceProvider serviceProvider,
    IPostnomicBlogResolver blogResolver,
    IOptions<PostnomicClientOptions> defaultClientOptions,
    IOptionsMonitor<PostnomicClientOptions> optionsMonitor,
    ILogger<IndexModel>? logger = null) : PageModel
{
    /// <summary>Largest accepted <see cref="PageSize"/>; anything above is clamped down to it.</summary>
    private const int MaxPageSize = 100;

    // ── Query parameters ──────────────────────────────────────────────────────

    /// <summary>The 1-based page number to display. Defaults to <c>1</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    /// <summary>Number of posts per page. Defaults to <c>5</c>.</summary>
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 5;

    /// <summary>Optional tag slug filter.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Tag { get; set; }

    /// <summary>Optional category slug filter.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    /// <summary>Optional author display-name filter.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Author { get; set; }

    /// <summary>Optional full-text search term.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    /// <summary>
    /// Optional ISO-639-1 language code bound from the <c>/{lang}/</c> route segment (e.g. "de").
    /// Null when the request used the default (non-prefixed) route.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Lang { get; set; }

    // ── Page data ─────────────────────────────────────────────────────────────

    /// <summary>Public metadata for the blog (name, description, layout).</summary>
    public PostnomicBlogInfo? BlogInfo { get; private set; }

    /// <summary>Whether the blog uses the masonry layout.</summary>
    public bool IsMasonry => string.Equals(BlogInfo?.DefaultLayout, "Masonry", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether to show the Postnomic branding in the sidebar and post footer.
    /// Prefers the server-enforced value from the API, falling back to client options.
    /// </summary>
    public bool ShowBranding
    {
        get
        {
            if (BlogInfo is not null)
                return BlogInfo.ShowBranding;

            var blogName = blogResolver.ResolveBlogName(HttpContext.Request.Path.Value ?? "");
            return blogName is not null
                ? optionsMonitor.Get(blogName).ShowBranding
                : defaultClientOptions.Value.ShowBranding;
        }
    }

    /// <summary>Paginated post summaries for the current page.</summary>
    public PostnomicPagedResult<PostnomicPostSummary> Posts { get; private set; } =
        new() { Items = [], Page = 1, PageSize = 5, TotalCount = 0, TotalPages = 0 };

    /// <summary>All tags used by at least one published post.</summary>
    public List<PostnomicTag> Tags { get; private set; } = [];

    /// <summary>All categories used by at least one published post.</summary>
    public List<PostnomicCategory> Categories { get; private set; } = [];

    /// <summary>All authors who have at least one published post.</summary>
    public List<PostnomicAuthor> Authors { get; private set; } = [];

    /// <summary>Posts ranked by approved comment count, for the sidebar widget.</summary>
    public List<PostnomicPopularPost> TopCommented { get; private set; } = [];

    /// <summary>Posts ranked by page-view count, for the sidebar widget.</summary>
    public List<PostnomicPopularPost> MostRead { get; private set; } = [];

    // ── Handler ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads all page data in parallel and returns the page for rendering.
    /// <para>
    /// The post list and the blog metadata are essential: if either fails the request fails, because
    /// there is no meaningful page without them. Every other call feeds a decorative sidebar widget
    /// and degrades to an empty list on failure — a visitor still gets the posts when the tag list
    /// is down. Cancellation of <paramref name="cancellationToken"/> (the visitor navigated away)
    /// always propagates and is never mistaken for a widget failure.
    /// </para>
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        NormalizePaging();

        var blogService = ResolveBlogService();

        // Essential — a failure here must still surface as a failure.
        var postsTask = blogService.GetPostsAsync(
            PageNumber, PageSize, Tag, Category, Author, Search,
            language: Lang,
            cancellationToken: cancellationToken);
        var blogTask = blogService.GetBlogAsync(cancellationToken);

        // Decorative — sidebar widgets, started in parallel with the essential calls and each
        // wrapped so that one failing widget renders empty instead of 500-ing the whole page.
        var tagsTask = Optional(
            () => blogService.GetTagsAsync(cancellationToken), new List<PostnomicTag>(), "tags", cancellationToken);
        var categoriesTask = Optional(
            () => blogService.GetCategoriesAsync(cancellationToken), new List<PostnomicCategory>(), "categories", cancellationToken);
        var authorsTask = Optional(
            () => blogService.GetAuthorsAsync(cancellationToken), new List<PostnomicAuthor>(), "authors", cancellationToken);
        var topCommentedTask = Optional(
            () => blogService.GetTopCommentedPostsAsync(cancellationToken: cancellationToken), new List<PostnomicPopularPost>(), "top-commented", cancellationToken);
        var mostReadTask = Optional(
            () => blogService.GetMostReadPostsAsync(cancellationToken: cancellationToken), new List<PostnomicPopularPost>(), "most-read", cancellationToken);

        await Task.WhenAll(postsTask, blogTask, tagsTask, categoriesTask, authorsTask,
            topCommentedTask, mostReadTask);

        Posts = await postsTask;
        BlogInfo = await blogTask;
        Tags = await tagsTask;
        Categories = await categoriesTask;
        Authors = await authorsTask;
        TopCommented = await topCommentedTask;
        MostRead = await mostReadTask;

        return Page();
    }

    private Task<T> Optional<T>(Func<Task<T>> load, T fallback, string widget, CancellationToken cancellationToken)
        => PostnomicOptionalPageData.LoadAsync(load, fallback, widget, logger, cancellationToken);

    /// <summary>
    /// Clamps the query-bound paging values into a sane range before they reach the API or the
    /// generated links. <c>?p=</c> and <c>?PageSize=</c> arrive straight from the URL, so a crawler
    /// (or a typo) can otherwise ask for page -3 or 100000 posts at once.
    /// </summary>
    private void NormalizePaging()
    {
        PageNumber = Math.Max(1, PageNumber);
        PageSize = Math.Clamp(PageSize, 1, MaxPageSize);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The base path for the currently resolved blog (e.g. <c>/blog/enterprise</c>).
    /// Used by the view to generate correct multi-blog links.
    /// </summary>
    public string BasePath
    {
        get
        {
            var blogName = blogResolver.ResolveBlogName(HttpContext.Request.Path.Value ?? "");
            return blogName is not null
                ? optionsMonitor.Get(blogName).BasePath
                : defaultClientOptions.Value.BasePath;
        }
    }

    /// <summary>
    /// The <see cref="PostnomicLanguageRouteStyle"/> configured for the currently resolved blog.
    /// Used together with <see cref="BasePath"/> and <see cref="Lang"/> by
    /// <see cref="PostnomicRouteBuilder"/> to generate correct links regardless of style.
    /// </summary>
    public PostnomicLanguageRouteStyle RouteStyle
    {
        get
        {
            var blogName = blogResolver.ResolveBlogName(HttpContext.Request.Path.Value ?? "");
            return blogName is not null
                ? optionsMonitor.Get(blogName).LanguageRouteStyle
                : defaultClientOptions.Value.LanguageRouteStyle;
        }
    }

    /// <summary>
    /// The <see cref="PostnomicMarkupStyle"/> configured for the currently resolved blog.
    /// Default (<see cref="PostnomicMarkupStyle.Bootstrap"/>) preserves the page's pre-theming
    /// literal Bootstrap markup byte-for-byte; opting into
    /// <see cref="PostnomicMarkupStyle.Semantic"/> switches the view to <c>pn-*</c> classes.
    /// </summary>
    public PostnomicMarkupStyle MarkupStyle
    {
        get
        {
            var blogName = blogResolver.ResolveBlogName(HttpContext.Request.Path.Value ?? "");
            return blogName is not null
                ? optionsMonitor.Get(blogName).MarkupStyle
                : defaultClientOptions.Value.MarkupStyle;
        }
    }

    /// <summary>Resolves CSS classes for each semantic role according to <see cref="MarkupStyle"/>.</summary>
    public PostnomicCssClasses Cls => new(MarkupStyle);

    /// <summary>Whether <see cref="MarkupStyle"/> is <see cref="PostnomicMarkupStyle.Semantic"/>.</summary>
    public bool Semantic => MarkupStyle == PostnomicMarkupStyle.Semantic;

    /// <summary>
    /// Returns <see langword="true"/> when at least one filter (tag, category, author, or
    /// search) is currently active.
    /// </summary>
    public bool HasActiveFilter =>
        !string.IsNullOrWhiteSpace(Tag) ||
        !string.IsNullOrWhiteSpace(Category) ||
        !string.IsNullOrWhiteSpace(Author) ||
        !string.IsNullOrWhiteSpace(Search);

    /// <summary>
    /// Clamps a pagination target into the range the current result set actually has.
    /// Never below page 1; never beyond <c>Posts.TotalPages</c> once the posts are loaded
    /// (before that the upper bound is unknown, so only the lower bound is enforced).
    /// The view calls <c>PageUrl(PageNumber - 1)</c> and <c>PageUrl(PageNumber + 1)</c>
    /// unconditionally for the prev/next arrows, which is exactly how out-of-range links escaped.
    /// </summary>
    private int ClampTargetPage(int targetPage)
    {
        var clamped = Math.Max(1, targetPage);
        var totalPages = Posts.TotalPages;
        return totalPages > 0 ? Math.Min(clamped, totalPages) : clamped;
    }

    /// <summary>
    /// Builds a route-value dictionary for a pagination link, preserving the current filter
    /// query parameters while changing only the page number.
    /// </summary>
    /// <param name="targetPage">The target page number; clamped into range.</param>
    public Dictionary<string, string?> PageRouteValues(int targetPage) => new()
    {
        ["p"] = ClampTargetPage(targetPage).ToString(),
        [nameof(PageSize)] = Math.Clamp(PageSize, 1, MaxPageSize).ToString(),
        [nameof(Tag)] = Tag,
        [nameof(Category)] = Category,
        [nameof(Author)] = Author,
        [nameof(Search)] = Search,
    };

    /// <summary>
    /// Builds a full URL for a pagination link, including the base path and query parameters.
    /// The target page is clamped into range — see <see cref="ClampTargetPage"/>.
    /// </summary>
    public string PageUrl(int targetPage)
    {
        var parts = new List<string>
        {
            $"p={ClampTargetPage(targetPage)}",
            $"PageSize={Math.Clamp(PageSize, 1, MaxPageSize)}"
        };
        if (!string.IsNullOrWhiteSpace(Tag)) parts.Add($"Tag={Uri.EscapeDataString(Tag)}");
        if (!string.IsNullOrWhiteSpace(Category)) parts.Add($"Category={Uri.EscapeDataString(Category)}");
        if (!string.IsNullOrWhiteSpace(Author)) parts.Add($"Author={Uri.EscapeDataString(Author)}");
        if (!string.IsNullOrWhiteSpace(Search)) parts.Add($"Search={Uri.EscapeDataString(Search)}");
        return $"{PostnomicRouteBuilder.BuildIndex(BasePath, RouteStyle, Lang)}?{string.Join("&", parts)}";
    }

    private IPostnomicBlogService ResolveBlogService()
    {
        var blogName = blogResolver.ResolveBlogName(HttpContext.Request.Path.Value ?? "");
        if (blogName is not null)
            return serviceProvider.GetRequiredKeyedService<IPostnomicBlogService>(blogName);
        return defaultBlogService;
    }
}

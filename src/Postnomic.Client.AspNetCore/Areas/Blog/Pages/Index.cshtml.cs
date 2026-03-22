using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;

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
    IOptionsMonitor<PostnomicClientOptions> optionsMonitor) : PageModel
{
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
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        var blogService = ResolveBlogService();

        var postsTask = blogService.GetPostsAsync(
            PageNumber, PageSize, Tag, Category, Author, Search, cancellationToken);
        var blogTask = blogService.GetBlogAsync(cancellationToken);
        var tagsTask = blogService.GetTagsAsync(cancellationToken);
        var categoriesTask = blogService.GetCategoriesAsync(cancellationToken);
        var authorsTask = blogService.GetAuthorsAsync(cancellationToken);
        var topCommentedTask = blogService.GetTopCommentedPostsAsync(cancellationToken: cancellationToken);
        var mostReadTask = blogService.GetMostReadPostsAsync(cancellationToken: cancellationToken);

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
    /// Returns <see langword="true"/> when at least one filter (tag, category, author, or
    /// search) is currently active.
    /// </summary>
    public bool HasActiveFilter =>
        !string.IsNullOrWhiteSpace(Tag) ||
        !string.IsNullOrWhiteSpace(Category) ||
        !string.IsNullOrWhiteSpace(Author) ||
        !string.IsNullOrWhiteSpace(Search);

    /// <summary>
    /// Builds a route-value dictionary for a pagination link, preserving the current filter
    /// query parameters while changing only the page number.
    /// </summary>
    /// <param name="targetPage">The target page number.</param>
    public Dictionary<string, string?> PageRouteValues(int targetPage) => new()
    {
        ["p"] = targetPage.ToString(),
        [nameof(PageSize)] = PageSize.ToString(),
        [nameof(Tag)] = Tag,
        [nameof(Category)] = Category,
        [nameof(Author)] = Author,
        [nameof(Search)] = Search,
    };

    /// <summary>
    /// Builds a full URL for a pagination link, including the base path and query parameters.
    /// </summary>
    public string PageUrl(int targetPage)
    {
        var parts = new List<string> { $"p={targetPage}", $"PageSize={PageSize}" };
        if (!string.IsNullOrWhiteSpace(Tag)) parts.Add($"Tag={Uri.EscapeDataString(Tag)}");
        if (!string.IsNullOrWhiteSpace(Category)) parts.Add($"Category={Uri.EscapeDataString(Category)}");
        if (!string.IsNullOrWhiteSpace(Author)) parts.Add($"Author={Uri.EscapeDataString(Author)}");
        if (!string.IsNullOrWhiteSpace(Search)) parts.Add($"Search={Uri.EscapeDataString(Search)}");
        return $"{BasePath}?{string.Join("&", parts)}";
    }

    private IPostnomicBlogService ResolveBlogService()
    {
        var blogName = blogResolver.ResolveBlogName(HttpContext.Request.Path.Value ?? "");
        if (blogName is not null)
            return serviceProvider.GetRequiredKeyedService<IPostnomicBlogService>(blogName);
        return defaultBlogService;
    }
}

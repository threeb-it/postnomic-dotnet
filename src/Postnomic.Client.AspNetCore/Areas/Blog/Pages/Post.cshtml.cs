using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Postnomic.Client;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.AspNetCore.Resilience;

namespace Postnomic.Client.AspNetCore.Areas.Blog.Pages;

/// <summary>
/// Page model for the blog post detail page at <c>/blog/post/{postSlug}</c>.
/// Loads the full post content together with sidebar data and handles new comment submissions.
/// </summary>
public class PostModel(
    IPostnomicBlogService defaultBlogService,
    IServiceProvider serviceProvider,
    IPostnomicBlogResolver blogResolver,
    IOptions<PostnomicClientOptions> defaultClientOptions,
    IOptionsMonitor<PostnomicClientOptions> optionsMonitor,
    IStringLocalizer<PostModel> localizer,
    ILogger<PostModel>? logger = null) : PageModel
{
    // ── Route parameter ───────────────────────────────────────────────────────

    /// <summary>The URL-friendly slug of the post being viewed.</summary>
    [BindProperty(SupportsGet = true)]
    public string PostSlug { get; set; } = string.Empty;

    /// <summary>
    /// Optional ISO-639-1 language code bound from the <c>/{lang}/</c> route segment (e.g. "de").
    /// Null when the request used the default (non-prefixed) route.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Lang { get; set; }

    // ── Page data ─────────────────────────────────────────────────────────────

    /// <summary>Full detail of the post including content, comment settings, and comments.</summary>
    public PostnomicPostDetail Post { get; private set; } = null!;

    /// <summary>Public blog metadata including server-enforced branding flag.</summary>
    public PostnomicBlogInfo? BlogInfo { get; private set; }

    /// <summary>Posts ranked by approved comment count, for the sidebar widget.</summary>
    public List<PostnomicPopularPost> TopCommented { get; private set; } = [];

    /// <summary>Posts ranked by page-view count, for the sidebar widget.</summary>
    public List<PostnomicPopularPost> MostRead { get; private set; } = [];

    /// <summary>
    /// Estimated reading time in minutes, calculated server-side at 200 words per minute.
    /// Zero when the post has no content.
    /// </summary>
    public int EstimatedReadMinutes { get; private set; }

    // ── Comment form ──────────────────────────────────────────────────────────

    /// <summary>The comment submission form, bound on POST.</summary>
    [BindProperty]
    public CommentInputModel CommentInput { get; set; } = new();

    /// <summary>
    /// Non-empty after a successful comment submission; used to display a confirmation banner.
    /// </summary>
    public string? CommentSubmitSuccessMessage { get; private set; }

    /// <summary>
    /// Non-empty after a failed comment submission; used to display an error banner.
    /// </summary>
    public string? CommentSubmitErrorMessage { get; private set; }

    // ── GET handler ───────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the post detail and sidebar data. Returns a 404 result when the post does not exist.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        return await LoadPostAsync(cancellationToken);
    }

    // ── POST handler (comment submission) ─────────────────────────────────────

    /// <summary>
    /// Handles comment form submissions. Validates the form, submits the comment via the
    /// <see cref="IPostnomicBlogService"/>, and re-renders the page with a success or error banner.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        // Always reload the post regardless of validation outcome.
        var loadResult = await LoadPostAsync(cancellationToken);
        if (loadResult is NotFoundResult) return loadResult;

        // Enforce dynamic required fields based on blog settings.
        ValidateCommentFields();

        if (!ModelState.IsValid)
        {
            CommentSubmitErrorMessage = localizer["CommentSubmitValidationError"];
            return Page();
        }

        var blogService = ResolveBlogService();

        var request = new PostnomicCreateCommentRequest
        {
            Body = CommentInput.Body,
            Subject = string.IsNullOrWhiteSpace(CommentInput.Subject) ? null : CommentInput.Subject,
            AuthorFirstname = string.IsNullOrWhiteSpace(CommentInput.Firstname) ? null : CommentInput.Firstname,
            AuthorLastname = string.IsNullOrWhiteSpace(CommentInput.Lastname) ? null : CommentInput.Lastname,
            AuthorEmail = string.IsNullOrWhiteSpace(CommentInput.Email) ? null : CommentInput.Email,
            AuthorPhone = string.IsNullOrWhiteSpace(CommentInput.Phone) ? null : CommentInput.Phone,
        };

        var comment = await blogService.CreateCommentAsync(PostSlug, request, cancellationToken);

        if (comment is null)
        {
            CommentSubmitErrorMessage = localizer["CommentSubmitError"];
        }
        else
        {
            CommentInput = new CommentInputModel();
            CommentSubmitSuccessMessage = Post.CommentRequireModeration
                ? localizer["CommentSubmitModerated"]
                : localizer["CommentSubmitSuccess"];
        }

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
    /// Whether to show the Postnomic branding footer below the post content.
    /// Server-enforced value from the API takes precedence over client configuration.
    /// </summary>
    public bool ShowBranding
    {
        get
        {
            // Server-enforced value takes precedence over client config
            if (BlogInfo is not null)
                return BlogInfo.ShowBranding;

            var blogName = blogResolver.ResolveBlogName(HttpContext.Request.Path.Value ?? "");
            return blogName is not null
                ? optionsMonitor.Get(blogName).ShowBranding
                : defaultClientOptions.Value.ShowBranding;
        }
    }

    /// <summary>
    /// The canonical URL for this post. When the API supplies an absolute canonical (the post's
    /// primary blog — set for cross-posted posts), use it; otherwise the host-relative default
    /// {BasePath}/post/{slug}.
    /// </summary>
    public string CanonicalUrl => !string.IsNullOrWhiteSpace(Post?.CanonicalUrl)
        ? Post!.CanonicalUrl!
        : PostnomicRouteBuilder.BuildPost(BasePath, RouteStyle, lang: null, PostSlug);

    /// <summary>
    /// hreflang alternates: one (languageCode, url) per available language. The blog default
    /// language (AvailableLanguages first, else Post.Language) maps to the canonical URL; others
    /// get the language segment placed according to <see cref="RouteStyle"/>. Empty when the
    /// post has no AvailableLanguages.
    /// </summary>
    public IReadOnlyList<(string Language, string Url)> AlternateLanguageUrls
    {
        get
        {
            var langs = Post?.AvailableLanguages ?? [];
            if (langs.Count == 0) return [];
            var defaultLang = langs.FirstOrDefault() ?? Post!.Language;
            return langs.Select(code => (code,
                PostnomicRouteBuilder.BuildPost(
                    BasePath,
                    RouteStyle,
                    string.Equals(code, defaultLang, StringComparison.OrdinalIgnoreCase) ? null : code,
                    PostSlug))).ToList();
        }
    }

    /// <summary>
    /// This post's host-supplied hreflang alternates, resolved once while the page loads and
    /// passed to <see cref="Postnomic.Client.AspNetCore.Seo.PostnomicSeo.ForPost"/>.
    /// <para>
    /// This is page <b>output</b>, not an input seam: set it by registering an
    /// <see cref="IPostnomicAlternateUrlProvider"/> with
    /// <c>services.AddPostnomicAlternateUrlProvider&lt;TProvider&gt;()</c>, which the SDK resolves
    /// from DI for the blog this request belongs to. Null when no provider (and no obsolete
    /// <see cref="PostnomicClientOptions.AlternateUrlResolver"/>) is configured for this blog, or
    /// when the provider returns null for this post — in either case
    /// <c>PostnomicSeoBuilder.ForPost</c> falls back to its composed alternates, unaffected.
    /// </para>
    /// </summary>
    public IReadOnlyList<(string Language, string Url)>? AlternateUrls { get; private set; }

    /// <summary>
    /// Loads the post and its surrounding page data in parallel.
    /// <para>
    /// The post itself is essential: <see langword="null"/> still means 404, and a thrown failure
    /// still fails the request. Everything else — blog metadata (only feeds the optional branding
    /// flag, which falls back to client options) and the two sidebar widgets — is decorative and
    /// degrades to empty rather than 500-ing a page whose actual content loaded fine.
    /// </para>
    /// </summary>
    private async Task<IActionResult> LoadPostAsync(CancellationToken cancellationToken)
    {
        var blogService = ResolveBlogService();

        // Essential — no post, no page.
        var postTask = blogService.GetPostAsync(PostSlug, language: Lang, cancellationToken: cancellationToken);

        // Decorative — started in parallel, each degrading independently.
        var blogInfoTask = Optional<PostnomicBlogInfo?>(
            () => blogService.GetBlogAsync(cancellationToken), null, "blog-info", cancellationToken);
        var topCommentedTask = Optional(
            () => blogService.GetTopCommentedPostsAsync(cancellationToken: cancellationToken), new List<PostnomicPopularPost>(), "top-commented", cancellationToken);
        var mostReadTask = Optional(
            () => blogService.GetMostReadPostsAsync(cancellationToken: cancellationToken), new List<PostnomicPopularPost>(), "most-read", cancellationToken);

        await Task.WhenAll(postTask, blogInfoTask, topCommentedTask, mostReadTask);

        var post = await postTask;
        if (post is null) return NotFound();

        Post = post;
        BlogInfo = await blogInfoTask;
        TopCommented = await topCommentedTask;
        MostRead = await mostReadTask;
        EstimatedReadMinutes = CalculateReadTime(post.Content);
        AlternateUrls = await Optional<IReadOnlyList<(string Language, string Url)>?>(
            () => ResolveAlternateUrlsAsync(post, cancellationToken), null, "alternate-urls", cancellationToken);

        return Page();
    }

    private Task<T> Optional<T>(Func<Task<T>> load, T fallback, string widget, CancellationToken cancellationToken)
        => PostnomicOptionalPageData.LoadAsync(load, fallback, widget, logger, cancellationToken);

    /// <summary>
    /// Resolves this post's hreflang alternates for the blog this request belongs to, using the
    /// same precedence as the Blazor hosting model so both emit identical SEO output. See
    /// <see cref="PostnomicAlternateUrls.ResolveAsync"/>.
    /// </summary>
    private async Task<IReadOnlyList<(string Language, string Url)>?> ResolveAlternateUrlsAsync(
        PostnomicPostDetail post,
        CancellationToken cancellationToken)
    {
        var blogName = blogResolver.ResolveBlogName(HttpContext.Request.Path.Value ?? "");
        var options = blogName is not null
            ? optionsMonitor.Get(blogName)
            : defaultClientOptions.Value;

        return await PostnomicAlternateUrls.ResolveAsync(
            serviceProvider, options, blogName, post, cancellationToken);
    }

    private void ValidateCommentFields()
    {
        if (Post is null) return;

        if (Post.CommentRequireSubject && string.IsNullOrWhiteSpace(CommentInput.Subject))
            ModelState.AddModelError($"{nameof(CommentInput)}.{nameof(CommentInput.Subject)}", localizer["FieldSubjectRequired"]);

        if (Post.CommentRequireFirstname && string.IsNullOrWhiteSpace(CommentInput.Firstname))
            ModelState.AddModelError($"{nameof(CommentInput)}.{nameof(CommentInput.Firstname)}", localizer["FieldFirstNameRequired"]);

        if (Post.CommentRequireLastname && string.IsNullOrWhiteSpace(CommentInput.Lastname))
            ModelState.AddModelError($"{nameof(CommentInput)}.{nameof(CommentInput.Lastname)}", localizer["FieldLastNameRequired"]);

        if (Post.CommentRequireEmail && string.IsNullOrWhiteSpace(CommentInput.Email))
            ModelState.AddModelError($"{nameof(CommentInput)}.{nameof(CommentInput.Email)}", localizer["FieldEmailRequired"]);

        if (Post.CommentRequirePhone && string.IsNullOrWhiteSpace(CommentInput.Phone))
            ModelState.AddModelError($"{nameof(CommentInput)}.{nameof(CommentInput.Phone)}", localizer["FieldPhoneRequired"]);
    }

    private static int CalculateReadTime(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return 0;
        var plainText = Regex.Replace(html, "<[^>]+>", " ");
        var wordCount = plainText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)Math.Ceiling(wordCount / 200.0));
    }

    private IPostnomicBlogService ResolveBlogService()
    {
        var blogName = blogResolver.ResolveBlogName(HttpContext.Request.Path.Value ?? "");
        if (blogName is not null)
            return serviceProvider.GetRequiredKeyedService<IPostnomicBlogService>(blogName);
        return defaultBlogService;
    }
}

/// <summary>
/// Form input model for the comment submission form on a blog post page.
/// </summary>
public class CommentInputModel
{
    /// <summary>The comment body text. Always required.</summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Comment body is required.")]
    [System.ComponentModel.DataAnnotations.MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    /// <summary>Optional comment subject line.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? Subject { get; set; }

    /// <summary>Commenter's first name (required when blog setting demands it).</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? Firstname { get; set; }

    /// <summary>Commenter's last name (required when blog setting demands it).</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? Lastname { get; set; }

    /// <summary>Commenter's email address (required when blog setting demands it).</summary>
    [System.ComponentModel.DataAnnotations.EmailAddress]
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? Email { get; set; }

    /// <summary>Commenter's phone number (required when blog setting demands it).</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(30)]
    public string? Phone { get; set; }
}

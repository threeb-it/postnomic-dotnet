using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;

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
    IOptionsMonitor<PostnomicClientOptions> optionsMonitor) : PageModel
{
    // ── Route parameter ───────────────────────────────────────────────────────

    /// <summary>The URL-friendly slug of the post being viewed.</summary>
    [BindProperty(SupportsGet = true)]
    public string PostSlug { get; set; } = string.Empty;

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
            CommentSubmitErrorMessage = "Please correct the errors below and try again.";
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
            CommentSubmitErrorMessage = "Your comment could not be submitted. Please try again later.";
        }
        else
        {
            CommentInput = new CommentInputModel();
            CommentSubmitSuccessMessage = Post.CommentRequireModeration
                ? "Thank you! Your comment has been submitted and is awaiting moderation."
                : "Thank you! Your comment has been published.";
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

    private async Task<IActionResult> LoadPostAsync(CancellationToken cancellationToken)
    {
        var blogService = ResolveBlogService();

        var postTask = blogService.GetPostAsync(PostSlug, cancellationToken);
        var blogInfoTask = blogService.GetBlogAsync(cancellationToken);
        var topCommentedTask = blogService.GetTopCommentedPostsAsync(cancellationToken: cancellationToken);
        var mostReadTask = blogService.GetMostReadPostsAsync(cancellationToken: cancellationToken);

        await Task.WhenAll(postTask, blogInfoTask, topCommentedTask, mostReadTask);

        var post = await postTask;
        if (post is null) return NotFound();

        Post = post;
        BlogInfo = await blogInfoTask;
        TopCommented = await topCommentedTask;
        MostRead = await mostReadTask;
        EstimatedReadMinutes = CalculateReadTime(post.Content);

        return Page();
    }

    private void ValidateCommentFields()
    {
        if (Post is null) return;

        if (Post.CommentRequireSubject && string.IsNullOrWhiteSpace(CommentInput.Subject))
            ModelState.AddModelError($"{nameof(CommentInput)}.{nameof(CommentInput.Subject)}", "Subject is required.");

        if (Post.CommentRequireFirstname && string.IsNullOrWhiteSpace(CommentInput.Firstname))
            ModelState.AddModelError($"{nameof(CommentInput)}.{nameof(CommentInput.Firstname)}", "First name is required.");

        if (Post.CommentRequireLastname && string.IsNullOrWhiteSpace(CommentInput.Lastname))
            ModelState.AddModelError($"{nameof(CommentInput)}.{nameof(CommentInput.Lastname)}", "Last name is required.");

        if (Post.CommentRequireEmail && string.IsNullOrWhiteSpace(CommentInput.Email))
            ModelState.AddModelError($"{nameof(CommentInput)}.{nameof(CommentInput.Email)}", "Email address is required.");

        if (Post.CommentRequirePhone && string.IsNullOrWhiteSpace(CommentInput.Phone))
            ModelState.AddModelError($"{nameof(CommentInput)}.{nameof(CommentInput.Phone)}", "Phone number is required.");
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

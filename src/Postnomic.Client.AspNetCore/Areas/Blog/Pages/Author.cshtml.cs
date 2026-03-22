using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.AspNetCore.Areas.Blog.Pages;

/// <summary>
/// Page model for the author profile page at <c>/blog/author/{authorSlug}</c>.
/// Loads the author's full profile including bio, social links, and recent posts.
/// </summary>
public class AuthorModel(
    IPostnomicBlogService defaultBlogService,
    IServiceProvider serviceProvider,
    IPostnomicBlogResolver blogResolver,
    IOptions<PostnomicClientOptions> defaultClientOptions,
    IOptionsMonitor<PostnomicClientOptions> optionsMonitor) : PageModel
{
    /// <summary>The URL-friendly slug of the author being viewed.</summary>
    [BindProperty(SupportsGet = true)]
    public string AuthorSlug { get; set; } = string.Empty;

    /// <summary>The full author profile loaded from the API.</summary>
    public PostnomicAuthorProfile Profile { get; private set; } = null!;

    /// <summary>The base path for the currently resolved blog (e.g. <c>/blog/enterprise</c>).</summary>
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
    /// Whether to show the Postnomic branding in the sidebar.
    /// Falls back to client options because the Author page does not fetch blog info.
    /// </summary>
    public bool ShowBranding
    {
        get
        {
            var blogName = blogResolver.ResolveBlogName(HttpContext.Request.Path.Value ?? "");
            return blogName is not null
                ? optionsMonitor.Get(blogName).ShowBranding
                : defaultClientOptions.Value.ShowBranding;
        }
    }

    /// <summary>
    /// Loads the author profile. Returns a 404 result when the author does not exist.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        var blogService = ResolveBlogService();
        var profile = await blogService.GetAuthorProfileAsync(AuthorSlug, cancellationToken);
        if (profile is null) return NotFound();

        Profile = profile;
        return Page();
    }

    private IPostnomicBlogService ResolveBlogService()
    {
        var blogName = blogResolver.ResolveBlogName(HttpContext.Request.Path.Value ?? "");
        if (blogName is not null)
            return serviceProvider.GetRequiredKeyedService<IPostnomicBlogService>(blogName);
        return defaultBlogService;
    }
}

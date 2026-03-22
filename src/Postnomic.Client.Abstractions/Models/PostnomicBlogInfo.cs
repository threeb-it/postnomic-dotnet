namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// Public metadata for a Postnomic blog returned by the public API.
/// </summary>
public record PostnomicBlogInfo
{
    /// <summary>
    /// The display name of the blog.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The URL-friendly slug that uniquely identifies the blog.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// An optional short description of the blog. <see langword="null"/> when no description has been set.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The default layout style for the blog's post listing page (e.g. "Default", "Masonry").
    /// </summary>
    public string DefaultLayout { get; init; } = "Default";

    /// <summary>
    /// Server-enforced branding flag. When <see langword="true"/>, the client SDK should
    /// display a "Powered by Postnomic" promotional banner below each blog post.
    /// The API sets this to <see langword="true"/> for Free-tier blogs and <see langword="false"/>
    /// for paid plans (Plus, Pro, Enterprise).
    /// </summary>
    public bool ShowBranding { get; init; }
}

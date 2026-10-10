namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// The data for a new blog, passed to <see cref="IPostnomicAuthoringService.CreateBlogAsync"/>.
/// </summary>
public record PostnomicCreateBlogRequest
{
    /// <summary>The blog's display name (at most 200 characters).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The blog's slug: lowercase letters and digits separated by single hyphens (e.g.
    /// <c>my-blog</c>). It becomes the blog's address in the public API and the folder its media is
    /// stored under. Must be unique across the platform; a taken or reserved slug is refused with 409.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>The base URL the blog's posts are canonically published under, e.g. <c>https://example.com/blog</c>.</summary>
    public required string CanonicalBaseUrl { get; init; }

    /// <summary>An optional description (at most 1000 characters).</summary>
    public string? Description { get; init; }

    /// <summary>
    /// The blog's default language as a two-letter lower-case ISO-639-1 code. Posts are written in
    /// this language; other languages are added as translations. Defaults to <c>en</c>. It cannot be
    /// changed once the blog exists.
    /// </summary>
    public string DefaultLanguage { get; init; } = "en";

    /// <summary>The blog's post-listing layout: <c>Default</c> or <c>Masonry</c>.</summary>
    public string DefaultLayout { get; init; } = "Default";

    /// <summary>The maximum number of tags shown in the blog's sidebar (0-200; <c>0</c> means all).</summary>
    public int SidebarTagLimit { get; init; } = 15;
}

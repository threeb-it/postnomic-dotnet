namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// A blog as the management API describes it to one of its members — the authoring counterpart of
/// <see cref="PostnomicBlogInfo"/>, which is what an anonymous reader sees.
/// </summary>
public record PostnomicBlog
{
    /// <summary>
    /// The blog's public ID (a GUID). This is the value <see cref="PostnomicClientOptions.BlogId"/>
    /// expects.
    /// </summary>
    public required string PublicId { get; init; }

    /// <summary>The blog's display name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The blog's slug: its address in the public API and the value
    /// <see cref="PostnomicClientOptions.BlogSlug"/> expects.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>An optional description of the blog.</summary>
    public string? Description { get; init; }

    /// <summary>The base URL the blog's posts are canonically published under.</summary>
    public string? CanonicalBaseUrl { get; init; }

    /// <summary>
    /// The blog's default language as an ISO-639-1 code (e.g. <c>en</c>, <c>de</c>). A post's own
    /// title, slug and content are in this language; every other language is a translation, and
    /// the API refuses a translation in this language. Reads as <c>en</c> — the platform default —
    /// from an API that does not report it yet.
    /// </summary>
    public string DefaultLanguage { get; init; } = "en";

    /// <summary>The blog's post-listing layout (<c>Default</c> or <c>Masonry</c>).</summary>
    public string DefaultLayout { get; init; } = "Default";

    /// <summary>The maximum number of tags shown in the blog's sidebar; <c>0</c> means all.</summary>
    public int SidebarTagLimit { get; init; } = 15;

    /// <summary>
    /// The slug a rename in progress is moving this blog to, or <see langword="null"/>. Set only
    /// when a rename was started and did not finish; repeating that rename completes it.
    /// </summary>
    public string? PendingSlug { get; init; }

    /// <summary>The UTC date/time the blog was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>The UTC date/time the blog's settings were last changed, if ever.</summary>
    public DateTime? UpdatedAt { get; init; }
}

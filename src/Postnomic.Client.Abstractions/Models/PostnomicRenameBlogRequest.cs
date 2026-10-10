namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// What to rename, passed to <see cref="IPostnomicAuthoringService.RenameBlogAsync"/>. Supply
/// <see cref="Name"/>, <see cref="Slug"/>, or both; a property left <see langword="null"/> keeps its
/// current value.
/// </summary>
public record PostnomicRenameBlogRequest
{
    /// <summary>The new display name, or <see langword="null"/> to keep the current one.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// The new slug — lowercase letters and digits separated by single hyphens — or
    /// <see langword="null"/> to keep the current one.
    /// </summary>
    public string? Slug { get; init; }
}

/// <summary>
/// What a rename did, returned by <see cref="IPostnomicAuthoringService.RenameBlogAsync"/>.
/// </summary>
public record PostnomicBlogRenameResult
{
    /// <summary>The blog as it is now.</summary>
    public required PostnomicBlog Blog { get; init; }

    /// <summary>The slug the blog had before, or <see langword="null"/> when the slug did not change.</summary>
    public string? PreviousSlug { get; init; }

    /// <summary>
    /// Until when (UTC) the public API keeps answering requests made with <see cref="PreviousSlug"/>.
    /// Update <see cref="PostnomicClientOptions.BlogSlug"/> before then.
    /// </summary>
    public DateTime? FormerSlugExpiresAt { get; init; }

    /// <summary>Media files present under the new slug after the copy.</summary>
    public int MediaFiles { get; init; }

    /// <summary>Total size of those media files in bytes.</summary>
    public long MediaBytes { get; init; }

    /// <summary>Posts whose stored media references were rewritten to the new slug.</summary>
    public int PostsRewritten { get; init; }

    /// <summary>Post translations whose stored media references were rewritten to the new slug.</summary>
    public int TranslationsRewritten { get; init; }
}

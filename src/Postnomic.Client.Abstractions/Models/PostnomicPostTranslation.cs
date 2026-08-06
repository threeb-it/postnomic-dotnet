namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// A post's translation into a language other than the blog's default, as returned by
/// <see cref="IPostnomicAuthoringService.GetPostTranslationsAsync"/> and
/// <see cref="IPostnomicAuthoringService.SetPostTranslationAsync"/>.
/// </summary>
public record PostnomicPostTranslation
{
    /// <summary>The ISO-639-1 language code (lower-case).</summary>
    public required string Language { get; init; }

    /// <summary>The post title in this language.</summary>
    public required string Title { get; init; }

    /// <summary>The URL-friendly slug in this language.</summary>
    public required string Slug { get; init; }

    /// <summary>The full body content in this language (HTML). <see langword="null"/> when not set.</summary>
    public string? Content { get; init; }

    /// <summary>Optional short summary/teaser in this language. <see langword="null"/> when not set.</summary>
    public string? Excerpt { get; init; }

    /// <summary>The UTC date/time this translation was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>The UTC date/time this translation was last updated, if it has been updated since creation.</summary>
    public DateTime? UpdatedAt { get; init; }
}

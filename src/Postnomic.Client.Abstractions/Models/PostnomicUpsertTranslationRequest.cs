namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// The request body used to create or update a post's translation via
/// <see cref="IPostnomicAuthoringService.SetPostTranslationAsync"/>. The language is taken from
/// the method's <c>language</c> parameter, not this body.
/// </summary>
public record PostnomicUpsertTranslationRequest
{
    /// <summary>The post title in this language.</summary>
    public required string Title { get; init; }

    /// <summary>The URL-friendly slug in this language. Must be unique within the blog for this language.</summary>
    public required string Slug { get; init; }

    /// <summary>The full body content in this language (HTML).</summary>
    public string? Content { get; init; }

    /// <summary>Optional short summary/teaser in this language.</summary>
    public string? Excerpt { get; init; }
}

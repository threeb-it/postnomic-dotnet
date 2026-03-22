namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// Represents a category used to organise blog posts, together with the number
/// of published posts assigned to it.
/// </summary>
public record PostnomicCategory
{
    /// <summary>
    /// The display name of the category (e.g. "Tutorials").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The URL-friendly slug of the category (e.g. "tutorials").
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// The number of published posts that belong to this category on the blog.
    /// </summary>
    public int PostCount { get; init; }
}

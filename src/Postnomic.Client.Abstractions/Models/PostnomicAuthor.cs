namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// Represents a blog author together with the count of their published posts.
/// </summary>
public record PostnomicAuthor
{
    /// <summary>
    /// The full display name of the author (first name and last name combined).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The URL-friendly slug of the author, used for linking to the author profile page.
    /// </summary>
    public string? Slug { get; init; }

    /// <summary>
    /// The number of published posts written by this author on the blog.
    /// </summary>
    public int PostCount { get; init; }
}

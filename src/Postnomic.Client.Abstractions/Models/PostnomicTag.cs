namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// Represents a tag used to categorise blog posts, together with the number
/// of published posts that carry it.
/// </summary>
public record PostnomicTag
{
    /// <summary>
    /// The display name of the tag (e.g. "C# Tips").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The URL-friendly slug of the tag (e.g. "csharp-tips").
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// The number of published posts that are assigned this tag on the blog.
    /// </summary>
    public int PostCount { get; init; }
}

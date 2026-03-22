namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// Wraps a paginated collection of items returned by the Postnomic API, together with the
/// paging metadata needed to render navigation controls.
/// </summary>
/// <typeparam name="T">The type of item in the result set.</typeparam>
public record PostnomicPagedResult<T>
{
    /// <summary>
    /// The items on the current page. Never <see langword="null"/>; may be empty when the
    /// requested page exceeds the total number of pages.
    /// </summary>
    public required ICollection<T> Items { get; init; }

    /// <summary>
    /// The 1-based index of the current page.
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// The maximum number of items per page that was requested.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// The total number of items across all pages that match the applied filters.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// The total number of pages, calculated as <c>ceil(TotalCount / PageSize)</c>.
    /// </summary>
    public int TotalPages { get; init; }
}

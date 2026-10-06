namespace Postnomic.Client.Abstractions;

/// <summary>
/// Computes the <c>rel</c> attribute value for SDK-rendered links. Pure and framework-free — used by
/// both the Razor Pages area and the Blazor components so the two surfaces stay in lockstep.
/// </summary>
public static class PostnomicLinkRel
{
    /// <summary>
    /// Merges <paramref name="existingRel"/> with <paramref name="filterLinkRel"/> (see
    /// <see cref="PostnomicClientOptions.FilterLinkRel"/>): tokens split on whitespace, duplicates
    /// removed case-insensitively, first spelling and order preserved.
    /// </summary>
    /// <param name="filterLinkRel">The configured <see cref="PostnomicClientOptions.FilterLinkRel"/>.</param>
    /// <param name="existingRel">A <c>rel</c> the link already carries, if any.</param>
    /// <returns>
    /// The merged value, or <see langword="null"/> when there are no tokens at all — so a Razor or
    /// Blazor <c>rel="@..."</c> attribute is omitted entirely and the markup is unchanged.
    /// </returns>
    public static string? ForFilterLink(string? filterLinkRel, string? existingRel = null)
    {
        if (string.IsNullOrWhiteSpace(filterLinkRel))
            return string.IsNullOrWhiteSpace(existingRel) ? null : existingRel;

        var tokens = new List<string>();
        foreach (var token in Split(existingRel).Concat(Split(filterLinkRel)))
        {
            if (!tokens.Contains(token, StringComparer.OrdinalIgnoreCase))
                tokens.Add(token);
        }

        return tokens.Count == 0 ? null : string.Join(' ', tokens);
    }

    private static string[] Split(string? value) =>
        value?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) ?? [];
}

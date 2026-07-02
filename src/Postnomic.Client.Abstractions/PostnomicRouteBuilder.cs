namespace Postnomic.Client.Abstractions;

/// <summary>
/// Builds and parses blog route URLs according to a configured <see cref="PostnomicLanguageRouteStyle"/>.
/// Shared by <c>Postnomic.Client.AspNetCore</c> and <c>Postnomic.Client.Blazor</c> so route
/// generation and resolution stay consistent across hosting models.
/// </summary>
public static class PostnomicRouteBuilder
{
    /// <summary>Builds the URL for the blog index page.</summary>
    public static string BuildIndex(string basePath, PostnomicLanguageRouteStyle style, string? lang)
        => Compose(basePath, style, lang, tail: "");

    /// <summary>Builds the URL for a single post page.</summary>
    public static string BuildPost(string basePath, PostnomicLanguageRouteStyle style, string? lang, string postSlug)
        => Compose(basePath, style, lang, tail: $"/post/{postSlug}");

    /// <summary>Builds the URL for an author page.</summary>
    public static string BuildAuthor(string basePath, PostnomicLanguageRouteStyle style, string? lang, string authorSlug)
        => Compose(basePath, style, lang, tail: $"/author/{authorSlug}");

    private static string Compose(string basePath, PostnomicLanguageRouteStyle style, string? lang, string tail)
    {
        var bp = "/" + basePath.Trim('/');
        var hasLang = !string.IsNullOrEmpty(lang) && style != PostnomicLanguageRouteStyle.None;
        return style switch
        {
            PostnomicLanguageRouteStyle.Prefix when hasLang => $"/{lang}{bp}{tail}",
            PostnomicLanguageRouteStyle.Suffix when hasLang => $"{bp}/{lang}{tail}",
            _ => $"{bp}{tail}",
        };
    }

    /// <summary>
    /// Extracts the language segment from a request path, if present for the given
    /// <paramref name="style"/>. Returns <see langword="null"/> when the style carries no
    /// language segment (<see cref="PostnomicLanguageRouteStyle.None"/>) or none is present.
    /// </summary>
    public static string? ExtractLang(string requestPath, string basePath, PostnomicLanguageRouteStyle style)
    {
        var segs = Segments(requestPath);
        var bpSegs = Segments(basePath);
        return style switch
        {
            PostnomicLanguageRouteStyle.Prefix
                when segs.Length > bpSegs.Length && IsLang(segs[0]) && MatchesSegments(segs, 1, bpSegs)
                => segs[0],
            PostnomicLanguageRouteStyle.Suffix
                when segs.Length > bpSegs.Length && MatchesSegments(segs, 0, bpSegs) && IsLang(segs[bpSegs.Length])
                => segs[bpSegs.Length],
            _ => null,
        };
    }

    /// <summary>
    /// Resolver helper: returns <see langword="true"/> when <paramref name="requestPath"/> targets
    /// this blog's <paramref name="basePath"/>, ignoring a leading language segment in
    /// <see cref="PostnomicLanguageRouteStyle.Prefix"/> mode.
    /// </summary>
    public static bool MatchesBlog(string requestPath, string basePath, PostnomicLanguageRouteStyle style)
    {
        var segs = Segments(requestPath);
        var bpSegs = Segments(basePath);
        return style switch
        {
            PostnomicLanguageRouteStyle.Prefix => MatchesSegments(segs, 0, bpSegs)
                || (segs.Length >= 1 && IsLang(segs[0]) && MatchesSegments(segs, 1, bpSegs)),
            _ => MatchesSegments(segs, 0, bpSegs),
        };
    }

    private static string[] Segments(string path)
    {
        var withoutQuery = path.Split('?', 2)[0];
        return withoutQuery.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool MatchesSegments(string[] segs, int offset, string[] baseSegs)
    {
        if (baseSegs.Length == 0 || segs.Length < offset + baseSegs.Length)
            return false;

        for (var i = 0; i < baseSegs.Length; i++)
        {
            if (!string.Equals(segs[offset + i], baseSegs[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static bool IsLang(string s) => s.Length == 2 && char.IsLetter(s[0]) && char.IsLetter(s[1]);
}

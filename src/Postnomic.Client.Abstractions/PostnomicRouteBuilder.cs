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
        var segs = requestPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var bp = basePath.Trim('/');
        return style switch
        {
            PostnomicLanguageRouteStyle.Prefix when segs.Length >= 2 && segs[1] == bp && IsLang(segs[0]) => segs[0],
            PostnomicLanguageRouteStyle.Suffix when segs.Length >= 2 && segs[0] == bp && IsLang(segs[1]) => segs[1],
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
        var segs = requestPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var bp = basePath.Trim('/');
        return style switch
        {
            PostnomicLanguageRouteStyle.Prefix => (segs.Length >= 1 && segs[0] == bp)
                || (segs.Length >= 2 && IsLang(segs[0]) && segs[1] == bp),
            _ => segs.Length >= 1 && segs[0] == bp,
        };
    }

    private static bool IsLang(string s) => s.Length == 2 && char.IsLetter(s[0]) && char.IsLetter(s[1]);
}

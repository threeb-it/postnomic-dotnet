namespace Postnomic.Client.Abstractions;

/// <summary>
/// Shared mapping from a Bootstrap Icons class (e.g. <c>"bi bi-person"</c>) to a small, distinct
/// inline SVG glyph, used by every Semantic-mode icon rendering surface — Blazor components and
/// the AspNetCore Razor Pages Blog Area alike. Pure and framework-free (no dependency on
/// <c>Microsoft.AspNetCore.Components</c>) so both rendering hosts can consume it without pulling
/// in the other's framework — kept in one place rather than duplicated per host so Semantic-mode
/// consumers (e.g. OutaStory) get visually distinguishable icons instead of one generic placeholder
/// repeated everywhere.
/// </summary>
/// <remarks>
/// The glyphs are small hand-drawn approximations, not pixel-perfect reproductions of Bootstrap
/// Icons — they only need to be visually distinct and recognizable, since Semantic mode carries no
/// dependency on the Bootstrap Icons font/CSS. Bootstrap-mode output (<c>&lt;i class="bi …"&gt;</c>)
/// is untouched by this type; only the Semantic <c>&lt;svg&gt;</c> branch consumes it.
/// </remarks>
public static class PostnomicIconSvg
{
    private const string SvgOpen =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"14\" height=\"14\" viewBox=\"0 0 16 16\" fill=\"currentColor\" aria-hidden=\"true\">";

    private const string SvgClose = "</svg>";

    /// <summary>The generic fallback glyph used for any bootstrap-icon class not in <see cref="s_paths"/>.</summary>
    private const string FallbackPath = "<circle cx=\"8\" cy=\"8\" r=\"6\" />";

    private static readonly Dictionary<string, string> s_paths = new(StringComparer.Ordinal)
    {
        ["bi bi-person"] =
            "<circle cx=\"8\" cy=\"5\" r=\"3\" />" +
            "<path d=\"M2 15c0-3.3 3-5 6-5s6 1.7 6 5v1H2z\" />",

        ["bi bi-person-circle"] =
            "<circle cx=\"8\" cy=\"8\" r=\"6.5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.2\" />" +
            "<circle cx=\"8\" cy=\"6.3\" r=\"2\" />" +
            "<path d=\"M3.8 12.6c.6-2.3 2.4-3.6 4.2-3.6s3.6 1.3 4.2 3.6A6.47 6.47 0 0 1 8 14.5a6.47 6.47 0 0 1-4.2-1.9Z\" />",

        ["bi bi-people"] =
            "<circle cx=\"5.5\" cy=\"5\" r=\"2.3\" />" +
            "<path d=\"M1 14c0-2.8 2-4.3 4.5-4.3S10 11.2 10 14v.5H1Z\" />" +
            "<circle cx=\"11.3\" cy=\"5.5\" r=\"1.8\" opacity=\"0.55\" />" +
            "<path d=\"M9.6 9.9c1.9.3 3.6 1.7 3.9 3.9.05.3.05.5.05.7H11\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.2\" opacity=\"0.55\" />",

        ["bi bi-calendar"] =
            "<rect x=\"1\" y=\"2.5\" width=\"14\" height=\"12\" rx=\"1\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.3\" />" +
            "<line x1=\"1\" y1=\"6\" x2=\"15\" y2=\"6\" stroke=\"currentColor\" stroke-width=\"1.3\" />" +
            "<line x1=\"4\" y1=\"1\" x2=\"4\" y2=\"4\" stroke=\"currentColor\" stroke-width=\"1.3\" stroke-linecap=\"round\" />" +
            "<line x1=\"12\" y1=\"1\" x2=\"12\" y2=\"4\" stroke=\"currentColor\" stroke-width=\"1.3\" stroke-linecap=\"round\" />",

        ["bi bi-clock"] =
            "<circle cx=\"8\" cy=\"8\" r=\"6.5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.3\" />" +
            "<line x1=\"8\" y1=\"8\" x2=\"8\" y2=\"4\" stroke=\"currentColor\" stroke-width=\"1.3\" stroke-linecap=\"round\" />" +
            "<line x1=\"8\" y1=\"8\" x2=\"11\" y2=\"9.5\" stroke=\"currentColor\" stroke-width=\"1.3\" stroke-linecap=\"round\" />",

        ["bi bi-chat"] =
            "<path d=\"M2 3h12a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1H6l-3 3v-3H2a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1Z\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.2\" />",

        ["bi bi-chat-dots"] =
            "<path d=\"M2 3h12a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1H6l-3 3v-3H2a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1Z\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.2\" />" +
            "<circle cx=\"5\" cy=\"7.5\" r=\"0.8\" />" +
            "<circle cx=\"8\" cy=\"7.5\" r=\"0.8\" />" +
            "<circle cx=\"11\" cy=\"7.5\" r=\"0.8\" />",

        ["bi bi-chat-fill"] =
            "<path d=\"M2 3h12a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1H6l-3 3v-3H2a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1Z\" />",

        ["bi bi-funnel"] =
            "<path d=\"M1 2h14l-5.5 6.5V14l-3-1.5V8.5Z\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.1\" stroke-linejoin=\"round\" />",

        ["bi bi-search"] =
            "<circle cx=\"6.5\" cy=\"6.5\" r=\"4.5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.4\" />" +
            "<line x1=\"9.9\" y1=\"9.9\" x2=\"14.5\" y2=\"14.5\" stroke=\"currentColor\" stroke-width=\"1.4\" stroke-linecap=\"round\" />",

        ["bi bi-eye"] =
            "<path d=\"M1 8s2.7-5 7-5 7 5 7 5-2.7 5-7 5-7-5-7-5Z\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.2\" />" +
            "<circle cx=\"8\" cy=\"8\" r=\"2\" />",

        ["bi bi-eye-fill"] =
            "<path fill-rule=\"evenodd\" d=\"M1 8s2.7-5 7-5 7 5 7 5-2.7 5-7 5-7-5-7-5Zm7 2.5A2.5 2.5 0 1 0 8 5.5a2.5 2.5 0 0 0 0 5Z\" />",

        ["bi bi-tags"] =
            "<path d=\"M9.6 1.2 14 5.6a1 1 0 0 1 0 1.4l-6 6a1 1 0 0 1-1.4 0L1.2 7.6a1 1 0 0 1-.3-.7V2a1 1 0 0 1 1-1h4.9a1 1 0 0 1 .7.3Z\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.2\" stroke-linejoin=\"round\" />" +
            "<circle cx=\"4.5\" cy=\"4.5\" r=\"1\" />",

        ["bi bi-folder"] =
            "<path d=\"M1 3.5A1 1 0 0 1 2 2.5h4l1.5 1.5H14a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1H2a1 1 0 0 1-1-1Z\" />",

        ["bi bi-geo-alt"] =
            "<path d=\"M8 15S13 9.5 13 6a5 5 0 1 0-10 0c0 3.5 5 9 5 9Z\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.2\" stroke-linejoin=\"round\" />" +
            "<circle cx=\"8\" cy=\"6\" r=\"1.8\" />",

        ["bi bi-globe"] =
            "<circle cx=\"8\" cy=\"8\" r=\"6.5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.2\" />" +
            "<ellipse cx=\"8\" cy=\"8\" rx=\"2.6\" ry=\"6.5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.2\" />" +
            "<line x1=\"1.5\" y1=\"8\" x2=\"14.5\" y2=\"8\" stroke=\"currentColor\" stroke-width=\"1.2\" />",
    };

    /// <summary>
    /// Returns the raw inline-<c>&lt;svg&gt;</c> markup for the given Bootstrap Icons class
    /// (e.g. <c>"bi bi-calendar"</c>), or a small generic placeholder glyph for any class not in
    /// the mapping. Returned as a raw <see cref="string"/> so call sites in any host — Blazor's
    /// <c>MarkupString</c>, AspNetCore's <c>Html.Raw</c>, or plain string composition — can wrap it
    /// as needed.
    /// </summary>
    public static string For(string bootstrapIconClass)
    {
        var path = s_paths.TryGetValue(bootstrapIconClass, out var p) ? p : FallbackPath;
        return $"{SvgOpen}{path}{SvgClose}";
    }
}

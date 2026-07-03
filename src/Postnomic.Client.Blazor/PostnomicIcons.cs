using Microsoft.AspNetCore.Components;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client.Blazor;

/// <summary>
/// Blazor-facing wrapper over the shared, framework-free <see cref="PostnomicIconSvg"/> glyph map
/// (moved to <c>Postnomic.Client.Abstractions</c> so the AspNetCore Razor Pages Blog Area can reuse
/// the exact same distinct SVGs — see <see cref="PostnomicIconSvg"/> for the glyph data itself).
/// This type exists only to hand back a Blazor <see cref="MarkupString"/> for call sites that want
/// one, without leaking the <c>Microsoft.AspNetCore.Components</c> dependency into the abstractions
/// package.
/// </summary>
internal static class PostnomicIcons
{
    /// <summary>
    /// Returns the raw inline-<c>&lt;svg&gt;</c> markup for the given Bootstrap Icons class
    /// (e.g. <c>"bi bi-calendar"</c>), or a small generic placeholder glyph for any class not in
    /// the mapping. As a raw <see cref="string"/> (rather than a <see cref="MarkupString"/>) so
    /// call sites that need to compose it further (e.g. append a trailing space to stand in for a
    /// Bootstrap margin utility) can do so before converting to markup.
    /// </summary>
    public static string Markup(string bootstrapIconClass) => PostnomicIconSvg.For(bootstrapIconClass);

    /// <summary>Convenience wrapper over <see cref="Markup"/> for call sites that just need a <see cref="MarkupString"/>.</summary>
    public static MarkupString Svg(string bootstrapIconClass) => (MarkupString)Markup(bootstrapIconClass);
}

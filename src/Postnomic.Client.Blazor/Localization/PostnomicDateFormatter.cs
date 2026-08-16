using System.Globalization;

namespace Postnomic.Client.Blazor.Localization;

/// <summary>
/// Formats the dates rendered by the Postnomic Blazor components (published dates, comment
/// timestamps, certification/education month-years) in a way that reads correctly for the page's
/// <c>Language</c>, rather than always emitting the English-centric <c>"MMMM dd, yyyy"</c> family
/// of patterns the components used before localization.
/// </summary>
/// <remarks>
/// For English (and any language other than German, including <see langword="null"/> and unknown
/// codes) every method below calls the exact same <see cref="DateTime.ToString(string)"/>
/// overload — with no explicit <see cref="CultureInfo"/> — that the component markup called
/// directly prior to localization. That preserves today's rendered text byte-for-byte regardless
/// of the host process's current culture, satisfying the SDK's no-breaking-change guarantee. Only
/// the German branch takes on an explicit <c>de-DE</c> <see cref="CultureInfo"/> and a
/// German-appropriate pattern (day before month, no comma, period-abbreviated month names where
/// German uses them).
/// </remarks>
internal static class PostnomicDateFormatter
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Whether <paramref name="language"/> normalizes to German for date-formatting purposes.</summary>
    private static bool IsGerman(string? language) =>
        string.Equals(PostnomicUiStrings.NormalizeLanguage(language), "de", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A long date, e.g. English "August 16, 2026" / German "16. August 2026". Replaces the
    /// components' original hardcoded <c>"MMMM dd, yyyy"</c> pattern for post-published and
    /// recent-post dates.
    /// </summary>
    public static string LongDate(DateTime date, string? language) =>
        IsGerman(language)
            ? date.ToString("d. MMMM yyyy", German)
            : date.ToString("MMMM dd, yyyy");

    /// <summary>
    /// A short date with a time, e.g. English "Aug 16, 2026 · 14:30" / German "16. Aug. 2026 · 14:30".
    /// Replaces the <c>CommentView</c> component's original hardcoded
    /// <c>"MMM dd, yyyy · HH:mm"</c> pattern for comment timestamps.
    /// </summary>
    public static string ShortDateTime(DateTime date, string? language) =>
        IsGerman(language)
            ? date.ToString("d. MMM yyyy · HH:mm", German)
            : date.ToString("MMM dd, yyyy · HH:mm");

    /// <summary>
    /// A month and year, e.g. English "Aug 2026" / German "Aug 2026" (the de-DE abbreviated month
    /// name for a standalone month-year pattern carries no trailing period, unlike its
    /// day-qualified form — see <see cref="ShortDateTime"/>). Replaces the original hardcoded
    /// <c>"MMM yyyy"</c> pattern used for certification/education issue and expiry dates.
    /// </summary>
    public static string MonthYear(DateTime date, string? language) =>
        IsGerman(language)
            ? date.ToString("MMM yyyy", German)
            : date.ToString("MMM yyyy");
}

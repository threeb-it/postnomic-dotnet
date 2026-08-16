using Postnomic.Client.Abstractions;

namespace Postnomic.Client.Blazor.Localization;

/// <summary>
/// Resolves a UI chrome string key to display text for a given page <c>Language</c>, honoring any
/// consumer-supplied <see cref="PostnomicUiStringOverrides"/> before falling back to the built-in
/// <see cref="PostnomicUiStringCatalog"/>, and finally to the English built-in if a key is missing
/// from a translated language altogether.
/// </summary>
internal static class PostnomicUiStrings
{
    /// <summary>
    /// Normalizes a page's <c>Language</c> parameter (e.g. <c>"de"</c>, <c>"de-DE"</c>,
    /// <see langword="null"/>, or an unrecognized code) down to a language the built-in catalog
    /// has translations for, defaulting to <see cref="PostnomicUiStringCatalog.DefaultLanguage"/>.
    /// </summary>
    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return PostnomicUiStringCatalog.DefaultLanguage;

        var code = language.Trim();
        code = code.Length >= 2 ? code[..2] : code;

        return PostnomicUiStringCatalog.Languages.ContainsKey(code)
            ? code
            : PostnomicUiStringCatalog.DefaultLanguage;
    }

    /// <summary>
    /// Resolves the display text for <paramref name="key"/> under <paramref name="language"/>,
    /// preferring a consumer override, then the built-in translation for the normalized language,
    /// then the English built-in.
    /// </summary>
    public static string Get(string key, string? language, PostnomicUiStringOverrides? overrides)
    {
        var normalized = NormalizeLanguage(language);

        if (overrides is not null && overrides.TryGet(normalized, key, out var overridden))
            return overridden;

        if (PostnomicUiStringCatalog.Languages.TryGetValue(normalized, out var strings) &&
            strings.TryGetValue(key, out var builtIn))
        {
            return builtIn;
        }

        // The built-in catalog is missing this key for this language (e.g. a translation the SDK
        // hasn't caught up on yet, or a consumer-added language with a gap) — fall back to the
        // English built-in rather than surfacing the raw key to end users.
        return PostnomicUiStringCatalog.Languages[PostnomicUiStringCatalog.DefaultLanguage][key];
    }

    /// <summary>
    /// Resolves <paramref name="key"/> as a composite-format string (e.g. <c>"Comments ({0})"</c>)
    /// and formats it with <paramref name="args"/>.
    /// </summary>
    public static string GetFormat(string key, string? language, PostnomicUiStringOverrides? overrides, params object?[] args) =>
        string.Format(Get(key, language, overrides), args);

    /// <summary>
    /// Resolves <paramref name="singularKey"/> when <paramref name="count"/> is exactly 1,
    /// otherwise <paramref name="pluralKey"/>. Matches the SDK's existing English-only pluralization
    /// (which never distinguished zero from "many"), so behaviour is unchanged for English.
    /// </summary>
    public static string Pluralize(int count, string singularKey, string pluralKey, string? language, PostnomicUiStringOverrides? overrides) =>
        count == 1 ? Get(singularKey, language, overrides) : Get(pluralKey, language, overrides);
}

namespace Postnomic.Client.Abstractions;

/// <summary>
/// Consumer-supplied overrides for the Postnomic blog components' built-in UI chrome strings
/// (the pager, the search box, comment-form labels, empty states, and similar copy that ships
/// with the SDK rather than coming from a post's own content). Assign an instance to
/// <see cref="PostnomicClientOptions.UiStrings"/> to replace one or more of the built-in
/// translations without forking the package.
/// </summary>
/// <remarks>
/// Keys are grouped first by a two-letter, ISO-639-1 language code (matching the
/// <c>Language</c> parameter accepted by <c>BlogPage</c>/<c>PostPage</c>/<c>AuthorPage</c> and
/// their child components), then by an SDK-defined string key such as
/// <c>"BlogPage.ReadMore"</c>. Language matching is case-insensitive.
/// <para>
/// Setting a key that the SDK already ships a built-in translation for replaces just that one
/// string; every other key for that language keeps its built-in value. Setting a key under a
/// language the SDK has no built-in translation for adds a new language, provided every key the
/// rendered pages need has been supplied — any key still missing falls back to the English
/// built-in rather than rendering blank.
/// </para>
/// </remarks>
public sealed class PostnomicUiStringOverrides
{
    private readonly Dictionary<string, Dictionary<string, string>> _byLanguage =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sets (or replaces) the string for <paramref name="key"/> under <paramref name="language"/>.
    /// Returns this instance so calls can be chained.
    /// </summary>
    /// <param name="language">A two-letter, ISO-639-1 language code (e.g. <c>"de"</c>). Case-insensitive.</param>
    /// <param name="key">An SDK-defined string key (e.g. <c>"BlogPage.ReadMore"</c>).</param>
    /// <param name="value">The replacement text. Format-string placeholders (e.g. <c>"{0}"</c>) must be preserved for keys that use them.</param>
    public PostnomicUiStringOverrides Set(string language, string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(language);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        if (!_byLanguage.TryGetValue(language, out var strings))
        {
            strings = new Dictionary<string, string>(StringComparer.Ordinal);
            _byLanguage[language] = strings;
        }

        strings[key] = value;
        return this;
    }

    /// <summary>
    /// Attempts to resolve an override for <paramref name="key"/> under <paramref name="language"/>.
    /// </summary>
    public bool TryGet(string language, string key, out string value)
    {
        if (_byLanguage.TryGetValue(language, out var strings) && strings.TryGetValue(key, out var found))
        {
            value = found;
            return true;
        }

        value = "";
        return false;
    }
}

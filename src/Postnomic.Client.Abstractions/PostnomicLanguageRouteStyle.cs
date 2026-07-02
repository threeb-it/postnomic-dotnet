namespace Postnomic.Client.Abstractions;

/// <summary>How the post-language code appears in blog URLs.</summary>
public enum PostnomicLanguageRouteStyle
{
    /// <summary>Language after the base path: <c>/blog/{lang}/post/{slug}</c> (default).</summary>
    Suffix,
    /// <summary>Language before the base path: <c>/{lang}/blog/post/{slug}</c>.</summary>
    Prefix,
    /// <summary>No language segment: <c>/blog/post/{slug}</c>; API serves the blog default.</summary>
    None
}

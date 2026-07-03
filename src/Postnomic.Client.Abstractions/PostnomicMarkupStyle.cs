namespace Postnomic.Client.Abstractions;

/// <summary>
/// Selects the CSS class vocabulary emitted by Postnomic-rendered markup.
/// </summary>
public enum PostnomicMarkupStyle
{
    /// <summary>
    /// Emits Bootstrap utility classes (<c>card</c>, <c>row</c>, <c>btn btn-primary</c>, ...).
    /// This is the default and preserves pre-theming byte-for-byte output for existing consumers.
    /// </summary>
    Bootstrap = 0,

    /// <summary>
    /// Emits framework-free, semantic <c>pn-*</c> classes intended to be themed via CSS variables.
    /// </summary>
    Semantic = 1
}

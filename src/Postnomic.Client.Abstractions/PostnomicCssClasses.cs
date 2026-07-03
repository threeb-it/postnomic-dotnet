namespace Postnomic.Client.Abstractions;

/// <summary>
/// Resolves the CSS class string for each semantic role in Postnomic-rendered markup, based on the
/// configured <see cref="PostnomicMarkupStyle"/>. Pure and framework-free — consumed by the Blazor
/// components and the AspNetCore Razor Pages area to keep the two rendering surfaces in lockstep.
/// </summary>
public sealed class PostnomicCssClasses
{
    private readonly PostnomicMarkupStyle _style;

    /// <summary>Creates a resolver bound to the given markup style.</summary>
    public PostnomicCssClasses(PostnomicMarkupStyle style)
    {
        _style = style;
    }

    /// <summary>The outermost container that wraps the entire blog UI.</summary>
    public string BlogRoot => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-blog",
        _ => "blog-container"
    };

    /// <summary>The blog's header band (title + lead).</summary>
    public string Header => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-header",
        _ => "blog-header text-center py-4 mb-4 border-bottom"
    };

    /// <summary>The blog title heading.</summary>
    public string Title => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-title",
        _ => "display-5 fw-bold"
    };

    /// <summary>The blog lead/subtitle text.</summary>
    public string Lead => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-lead",
        _ => "lead text-muted"
    };

    /// <summary>The two-column layout row wrapping main content and sidebar.</summary>
    public string Layout => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-layout",
        _ => "row"
    };

    /// <summary>The main content column.</summary>
    public string Main => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-main",
        _ => "col-lg-8"
    };

    /// <summary>The sidebar column.</summary>
    public string Sidebar => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-sidebar",
        _ => "col-lg-4"
    };

    /// <summary>A post summary card.</summary>
    public string Card => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-card",
        _ => "card mb-4 shadow-sm"
    };

    /// <summary>The cover image inside a post card.</summary>
    public string CardMedia => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-card__media",
        _ => "card-img-top"
    };

    /// <summary>The body wrapper inside a post card.</summary>
    public string CardBody => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-card__body",
        _ => "card-body"
    };

    /// <summary>A post's title within a card or detail view.</summary>
    public string PostTitle => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-post-title",
        _ => "card-title h4"
    };

    /// <summary>A post's metadata line (date, author, etc.).</summary>
    public string PostMeta => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-post-meta",
        _ => "text-muted small mb-2"
    };

    /// <summary>A post's excerpt text.</summary>
    public string Excerpt => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-excerpt",
        _ => "card-text"
    };

    /// <summary>A generic tag pill.</summary>
    public string Tag => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-tag",
        _ => "badge bg-secondary me-1"
    };

    /// <summary>A category-flavored tag pill.</summary>
    public string TagCategory => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-tag pn-tag--category",
        _ => "badge bg-primary me-1"
    };

    /// <summary>A primary call-to-action button.</summary>
    public string BtnPrimary => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-btn pn-btn--primary",
        _ => "btn btn-primary"
    };

    /// <summary>An outlined, small button (e.g. "Read more").</summary>
    public string BtnOutline => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-btn pn-btn--outline pn-btn--sm",
        _ => "btn btn-outline-primary btn-sm"
    };

    /// <summary>A small secondary/outline button.</summary>
    public string BtnSm => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-btn pn-btn--sm pn-btn--outline",
        _ => "btn btn-sm btn-outline-secondary"
    };

    /// <summary>The pagination control's outer list.</summary>
    public string Pagination => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-pagination",
        _ => "pagination"
    };

    /// <summary>A single pagination page item.</summary>
    public string Page => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-page",
        _ => "page-item"
    };

    /// <summary>Modifier applied to the current pagination page item.</summary>
    public string PageActive => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-page--active",
        _ => "active"
    };

    /// <summary>Modifier applied to a disabled pagination page item.</summary>
    public string PageDisabled => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-page--disabled",
        _ => "disabled"
    };

    /// <summary>The banner shown when a tag/category filter is active.</summary>
    public string FilterBanner => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-filter-banner",
        _ => "alert alert-info d-flex justify-content-between align-items-center mb-3"
    };

    /// <summary>The empty-state message shown when no posts match.</summary>
    public string Empty => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-empty",
        _ => "text-center text-muted py-5"
    };

    /// <summary>The loading-state placeholder.</summary>
    public string Loading => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-loading",
        _ => "text-center py-5"
    };

    /// <summary>The masonry grid wrapper for post cards.</summary>
    public string Masonry => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-masonry",
        _ => "postnomic-masonry"
    };

    /// <summary>A sidebar widget card.</summary>
    public string Widget => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-widget",
        _ => "card mb-3"
    };

    /// <summary>A sidebar widget's title/header.</summary>
    public string WidgetTitle => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-widget__title",
        _ => "card-header"
    };

    /// <summary>The search input group.</summary>
    public string SearchBox => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-searchbox",
        _ => "input-group mb-3"
    };

    /// <summary>A single rendered comment.</summary>
    public string Comment => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-comment",
        _ => "border-bottom pb-2 mb-2"
    };

    /// <summary>The comment-submission form wrapper.</summary>
    public string CommentForm => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-comment-form",
        _ => "mb-3"
    };

    /// <summary>A form input field.</summary>
    public string Field => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-field",
        _ => "form-control"
    };

    /// <summary>The rendered post body/content wrapper.</summary>
    public string PostContent => _style switch
    {
        PostnomicMarkupStyle.Semantic => "pn-post-content",
        _ => "postnomic-post-content"
    };
}

using System.Reflection;
using Postnomic.Client.Abstractions;
using Xunit;

namespace Postnomic.Client.Abstractions.Tests;

public class PostnomicCssClassesTests
{
    [Fact]
    public void Bootstrap_mode_returns_legacy_classes()
    {
        var c = new PostnomicCssClasses(PostnomicMarkupStyle.Bootstrap);
        Assert.Equal("card mb-4 shadow-sm", c.Card);
        Assert.Equal("btn btn-outline-primary btn-sm", c.BtnOutline);
        Assert.Equal("row", c.Layout);
    }

    [Fact]
    public void Semantic_mode_returns_pn_classes()
    {
        var c = new PostnomicCssClasses(PostnomicMarkupStyle.Semantic);
        Assert.Equal("pn-card", c.Card);
        Assert.Equal("pn-btn pn-btn--outline pn-btn--sm", c.BtnOutline);
        Assert.Equal("pn-layout", c.Layout);
    }

    [Fact]
    public void Options_default_markup_style_is_bootstrap() =>
        Assert.Equal(
            PostnomicMarkupStyle.Bootstrap,
            new PostnomicClientOptions { BaseUrl = "x", ApiKey = "k", BlogSlug = "b" }.MarkupStyle);

    /// <summary>
    /// Characterization test pinning ALL 33 <see cref="PostnomicCssClasses"/> role properties in both
    /// <see cref="PostnomicMarkupStyle.Bootstrap"/> and <see cref="PostnomicMarkupStyle.Semantic"/> modes.
    /// This transcribes the CURRENT literal values straight from <c>PostnomicCssClasses.cs</c> — it does not
    /// assert new behavior. Its purpose is to fail CI the moment any Bootstrap-mode role literal changes,
    /// since Bootstrap-mode output must stay byte-identical to pre-branch behavior for existing consumers
    /// (e.g. Xircuit). If a change to Bootstrap here is ever intentional, treat it as a breaking change.
    /// </summary>
    [Theory]
    [InlineData("BlogRoot", "blog-container", "pn-blog")]
    [InlineData("Header", "blog-header text-center py-4 mb-4 border-bottom", "pn-header")]
    [InlineData("Title", "display-5 fw-bold", "pn-title")]
    [InlineData("Lead", "lead text-muted", "pn-lead")]
    [InlineData("Layout", "row", "pn-layout")]
    [InlineData("Main", "col-lg-8", "pn-main")]
    [InlineData("Sidebar", "col-lg-4", "pn-sidebar")]
    [InlineData("Card", "card mb-4 shadow-sm", "pn-card")]
    [InlineData("CardMedia", "card-img-top", "pn-card__media")]
    [InlineData("CardBody", "card-body", "pn-card__body")]
    [InlineData("PostTitle", "card-title h4", "pn-post-title")]
    [InlineData("PostMeta", "text-muted small mb-2", "pn-post-meta")]
    [InlineData("Excerpt", "card-text", "pn-excerpt")]
    [InlineData("Tag", "badge bg-secondary me-1", "pn-tag")]
    [InlineData("TagCategory", "badge bg-primary me-1", "pn-tag pn-tag--category")]
    [InlineData("BtnPrimary", "btn btn-primary", "pn-btn pn-btn--primary")]
    [InlineData("BtnOutline", "btn btn-outline-primary btn-sm", "pn-btn pn-btn--outline pn-btn--sm")]
    [InlineData("BtnSm", "btn btn-sm btn-outline-secondary", "pn-btn pn-btn--sm pn-btn--outline")]
    [InlineData("Pagination", "pagination", "pn-pagination")]
    [InlineData("Page", "page-item", "pn-page")]
    [InlineData("PageActive", "active", "pn-page--active")]
    [InlineData("PageDisabled", "disabled", "pn-page--disabled")]
    [InlineData("FilterBanner", "alert alert-info d-flex justify-content-between align-items-center mb-3", "pn-filter-banner")]
    [InlineData("Empty", "text-center text-muted py-5", "pn-empty")]
    [InlineData("Loading", "text-center py-5", "pn-loading")]
    [InlineData("Masonry", "postnomic-masonry", "pn-masonry")]
    [InlineData("Widget", "card mb-3", "pn-widget")]
    [InlineData("WidgetTitle", "card-header", "pn-widget__title")]
    [InlineData("SearchBox", "input-group mb-3", "pn-searchbox")]
    [InlineData("Comment", "border-bottom pb-2 mb-2", "pn-comment")]
    [InlineData("CommentForm", "mb-3", "pn-comment-form")]
    [InlineData("Field", "form-control", "pn-field")]
    [InlineData("PostContent", "postnomic-post-content", "pn-post-content")]
    public void All_roles_resolve_to_their_pinned_literal_in_both_modes(
        string roleName, string expectedBootstrap, string expectedSemantic)
    {
        var property = typeof(PostnomicCssClasses).GetProperty(roleName, BindingFlags.Public | BindingFlags.Instance);
        // "'{roleName}' should be a public property on PostnomicCssClasses"
        Assert.NotNull(property);

        var bootstrap = new PostnomicCssClasses(PostnomicMarkupStyle.Bootstrap);
        var semantic = new PostnomicCssClasses(PostnomicMarkupStyle.Semantic);

        // "role '{roleName}' in Bootstrap mode"
        Assert.Equal(expectedBootstrap, property!.GetValue(bootstrap));
        // "role '{roleName}' in Semantic mode"
        Assert.Equal(expectedSemantic, property.GetValue(semantic));
    }

    [Fact]
    public void All_roles_are_covered_by_the_pinned_theory()
    {
        var expectedRoles = new[]
        {
            "BlogRoot", "Header", "Title", "Lead", "Layout", "Main", "Sidebar", "Card", "CardMedia",
            "CardBody", "PostTitle", "PostMeta", "Excerpt", "Tag", "TagCategory", "BtnPrimary",
            "BtnOutline", "BtnSm", "Pagination", "Page", "PageActive", "PageDisabled", "FilterBanner",
            "Empty", "Loading", "Masonry", "Widget", "WidgetTitle", "SearchBox", "Comment",
            "CommentForm", "Field", "PostContent",
        };

        var actualRoles = typeof(PostnomicCssClasses)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // "every public role property on PostnomicCssClasses must be pinned by the characterization theory above"
        Assert.Equal(
            expectedRoles.OrderBy(x => x, StringComparer.Ordinal),
            actualRoles.OrderBy(x => x, StringComparer.Ordinal));
    }
}

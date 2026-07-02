using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client.AspNetCore;

/// <summary>
/// A page route model convention that adds a route for the Blog area Razor Pages under
/// the configured <see cref="Abstractions.PostnomicClientOptions.BasePath"/>.
/// Each registration adds a new selector so that multiple base paths can coexist
/// (e.g. <c>/blog</c>, <c>/blog/free</c>, <c>/blog/enterprise</c>).
/// </summary>
/// <remarks>
/// The route templates generated depend on <paramref name="style"/>:
/// <list type="bullet">
/// <item><see cref="PostnomicLanguageRouteStyle.Suffix"/> (default) — a bare route plus a
/// <c>{basePath}/{lang}</c>-suffixed route, preserving the pre-1.2 behavior exactly.</item>
/// <item><see cref="PostnomicLanguageRouteStyle.Prefix"/> — a single route with a required
/// leading <c>{lang}</c> segment before the base path (no bare fallback route).</item>
/// <item><see cref="PostnomicLanguageRouteStyle.None"/> — a single bare route; no language
/// segment is ever part of the route.</item>
/// </list>
/// </remarks>
internal sealed class PostnomicBlogAreaRouteConvention(string basePath, PostnomicLanguageRouteStyle style) : IPageRouteModelConvention
{
    private const string LangSegment = "{lang:regex(^[a-z][a-z]$)}";

    public void Apply(PageRouteModel model)
    {
        if (!string.Equals(model.AreaName, "Blog", StringComparison.OrdinalIgnoreCase))
            return;

        var trimmedPath = basePath.Trim('/');

        string? bareTail = null;
        if (model.RelativePath.EndsWith("Index.cshtml", StringComparison.OrdinalIgnoreCase))
        {
            bareTail = "";
        }
        else if (model.RelativePath.EndsWith("Post.cshtml", StringComparison.OrdinalIgnoreCase))
        {
            bareTail = "/post/{postSlug}";
        }
        else if (model.RelativePath.EndsWith("Author.cshtml", StringComparison.OrdinalIgnoreCase))
        {
            bareTail = "/author/{authorSlug}";
        }

        if (bareTail is null)
            return;

        var bareTemplate = $"{trimmedPath}{bareTail}";
        List<string> templates = style switch
        {
            PostnomicLanguageRouteStyle.Prefix => [$"{LangSegment}/{bareTemplate}"],
            PostnomicLanguageRouteStyle.None => [bareTemplate],
            _ => [bareTemplate, $"{trimmedPath}/{LangSegment}{bareTail}"],
        };

        foreach (var template in templates)
        {
            var alreadyExists = model.Selectors.Any(s =>
                s.AttributeRouteModel is not null &&
                string.Equals(s.AttributeRouteModel.Template, template, StringComparison.OrdinalIgnoreCase));
            if (!alreadyExists)
                model.Selectors.Add(new SelectorModel { AttributeRouteModel = new AttributeRouteModel { Template = template } });
        }
    }
}

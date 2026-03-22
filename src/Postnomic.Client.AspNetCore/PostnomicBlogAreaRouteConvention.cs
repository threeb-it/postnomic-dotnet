using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Postnomic.Client.AspNetCore;

/// <summary>
/// A page route model convention that adds a route for the Blog area Razor Pages under
/// the configured <see cref="Abstractions.PostnomicClientOptions.BasePath"/>.
/// Each registration adds a new selector so that multiple base paths can coexist
/// (e.g. <c>/blog</c>, <c>/blog/free</c>, <c>/blog/enterprise</c>).
/// </summary>
internal sealed class PostnomicBlogAreaRouteConvention(string basePath) : IPageRouteModelConvention
{
    public void Apply(PageRouteModel model)
    {
        if (!string.Equals(model.AreaName, "Blog", StringComparison.OrdinalIgnoreCase))
            return;

        var trimmedPath = basePath.Trim('/');

        string? template = null;

        if (model.RelativePath.EndsWith("Index.cshtml", StringComparison.OrdinalIgnoreCase))
        {
            template = trimmedPath;
        }
        else if (model.RelativePath.EndsWith("Post.cshtml", StringComparison.OrdinalIgnoreCase))
        {
            template = $"{trimmedPath}/post/{{postSlug}}";
        }
        else if (model.RelativePath.EndsWith("Author.cshtml", StringComparison.OrdinalIgnoreCase))
        {
            template = $"{trimmedPath}/author/{{authorSlug}}";
        }

        if (template is null) return;

        // Check if any existing selector already has this exact template
        var alreadyExists = model.Selectors.Any(s =>
            s.AttributeRouteModel is not null &&
            string.Equals(s.AttributeRouteModel.Template, template, StringComparison.OrdinalIgnoreCase));

        if (!alreadyExists)
        {
            model.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel { Template = template }
            });
        }
    }
}

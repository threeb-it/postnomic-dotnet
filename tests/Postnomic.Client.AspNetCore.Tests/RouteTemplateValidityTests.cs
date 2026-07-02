using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Postnomic.Client.AspNetCore.Tests;

/// <summary>
/// Regression tests for a bug where <see cref="PostnomicBlogAreaRouteConvention"/> generated
/// a route template with an unescaped regex quantifier — <c>{lang:regex(^[a-z]{2}$)}</c> instead
/// of <c>{lang:regex(^[a-z][a-z]$)}</c>. Route templates use <c>{}</c> for parameter delimiters,
/// so the unescaped <c>{2}</c> quantifier produced a template that failed to parse — but only
/// when an app actually registered the route at startup via
/// <see cref="RoutePatternFactory.Parse(string)"/> (through Razor Pages route registration).
///
/// The existing tests in <see cref="MultiLanguageRoutingTests"/> and
/// <see cref="PostnomicAspNetCoreExtensionsTests"/> only string-compare the generated
/// <see cref="AttributeRouteModel.Template"/> values, so a template that is textually correct
/// but structurally invalid would still pass them. These tests close that gap by feeding every
/// template the convention produces through <see cref="RoutePatternFactory.Parse(string)"/>,
/// which throws <see cref="RoutePatternException"/> for an invalid route pattern — exactly what
/// ASP.NET Core does when the route is registered for real.
/// </summary>
public class RouteTemplateValidityTests
{
    [Theory]
    [InlineData("/blog")]
    [InlineData("/articles")]
    public void PostnomicBlogAreaRouteConvention_GeneratedTemplates_AreValidRoutePatterns(string basePath)
    {
        // Arrange
        var convention = ResolveConvention(basePath);

        var models = new[]
        {
            BuildPageRouteModel("/Areas/Blog/Pages/Index.cshtml", "/Index"),
            BuildPageRouteModel("/Areas/Blog/Pages/Post.cshtml", "/Post"),
            BuildPageRouteModel("/Areas/Blog/Pages/Author.cshtml", "/Author"),
        };

        foreach (var model in models)
        {
            convention.Apply(model);
        }

        var templates = models
            .SelectMany(m => m.Selectors)
            .Where(s => s.AttributeRouteModel is not null)
            .Select(s => s.AttributeRouteModel!.Template!)
            .ToList();

        // Sanity check — the convention must actually have produced templates to validate,
        // including the /{lang}/-prefixed ones the bug affected.
        templates.Should().NotBeEmpty();
        templates.Should().Contain(t => t.Contains("{lang:regex("));

        // Act & Assert — every generated template must be a valid, registerable route pattern.
        // RoutePatternFactory.Parse throws RoutePatternException for an invalid template such as
        // the old, unescaped "{lang:regex(^[a-z]{2}$)}".
        foreach (var template in templates)
        {
            var act = () => RoutePatternFactory.Parse(template);
            act.Should().NotThrow(
                because: $"the generated route template '{template}' must be registerable by ASP.NET Core routing");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IPageRouteModelConvention ResolveConvention(string basePath)
    {
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options => options.BasePath = basePath);
        var provider = services.BuildServiceProvider();

        return provider
            .GetRequiredService<IOptions<RazorPagesOptions>>().Value
            .Conventions
            .OfType<IPageRouteModelConvention>()
            .Single();
    }

    private static PageRouteModel BuildPageRouteModel(string relativePath, string viewEnginePath)
    {
        var model = new PageRouteModel(
            relativePath: relativePath,
            viewEnginePath: viewEnginePath,
            areaName: "Blog");

        model.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel { Template = "placeholder" }
        });

        return model;
    }
}

using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client.AspNetCore.Tests;

/// <summary>
/// Unit tests for <see cref="PostnomicBlogAreaRouteConvention"/> covering how it branches
/// route template generation per <see cref="PostnomicLanguageRouteStyle"/>:
/// <list type="bullet">
/// <item><see cref="PostnomicLanguageRouteStyle.Suffix"/> (default) — a bare route plus a
/// <c>{basePath}/{lang}</c> suffixed route (pre-existing, must stay byte-compatible).</item>
/// <item><see cref="PostnomicLanguageRouteStyle.Prefix"/> — a single route with a required
/// leading <c>{lang}</c> segment before the base path.</item>
/// <item><see cref="PostnomicLanguageRouteStyle.None"/> — a single bare route with no
/// language segment at all.</item>
/// </list>
/// </summary>
public class RouteConventionTests
{
    // ── Suffix (default) — unchanged, byte-compatible ─────────────────────────

    [Fact]
    public void Suffix_MapsIndexPage_WithBareAndSuffixedRoutes()
    {
        var templates = ApplyConvention("/blog", PostnomicLanguageRouteStyle.Suffix, IndexModel());

        Assert.Equal(["blog", "blog/{lang:regex(^[a-z][a-z]$)}"], templates);
    }

    [Fact]
    public void Suffix_MapsPostPage_WithBareAndSuffixedRoutes()
    {
        var templates = ApplyConvention("/blog", PostnomicLanguageRouteStyle.Suffix, PostModel());

        Assert.Equal(["blog/post/{postSlug}", "blog/{lang:regex(^[a-z][a-z]$)}/post/{postSlug}"], templates);
    }

    [Fact]
    public void Suffix_MapsAuthorPage_WithBareAndSuffixedRoutes()
    {
        var templates = ApplyConvention("/blog", PostnomicLanguageRouteStyle.Suffix, AuthorModel());

        Assert.Equal(["blog/author/{authorSlug}", "blog/{lang:regex(^[a-z][a-z]$)}/author/{authorSlug}"], templates);
    }

    // ── Prefix — single route, lang leads and is required ─────────────────────

    [Fact]
    public void Prefix_MapsIndexPage_WithSingleLangLeadingRoute()
    {
        var templates = ApplyConvention("/blog", PostnomicLanguageRouteStyle.Prefix, IndexModel());

        Assert.Equal(["{lang:regex(^[a-z][a-z]$)}/blog"], templates);
    }

    [Fact]
    public void Prefix_MapsPostPage_WithSingleLangLeadingRoute()
    {
        var templates = ApplyConvention("/blog", PostnomicLanguageRouteStyle.Prefix, PostModel());

        Assert.Equal(["{lang:regex(^[a-z][a-z]$)}/blog/post/{postSlug}"], templates);
    }

    [Fact]
    public void Prefix_MapsAuthorPage_WithSingleLangLeadingRoute()
    {
        var templates = ApplyConvention("/blog", PostnomicLanguageRouteStyle.Prefix, AuthorModel());

        Assert.Equal(["{lang:regex(^[a-z][a-z]$)}/blog/author/{authorSlug}"], templates);
    }

    // ── None — single bare route, no language segment ─────────────────────────

    [Fact]
    public void None_MapsIndexPage_WithBareRouteOnly()
    {
        var templates = ApplyConvention("/blog", PostnomicLanguageRouteStyle.None, IndexModel());

        Assert.Equal(["blog"], templates);
    }

    [Fact]
    public void None_MapsPostPage_WithBareRouteOnly()
    {
        var templates = ApplyConvention("/blog", PostnomicLanguageRouteStyle.None, PostModel());

        Assert.Equal(["blog/post/{postSlug}"], templates);
    }

    [Fact]
    public void None_MapsAuthorPage_WithBareRouteOnly()
    {
        var templates = ApplyConvention("/blog", PostnomicLanguageRouteStyle.None, AuthorModel());

        Assert.Equal(["blog/author/{authorSlug}"], templates);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static List<string> ApplyConvention(string basePath, PostnomicLanguageRouteStyle style, PageRouteModel model)
    {
        var convention = ResolveConvention(basePath, style);
        convention.Apply(model);

        return model.Selectors
            .Where(s => s.AttributeRouteModel is not null)
            .Select(s => s.AttributeRouteModel!.Template!)
            .Where(t => t != "placeholder")
            .ToList();
    }

    private static IPageRouteModelConvention ResolveConvention(string basePath, PostnomicLanguageRouteStyle style)
    {
        var services = new ServiceCollection();
        services.AddPostnomicBlog(options =>
        {
            options.BasePath = basePath;
            options.LanguageRouteStyle = style;
        });
        var provider = services.BuildServiceProvider();

        return provider
            .GetRequiredService<IOptions<RazorPagesOptions>>().Value
            .Conventions
            .OfType<IPageRouteModelConvention>()
            .Single();
    }

    private static PageRouteModel IndexModel() => BuildPageRouteModel("/Areas/Blog/Pages/Index.cshtml", "/Index");
    private static PageRouteModel PostModel() => BuildPageRouteModel("/Areas/Blog/Pages/Post.cshtml", "/Post");
    private static PageRouteModel AuthorModel() => BuildPageRouteModel("/Areas/Blog/Pages/Author.cshtml", "/Author");

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

using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.Blazor.Components.Pages;

namespace Postnomic.Client.Blazor.Tests;

/// <summary>
/// bUnit tests verifying that <see cref="BlogPage"/> (and, transitively, every sidebar widget it
/// hosts) resolves its CSS classes through <see cref="PostnomicCssClasses"/> according to the
/// configured <see cref="PostnomicMarkupStyle"/> — the default (<see cref="PostnomicMarkupStyle.Bootstrap"/>)
/// must keep emitting today's literal Bootstrap markup, while <see cref="PostnomicMarkupStyle.Semantic"/>
/// must emit only <c>pn-*</c> classes and carry no Bootstrap vestiges.
/// </summary>
public class MarkupStyleTests : BunitContext
{
    private void Wire(PostnomicMarkupStyle style)
    {
        var svc = new Mock<IPostnomicBlogService>();
        svc.Setup(s => s.GetBlogAsync(default)).ReturnsAsync(new PostnomicBlogInfo { Name = "Blog", Slug = "b" });
        svc.Setup(s => s.GetPostsAsync(1, 5, null, null, null, null, null, default)).ReturnsAsync(
            new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items = [ new PostnomicPostSummary { Slug="p", Title="Hello", AuthorName="A",
                    PublishedAt=DateTime.UtcNow, Language="en", AvailableLanguages=["en"] } ],
                Page=1, PageSize=5, TotalCount=1, TotalPages=1
            });
        // sidebar getters → empty
        svc.Setup(s => s.GetTagsAsync(default)).ReturnsAsync([]);
        svc.Setup(s => s.GetCategoriesAsync(default)).ReturnsAsync([]);
        svc.Setup(s => s.GetAuthorsAsync(default)).ReturnsAsync([]);
        svc.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), default)).ReturnsAsync([]);
        svc.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), default)).ReturnsAsync([]);
        Services.AddSingleton(svc.Object);
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(Options.Create(new PostnomicClientOptions
        { BaseUrl="https://api.x", ApiKey="k", BlogSlug="b", BasePath="/blog", MarkupStyle=style }));
    }

    [Fact]
    public void Default_bootstrap_mode_still_emits_bootstrap()
    {
        Wire(PostnomicMarkupStyle.Bootstrap);
        var html = Render<BlogPage>().Markup;
        html.Should().Contain("card").And.Contain("col-lg-8");
        html.Should().NotContain("pn-card");
    }

    [Fact]
    public void Semantic_mode_emits_pn_and_no_bootstrap()
    {
        Wire(PostnomicMarkupStyle.Semantic);
        var html = Render<BlogPage>().Markup;
        html.Should().Contain("pn-blog").And.Contain("pn-card").And.Contain("pn-post-title");
        foreach (var bs in new[] { "col-lg-", "card mb-4", "badge", "btn btn-", "bi bi-" })
            html.Should().NotContain(bs);
    }
}

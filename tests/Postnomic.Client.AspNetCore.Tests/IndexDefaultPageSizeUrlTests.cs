using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Moq;
using Postnomic.Client.Abstractions;
using static Postnomic.Client.AspNetCore.Tests.TestSupport.FilterLinkTestHost;

namespace Postnomic.Client.AspNetCore.Tests;

/// <summary>
/// The Index page leaves <c>PageSize</c> out of its pagination links and search form when it equals
/// <see cref="Areas.Blog.Pages.IndexModel.DefaultPageSize"/>: an absent <c>?PageSize=</c> binds the same
/// default, so including it only minted a second URL (and cache key) for the same page.
/// </summary>
public class IndexDefaultPageSizeUrlTests
{
    private static async Task<string> RenderAsync(PostnomicMarkupStyle style, string url)
    {
        using var host = await StartAsync(new BlogRegistration(null, "/blog", null, style));
        using var client = host.GetTestClient();
        var html = await client.GetStringAsync(url, TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
        return html;
    }

    [Theory]
    [InlineData(PostnomicMarkupStyle.Bootstrap)]
    [InlineData(PostnomicMarkupStyle.Semantic)]
    public async Task Default_page_size_is_left_out_of_pagination_links_and_the_search_form(PostnomicMarkupStyle style)
    {
        var html = await RenderAsync(style, "/blog?tag=dotnet&p=2");
        var pagerLinks = Elements(html).Where(e => e.Href?.Contains("?p=", StringComparison.Ordinal) == true).ToList();

        Assert.NotEmpty(pagerLinks);
        Assert.All(pagerLinks, e => Assert.DoesNotContain("PageSize", e.Href!, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("name=\"pageSize\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_default_page_size_is_still_carried_by_pagination_links_and_the_search_form()
    {
        var html = await RenderAsync(PostnomicMarkupStyle.Bootstrap, "/blog?PageSize=7&p=2");
        var pagerLinks = Elements(html).Where(e => e.Href?.Contains("?p=", StringComparison.Ordinal) == true).ToList();

        Assert.NotEmpty(pagerLinks);
        Assert.All(pagerLinks, e => Assert.Contains("PageSize=7", e.Href!, StringComparison.Ordinal));
        Assert.Contains("name=\"pageSize\" value=\"7\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_request_without_PageSize_loads_the_default_page_size()
    {
        // What makes the omission behaviour-neutral: /blog?p=2 asks the API for exactly what
        // /blog?p=2&PageSize=5 did.
        var service = CreateBlogServiceMock();
        using var host = await StartAsync((new BlogRegistration(null, "/blog", null), service));
        using var client = host.GetTestClient();

        await client.GetStringAsync("/blog?p=2", TestContext.Current.CancellationToken);

        service.Verify(s => s.GetPostsAsync(
            2, Areas.Blog.Pages.IndexModel.DefaultPageSize, It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(5, Areas.Blog.Pages.IndexModel.DefaultPageSize);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }
}

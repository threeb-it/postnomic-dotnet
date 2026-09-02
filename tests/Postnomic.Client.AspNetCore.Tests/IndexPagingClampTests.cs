using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.AspNetCore.Areas.Blog.Pages;

namespace Postnomic.Client.AspNetCore.Tests;

/// <summary>
/// Tests that the query-bound paging values on <see cref="IndexModel"/> are clamped into a sane
/// range before they reach the API or the generated pagination links. <c>?p=</c> and
/// <c>?PageSize=</c> come straight off the URL, so a crawler or a typo can otherwise ask for
/// page -3, page 9,999,999 or 100,000 posts in one request.
/// </summary>
public class IndexPagingClampTests
{
    // ── PageNumber ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task OnGetAsync_ClampsNonPositivePageNumberToOne(int requestedPage)
    {
        // Arrange
        var mock = CreateService();
        var sut = CreateSut(mock);
        sut.PageNumber = requestedPage;

        // Act
        await sut.OnGetAsync();

        // Assert — neither the API nor the view sees the out-of-range value.
        Assert.Equal(1, sut.PageNumber);
        VerifyRequestedPage(mock, 1);
    }

    [Fact]
    public async Task OnGetAsync_LeavesAValidPageNumberAlone()
    {
        // Arrange
        var mock = CreateService();
        var sut = CreateSut(mock);
        sut.PageNumber = 3;

        // Act
        await sut.OnGetAsync();

        // Assert
        Assert.Equal(3, sut.PageNumber);
        VerifyRequestedPage(mock, 3);
    }

    // ── PageSize ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-20, 1)]
    [InlineData(5, 5)]
    [InlineData(100, 100)]
    [InlineData(5000, 100)]
    [InlineData(int.MaxValue, 100)]
    public async Task OnGetAsync_ClampsPageSizeIntoRange(int requested, int expected)
    {
        // Arrange
        var mock = CreateService();
        var sut = CreateSut(mock);
        sut.PageSize = requested;

        // Act
        await sut.OnGetAsync();

        // Assert
        Assert.Equal(expected, sut.PageSize);
        mock.Verify(s => s.GetPostsAsync(
            It.IsAny<int>(), expected,
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Link generation ───────────────────────────────────────────────────────

    [Fact]
    public async Task PageUrl_NeverEmitsAPageBelowOne()
    {
        // Arrange — the view renders the "previous" arrow as PageUrl(PageNumber - 1) whatever
        // the current page is, so page 1 asks for page 0.
        var sut = CreateSut(CreateService(totalPages: 3));
        await sut.OnGetAsync();

        // Act & Assert
        Assert.Contains("p=1", sut.PageUrl(0));
        Assert.Contains("p=1", sut.PageUrl(-7));
        Assert.DoesNotContain("p=0", sut.PageUrl(0));
    }

    [Fact]
    public async Task PageUrl_NeverEmitsAPageBeyondTotalPages()
    {
        // Arrange
        var sut = CreateSut(CreateService(totalPages: 3));
        await sut.OnGetAsync();

        // Act & Assert
        Assert.Contains("p=3", sut.PageUrl(4));
        Assert.Contains("p=3", sut.PageUrl(999_999));
        Assert.Contains("p=2", sut.PageUrl(2));
    }

    [Fact]
    public async Task PageRouteValues_ClampsTheTargetPageIntoRange()
    {
        // Arrange
        var sut = CreateSut(CreateService(totalPages: 3));
        await sut.OnGetAsync();

        // Act & Assert
        Assert.Equal("1", sut.PageRouteValues(0)["p"]);
        Assert.Equal("3", sut.PageRouteValues(50)["p"]);
        Assert.Equal("2", sut.PageRouteValues(2)["p"]);
    }

    [Fact]
    public async Task PageUrl_ClampsAnOversizedPageSizeToo()
    {
        // Arrange — the link must not hand the next request a page size the model rejects.
        var mock = CreateService(totalPages: 2);
        var sut = CreateSut(mock);
        sut.PageSize = 100_000;
        await sut.OnGetAsync();

        // Act & Assert
        Assert.Contains("PageSize=100", sut.PageUrl(2));
        Assert.Equal("100", sut.PageRouteValues(2)["PageSize"]);
    }

    [Fact]
    public void PageUrl_BeforePostsAreLoaded_OnlyEnforcesTheLowerBound()
    {
        // Arrange — TotalPages is 0 until OnGetAsync has run, so there is no upper bound to
        // enforce yet; the pre-existing behaviour of echoing the requested page is preserved.
        var sut = CreateSut(CreateService());

        // Act & Assert
        Assert.Contains("p=4", sut.PageUrl(4));
        Assert.Contains("p=1", sut.PageUrl(-4));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void VerifyRequestedPage(Mock<IPostnomicBlogService> mock, int expectedPage) =>
        mock.Verify(s => s.GetPostsAsync(
            expectedPage, It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);

    private static Mock<IPostnomicBlogService> CreateService(int totalPages = 1)
    {
        var mock = new Mock<IPostnomicBlogService>();

        mock.Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items = [],
                Page = 1,
                PageSize = 5,
                TotalCount = totalPages * 5,
                TotalPages = totalPages
            });

        mock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Test Blog", Slug = "test-blog" });
        mock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        mock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        mock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        return mock;
    }

    private static IndexModel CreateSut(Mock<IPostnomicBlogService> mock)
    {
        var resolver = new Mock<IPostnomicBlogResolver>();
        resolver.Setup(r => r.ResolveBlogName(It.IsAny<string>())).Returns((string?)null);

        var model = new IndexModel(
            mock.Object,
            Mock.Of<IServiceProvider>(),
            resolver.Object,
            Options.Create(new PostnomicClientOptions { BasePath = "/blog" }),
            Mock.Of<IOptionsMonitor<PostnomicClientOptions>>());

        var httpContext = new DefaultHttpContext();
        model.PageContext = new PageContext(new ActionContext(
            httpContext,
            new RouteData(),
            new PageActionDescriptor(),
            new ModelStateDictionary()));
        model.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        model.Url = Mock.Of<IUrlHelper>();

        return model;
    }
}

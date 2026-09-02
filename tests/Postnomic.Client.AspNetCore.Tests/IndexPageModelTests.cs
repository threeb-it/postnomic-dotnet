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
/// Unit tests for <see cref="IndexModel"/>.
/// Verifies that <see cref="IndexModel.OnGetAsync"/> correctly populates all page data
/// properties and that filter query parameters are forwarded to the blog service.
/// </summary>
public class IndexPageModelTests
{
    private readonly Mock<IPostnomicBlogService> _blogServiceMock;
    private readonly IndexModel _sut;

    public IndexPageModelTests()
    {
        _blogServiceMock = new Mock<IPostnomicBlogService>();
        _sut = CreateSut(_blogServiceMock);
        SetupDefaultServiceResponses();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IndexModel CreateSut(Mock<IPostnomicBlogService> mock)
        => CreateSut(mock, new PostnomicClientOptions { BasePath = "/blog" });

    private static IndexModel CreateSut(Mock<IPostnomicBlogService> mock, PostnomicClientOptions clientOptions)
    {
        var resolver = new Mock<IPostnomicBlogResolver>();
        resolver.Setup(r => r.ResolveBlogName(It.IsAny<string>())).Returns((string?)null);
        var model = new IndexModel(
            mock.Object,
            Mock.Of<IServiceProvider>(),
            resolver.Object,
            Options.Create(clientOptions),
            Mock.Of<IOptionsMonitor<PostnomicClientOptions>>());

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new PageActionDescriptor(),
            new ModelStateDictionary());

        var urlHelper = new Mock<IUrlHelper>();
        var urlHelperFactory = new Mock<IUrlHelperFactory>();
        urlHelperFactory.Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>())).Returns(urlHelper.Object);

        model.PageContext = new PageContext(actionContext);
        model.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        model.Url = urlHelper.Object;

        return model;
    }

    private void SetupDefaultServiceResponses()
    {
        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Test Blog", Slug = "test-blog" });

        _blogServiceMock
            .Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicTag>());

        _blogServiceMock
            .Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicCategory>());

        _blogServiceMock
            .Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicAuthor>());

        _blogServiceMock
            .Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());

        _blogServiceMock
            .Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());

        _blogServiceMock
            .Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items = [],
                Page = 1,
                PageSize = 5,
                TotalCount = 0,
                TotalPages = 0
            });
    }

    // ── OnGetAsync — return value ─────────────────────────────────────────────

    [Fact]
    public async Task OnGetAsync_ReturnsPageResult()
    {
        // Act
        var result = await _sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<PageResult>(result);
    }

    // ── OnGetAsync — data population ─────────────────────────────────────────

    [Fact]
    public async Task OnGetAsync_PopulatesBlogInfo()
    {
        // Act
        await _sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(_sut.BlogInfo);
        Assert.Equal("Test Blog", _sut.BlogInfo!.Name);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesPosts()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items =
                [
                    new PostnomicPostSummary
                    {
                        Slug = "loaded-post",
                        Title = "Loaded Post",
                        AuthorName = "Author",
                        PublishedAt = DateTime.UtcNow
                    }
                ],
                Page = 1,
                PageSize = 5,
                TotalCount = 1,
                TotalPages = 1
            });

        // Act
        await _sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(_sut.Posts.Items);
        Assert.Equal("loaded-post", _sut.Posts.Items.First().Slug);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesTags()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicTag>
            {
                new() { Name = "C#", Slug = "csharp", PostCount = 3 }
            });

        // Act
        await _sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(_sut.Tags);
        Assert.Equal("csharp", _sut.Tags[0].Slug);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesCategories()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicCategory>
            {
                new() { Name = "Tutorials", Slug = "tutorials", PostCount = 5 }
            });

        // Act
        await _sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(_sut.Categories);
        Assert.Equal("tutorials", _sut.Categories[0].Slug);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesAuthors()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicAuthor>
            {
                new() { Name = "Jane Doe", PostCount = 7 }
            });

        // Act
        await _sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(_sut.Authors);
        Assert.Equal("Jane Doe", _sut.Authors[0].Name);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesTopCommented()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>
            {
                new() { Slug = "hot-post", Title = "Hot Post", Count = 50 }
            });

        // Act
        await _sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(_sut.TopCommented);
        Assert.Equal("hot-post", _sut.TopCommented[0].Slug);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesMostRead()
    {
        // Arrange
        _blogServiceMock
            .Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>
            {
                new() { Slug = "viral-post", Title = "Viral Post", Count = 9000 }
            });

        // Act
        await _sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(_sut.MostRead);
        Assert.Equal("viral-post", _sut.MostRead[0].Slug);
    }

    // ── OnGetAsync — filter parameters ───────────────────────────────────────

    [Fact]
    public async Task OnGetAsync_ForwardsTagFilterToService()
    {
        // Arrange
        _sut.Tag = "csharp";

        // Act
        await _sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        _blogServiceMock.Verify(s => s.GetPostsAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            "csharp",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_ForwardsCategoryFilterToService()
    {
        // Arrange
        _sut.Category = "tutorials";

        // Act
        await _sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        _blogServiceMock.Verify(s => s.GetPostsAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            "tutorials",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_ForwardsAuthorFilterToService()
    {
        // Arrange
        _sut.Author = "Jane Doe";

        // Act
        await _sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        _blogServiceMock.Verify(s => s.GetPostsAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            "Jane Doe",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_ForwardsSearchTermToService()
    {
        // Arrange
        _sut.Search = "blazor";

        // Act
        await _sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        _blogServiceMock.Verify(s => s.GetPostsAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            "blazor",
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_ForwardsPageNumberToService()
    {
        // Arrange
        _sut.PageNumber = 3;

        // Act
        await _sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        _blogServiceMock.Verify(s => s.GetPostsAsync(
            3,
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── HasActiveFilter helper ────────────────────────────────────────────────

    [Theory]
    [InlineData("csharp", null, null, null, true)]
    [InlineData(null, "tutorials", null, null, true)]
    [InlineData(null, null, "Jane", null, true)]
    [InlineData(null, null, null, "blazor", true)]
    [InlineData(null, null, null, null, false)]
    public void HasActiveFilter_ReturnsExpectedValue(
        string? tag, string? category, string? author, string? search, bool expected)
    {
        // Arrange
        _sut.Tag = tag;
        _sut.Category = category;
        _sut.Author = author;
        _sut.Search = search;

        // Act & Assert
        Assert.Equal(expected, _sut.HasActiveFilter);
    }

    // ── PageRouteValues helper ─────────────────────────────────────────────────

    [Fact]
    public void PageRouteValues_ReturnsCorrectPageNumber()
    {
        // Arrange
        _sut.PageSize = 10;
        _sut.Tag = "csharp";

        // Act
        var values = _sut.PageRouteValues(4);

        // Assert
        Assert.Equal("4", values["p"]);
    }

    [Fact]
    public void PageRouteValues_PreservesCurrentFilters()
    {
        // Arrange
        _sut.Tag = "dotnet";
        _sut.Category = "tutorials";
        _sut.Author = "Jane";
        _sut.Search = "query";
        _sut.PageSize = 5;

        // Act
        var values = _sut.PageRouteValues(2);

        // Assert
        Assert.Equal("dotnet", values["Tag"]);
        Assert.Equal("tutorials", values["Category"]);
        Assert.Equal("Jane", values["Author"]);
        Assert.Equal("query", values["Search"]);
        Assert.Equal("5", values["PageSize"]);
    }

    // ── ShowBranding ──────────────────────────────────────────────────────────

    [Fact]
    public void ShowBranding_WhenNoBlogResolved_ReturnsFalseByDefault()
    {
        // Arrange — default options leave ShowBranding at its default value (false)
        var sut = CreateSut(new Mock<IPostnomicBlogService>());

        // Act & Assert
        Assert.False(sut.ShowBranding);
    }

    [Fact]
    public void ShowBranding_WhenNoBlogResolved_ReturnsValueFromDefaultOptions()
    {
        // Arrange — explicitly enable branding in the default options
        var options = new PostnomicClientOptions { BasePath = "/blog", ShowBranding = true };
        var sut = CreateSut(new Mock<IPostnomicBlogService>(), options);

        // Act & Assert
        Assert.True(sut.ShowBranding);
    }

    [Fact]
    public async Task ShowBranding_WhenBlogInfoReturnedWithShowBrandingTrue_ReturnsTrue()
    {
        // Arrange
        var mock = new Mock<IPostnomicBlogService>();
        mock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Free Blog", Slug = "free-blog", ShowBranding = true });
        mock.Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items = [], Page = 1, PageSize = 5, TotalCount = 0, TotalPages = 0
            });
        mock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicTag>());
        mock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicCategory>());
        mock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicAuthor>());
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());

        var sut = CreateSut(mock);

        // Act
        await sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(sut.ShowBranding,
            "the server returned ShowBranding = true so it should take precedence");
    }

    [Fact]
    public async Task ShowBranding_WhenBlogInfoReturnedWithShowBrandingFalse_ReturnsFalse()
    {
        // Arrange
        var mock = new Mock<IPostnomicBlogService>();
        mock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Paid Blog", Slug = "paid-blog", ShowBranding = false });
        mock.Setup(s => s.GetPostsAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items = [], Page = 1, PageSize = 5, TotalCount = 0, TotalPages = 0
            });
        mock.Setup(s => s.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicTag>());
        mock.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicCategory>());
        mock.Setup(s => s.GetAuthorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicAuthor>());
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());

        // Even though client options say ShowBranding = true, the server value wins
        var options = new PostnomicClientOptions { BasePath = "/blog", ShowBranding = true };
        var sut = CreateSut(mock, options);

        // Act
        await sut.OnGetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(sut.ShowBranding,
            "the server returned ShowBranding = false so it should take precedence over client config");
    }
}

using FluentAssertions;
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
        var result = await _sut.OnGetAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
    }

    // ── OnGetAsync — data population ─────────────────────────────────────────

    [Fact]
    public async Task OnGetAsync_PopulatesBlogInfo()
    {
        // Act
        await _sut.OnGetAsync();

        // Assert
        _sut.BlogInfo.Should().NotBeNull();
        _sut.BlogInfo!.Name.Should().Be("Test Blog");
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
        await _sut.OnGetAsync();

        // Assert
        _sut.Posts.Items.Should().HaveCount(1);
        _sut.Posts.Items.First().Slug.Should().Be("loaded-post");
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
        await _sut.OnGetAsync();

        // Assert
        _sut.Tags.Should().HaveCount(1);
        _sut.Tags[0].Slug.Should().Be("csharp");
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
        await _sut.OnGetAsync();

        // Assert
        _sut.Categories.Should().HaveCount(1);
        _sut.Categories[0].Slug.Should().Be("tutorials");
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
        await _sut.OnGetAsync();

        // Assert
        _sut.Authors.Should().HaveCount(1);
        _sut.Authors[0].Name.Should().Be("Jane Doe");
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
        await _sut.OnGetAsync();

        // Assert
        _sut.TopCommented.Should().HaveCount(1);
        _sut.TopCommented[0].Slug.Should().Be("hot-post");
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
        await _sut.OnGetAsync();

        // Assert
        _sut.MostRead.Should().HaveCount(1);
        _sut.MostRead[0].Slug.Should().Be("viral-post");
    }

    // ── OnGetAsync — filter parameters ───────────────────────────────────────

    [Fact]
    public async Task OnGetAsync_ForwardsTagFilterToService()
    {
        // Arrange
        _sut.Tag = "csharp";

        // Act
        await _sut.OnGetAsync();

        // Assert
        _blogServiceMock.Verify(s => s.GetPostsAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            "csharp",
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
        await _sut.OnGetAsync();

        // Assert
        _blogServiceMock.Verify(s => s.GetPostsAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            "tutorials",
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
        await _sut.OnGetAsync();

        // Assert
        _blogServiceMock.Verify(s => s.GetPostsAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            "Jane Doe",
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_ForwardsSearchTermToService()
    {
        // Arrange
        _sut.Search = "blazor";

        // Act
        await _sut.OnGetAsync();

        // Assert
        _blogServiceMock.Verify(s => s.GetPostsAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            "blazor",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_ForwardsPageNumberToService()
    {
        // Arrange
        _sut.PageNumber = 3;

        // Act
        await _sut.OnGetAsync();

        // Assert
        _blogServiceMock.Verify(s => s.GetPostsAsync(
            3,
            It.IsAny<int>(),
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
        _sut.HasActiveFilter.Should().Be(expected);
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
        values["p"].Should().Be("4");
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
        values["Tag"].Should().Be("dotnet");
        values["Category"].Should().Be("tutorials");
        values["Author"].Should().Be("Jane");
        values["Search"].Should().Be("query");
        values["PageSize"].Should().Be("5");
    }

    // ── ShowBranding ──────────────────────────────────────────────────────────

    [Fact]
    public void ShowBranding_WhenNoBlogResolved_ReturnsFalseByDefault()
    {
        // Arrange — default options leave ShowBranding at its default value (false)
        var sut = CreateSut(new Mock<IPostnomicBlogService>());

        // Act & Assert
        sut.ShowBranding.Should().BeFalse();
    }

    [Fact]
    public void ShowBranding_WhenNoBlogResolved_ReturnsValueFromDefaultOptions()
    {
        // Arrange — explicitly enable branding in the default options
        var options = new PostnomicClientOptions { BasePath = "/blog", ShowBranding = true };
        var sut = CreateSut(new Mock<IPostnomicBlogService>(), options);

        // Act & Assert
        sut.ShowBranding.Should().BeTrue();
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
        await sut.OnGetAsync();

        // Assert
        sut.ShowBranding.Should().BeTrue(
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
        await sut.OnGetAsync();

        // Assert
        sut.ShowBranding.Should().BeFalse(
            "the server returned ShowBranding = false so it should take precedence over client config");
    }
}

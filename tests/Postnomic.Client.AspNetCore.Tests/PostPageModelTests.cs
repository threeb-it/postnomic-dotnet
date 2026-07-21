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
using Postnomic.Client.AspNetCore.Tests.TestSupport;

namespace Postnomic.Client.AspNetCore.Tests;

/// <summary>
/// Unit tests for <see cref="PostModel"/>.
/// Verifies that <see cref="PostModel.OnGetAsync"/> loads post data and returns 404 when
/// the post is not found, and that <see cref="PostModel.OnPostAsync"/> submits comments
/// correctly and handles success and failure responses.
/// </summary>
public class PostPageModelTests
{
    private readonly Mock<IPostnomicBlogService> _blogServiceMock;
    private readonly PostModel _sut;

    public PostPageModelTests()
    {
        _blogServiceMock = new Mock<IPostnomicBlogService>();
        _sut = CreateSut(_blogServiceMock);
        SetupDefaultSidebarResponses();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PostModel CreateSut(Mock<IPostnomicBlogService> mock)
        => CreateSut(mock, new PostnomicClientOptions { BasePath = "/blog" });

    private static PostModel CreateSut(Mock<IPostnomicBlogService> mock, PostnomicClientOptions clientOptions)
    {
        var resolver = new Mock<IPostnomicBlogResolver>();
        resolver.Setup(r => r.ResolveBlogName(It.IsAny<string>())).Returns((string?)null);
        var model = new PostModel(
            mock.Object,
            Mock.Of<IServiceProvider>(),
            resolver.Object,
            Options.Create(clientOptions),
            Mock.Of<IOptionsMonitor<PostnomicClientOptions>>(),
            TestStringLocalizers.Post());

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

    private void SetupDefaultSidebarResponses()
    {
        _blogServiceMock
            .Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());

        _blogServiceMock
            .Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());

        _blogServiceMock
            .Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Test Blog", Slug = "test-blog" });
    }

    private void SetupPost(PostnomicPostDetail? post)
    {
        _blogServiceMock
            .Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
    }

    private static PostnomicPostDetail CreateDetail(
        string slug = "test-post",
        string title = "Test Post",
        string author = "Jane Doe",
        string? content = "<p>Content here.</p>",
        bool commentsEnabled = true,
        bool requireModeration = false,
        bool requireFirstname = false,
        bool requireLastname = false,
        bool requireEmail = false,
        bool requirePhone = false,
        bool requireSubject = false) =>
        new()
        {
            Slug = slug,
            Title = title,
            AuthorName = author,
            PublishedAt = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Content = content,
            CommentsEnabled = commentsEnabled,
            CommentRequireModeration = requireModeration,
            CommentRequireFirstname = requireFirstname,
            CommentRequireLastname = requireLastname,
            CommentRequireEmail = requireEmail,
            CommentRequirePhone = requirePhone,
            CommentRequireSubject = requireSubject
        };

    // ── OnGetAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnGetAsync_WhenPostExists_ReturnsPageResult()
    {
        // Arrange
        SetupPost(CreateDetail());
        _sut.PostSlug = "test-post";

        // Act
        var result = await _sut.OnGetAsync();

        // Assert
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_WhenPostExists_PopulatesPost()
    {
        // Arrange
        SetupPost(CreateDetail(title: "Loaded Post"));
        _sut.PostSlug = "loaded-post";

        // Act
        await _sut.OnGetAsync();

        // Assert
        Assert.NotNull(_sut.Post);
        Assert.Equal("Loaded Post", _sut.Post.Title);
    }

    [Fact]
    public async Task OnGetAsync_WhenPostNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupPost(null);
        _sut.PostSlug = "missing-post";

        // Act
        var result = await _sut.OnGetAsync();

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesTopCommented()
    {
        // Arrange
        SetupPost(CreateDetail());
        _blogServiceMock
            .Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>
            {
                new() { Slug = "top", Title = "Top", Count = 99 }
            });

        // Act
        await _sut.OnGetAsync();

        // Assert
        Assert.Single(_sut.TopCommented);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesMostRead()
    {
        // Arrange
        SetupPost(CreateDetail());
        _blogServiceMock
            .Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>
            {
                new() { Slug = "viral", Title = "Viral", Count = 5000 }
            });

        // Act
        await _sut.OnGetAsync();

        // Assert
        Assert.Single(_sut.MostRead);
    }

    [Fact]
    public async Task OnGetAsync_WhenContentIsNull_SetsEstimatedReadMinutesToZero()
    {
        // Arrange
        SetupPost(CreateDetail(content: null));

        // Act
        await _sut.OnGetAsync();

        // Assert
        Assert.Equal(0, _sut.EstimatedReadMinutes);
    }

    [Theory]
    [InlineData("<p>one two three four five</p>", 1)]    // 5 words → 1 min
    [InlineData(null, 0)]                                 // no content → 0
    public async Task OnGetAsync_CalculatesEstimatedReadMinutes(string? content, int expectedMinutes)
    {
        // Arrange
        SetupPost(CreateDetail(content: content));

        // Act
        await _sut.OnGetAsync();

        // Assert
        Assert.Equal(expectedMinutes, _sut.EstimatedReadMinutes);
    }

    // ── OnPostAsync — comment submission ──────────────────────────────────────

    [Fact]
    public async Task OnPostAsync_WhenCommentAccepted_SetsSuccessMessage()
    {
        // Arrange
        SetupPost(CreateDetail(requireModeration: false));
        _sut.PostSlug = "test-post";
        _sut.CommentInput = new CommentInputModel { Body = "Great article!" };

        _blogServiceMock
            .Setup(s => s.CreateCommentAsync(
                It.IsAny<string>(),
                It.IsAny<PostnomicCreateCommentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicComment
            {
                PublicId = "new-comment",
                Body = "Great article!",
                CreatedAt = DateTime.UtcNow
            });

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.False(string.IsNullOrEmpty(_sut.CommentSubmitSuccessMessage));
        Assert.True(string.IsNullOrEmpty(_sut.CommentSubmitErrorMessage));
    }

    [Fact]
    public async Task OnPostAsync_WhenCommentAcceptedAndModerationRequired_SetsAwaitingModerationMessage()
    {
        // Arrange
        SetupPost(CreateDetail(requireModeration: true));
        _sut.PostSlug = "test-post";
        _sut.CommentInput = new CommentInputModel { Body = "Pending comment" };

        _blogServiceMock
            .Setup(s => s.CreateCommentAsync(
                It.IsAny<string>(),
                It.IsAny<PostnomicCreateCommentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicComment
            {
                PublicId = "pending",
                Body = "Pending comment",
                CreatedAt = DateTime.UtcNow
            });

        // Act
        await _sut.OnPostAsync();

        // Assert
        Assert.Contains("moderation", _sut.CommentSubmitSuccessMessage);
    }

    [Fact]
    public async Task OnPostAsync_WhenCommentAcceptedWithoutModeration_SetsPublishedMessage()
    {
        // Arrange
        SetupPost(CreateDetail(requireModeration: false));
        _sut.PostSlug = "test-post";
        _sut.CommentInput = new CommentInputModel { Body = "Instant comment" };

        _blogServiceMock
            .Setup(s => s.CreateCommentAsync(
                It.IsAny<string>(),
                It.IsAny<PostnomicCreateCommentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicComment
            {
                PublicId = "pub",
                Body = "Instant comment",
                CreatedAt = DateTime.UtcNow
            });

        // Act
        await _sut.OnPostAsync();

        // Assert
        Assert.Contains("published", _sut.CommentSubmitSuccessMessage);
    }

    [Fact]
    public async Task OnPostAsync_WhenServiceReturnsNull_SetsErrorMessage()
    {
        // Arrange
        SetupPost(CreateDetail());
        _sut.PostSlug = "test-post";
        _sut.CommentInput = new CommentInputModel { Body = "Valid body" };

        _blogServiceMock
            .Setup(s => s.CreateCommentAsync(
                It.IsAny<string>(),
                It.IsAny<PostnomicCreateCommentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostnomicComment?)null);

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.False(string.IsNullOrEmpty(_sut.CommentSubmitErrorMessage));
        Assert.True(string.IsNullOrEmpty(_sut.CommentSubmitSuccessMessage));
    }

    [Fact]
    public async Task OnPostAsync_WhenPostSlugIsInvalid_ReturnsNotFound()
    {
        // Arrange
        SetupPost(null);
        _sut.PostSlug = "nonexistent";
        _sut.CommentInput = new CommentInputModel { Body = "Body" };

        // Act
        var result = await _sut.OnPostAsync();

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_ForwardsCommentBodyToService()
    {
        // Arrange
        SetupPost(CreateDetail());
        _sut.PostSlug = "test-post";
        _sut.CommentInput = new CommentInputModel
        {
            Body = "My detailed comment body"
        };

        PostnomicCreateCommentRequest? capturedRequest = null;
        _blogServiceMock
            .Setup(s => s.CreateCommentAsync(
                It.IsAny<string>(),
                It.IsAny<PostnomicCreateCommentRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, PostnomicCreateCommentRequest, CancellationToken>(
                (_, req, _) => capturedRequest = req)
            .ReturnsAsync(new PostnomicComment
            {
                PublicId = "c1",
                Body = "My detailed comment body",
                CreatedAt = DateTime.UtcNow
            });

        // Act
        await _sut.OnPostAsync();

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("My detailed comment body", capturedRequest!.Body);
    }

    [Fact]
    public async Task OnPostAsync_WhenRequiredFieldMissing_SetsModelStateError()
    {
        // Arrange — post requires first name but CommentInput.Firstname is empty
        SetupPost(CreateDetail(requireFirstname: true));
        _sut.PostSlug = "test-post";
        _sut.CommentInput = new CommentInputModel
        {
            Body = "Valid body",
            Firstname = null
        };

        // Act
        await _sut.OnPostAsync();

        // Assert
        Assert.False(_sut.ModelState.IsValid);
        Assert.False(string.IsNullOrEmpty(_sut.CommentSubmitErrorMessage));
    }

    [Fact]
    public async Task OnPostAsync_WhenEmailRequiredAndMissing_SetsModelStateError()
    {
        // Arrange
        SetupPost(CreateDetail(requireEmail: true));
        _sut.PostSlug = "test-post";
        _sut.CommentInput = new CommentInputModel
        {
            Body = "Body text",
            Email = null
        };

        // Act
        await _sut.OnPostAsync();

        // Assert
        Assert.False(_sut.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostAsync_WhenSubjectRequiredAndMissing_SetsModelStateError()
    {
        // Arrange
        SetupPost(CreateDetail(requireSubject: true));
        _sut.PostSlug = "test-post";
        _sut.CommentInput = new CommentInputModel
        {
            Body = "Body text",
            Subject = null
        };

        // Act
        await _sut.OnPostAsync();

        // Assert
        Assert.False(_sut.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostAsync_AfterSuccessfulSubmit_ResetsCommentInputForm()
    {
        // Arrange
        SetupPost(CreateDetail());
        _sut.PostSlug = "test-post";
        _sut.CommentInput = new CommentInputModel
        {
            Body = "Original body",
            Firstname = "Jane"
        };

        _blogServiceMock
            .Setup(s => s.CreateCommentAsync(
                It.IsAny<string>(),
                It.IsAny<PostnomicCreateCommentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicComment
            {
                PublicId = "new",
                Body = "Original body",
                CreatedAt = DateTime.UtcNow
            });

        // Act
        await _sut.OnPostAsync();

        // Assert — form should be reset to defaults
        Assert.Equal(string.Empty, _sut.CommentInput.Body);
        Assert.Null(_sut.CommentInput.Firstname);
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
        mock.Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetail());
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());

        var sut = CreateSut(mock);
        sut.PostSlug = "test-post";

        // Act
        await sut.OnGetAsync();

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
        mock.Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetail());
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());

        // Even though client options say ShowBranding = true, the server value wins
        var options = new PostnomicClientOptions { BasePath = "/blog", ShowBranding = true };
        var sut = CreateSut(mock, options);
        sut.PostSlug = "test-post";

        // Act
        await sut.OnGetAsync();

        // Assert
        Assert.False(sut.ShowBranding,
            "the server returned ShowBranding = false so it should take precedence over client config");
    }

    [Fact]
    public async Task ShowBranding_ServerValueOverridesClientConfig()
    {
        // Arrange: client config says show branding, but server says don't
        var mock = new Mock<IPostnomicBlogService>();
        mock.Setup(s => s.GetBlogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostnomicBlogInfo { Name = "Paid Blog", Slug = "paid-blog", ShowBranding = false });
        mock.Setup(s => s.GetPostAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetail());
        mock.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());
        mock.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostnomicPopularPost>());

        var options = new PostnomicClientOptions { BasePath = "/blog", ShowBranding = true };
        var sut = CreateSut(mock, options);
        sut.PostSlug = "test-post";

        // Act
        await sut.OnGetAsync();

        // Assert: server value (false) wins over client config (true)
        Assert.False(sut.ShowBranding,
            "the server-enforced value should override the client configuration");
    }

    // ── CanonicalUrl ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CanonicalUrl_WhenApiSuppliesCanonicalUrl_UsesApiValue()
    {
        // Arrange — the post is cross-posted; the API returns the primary blog's absolute URL
        SetupPost(CreateDetail(slug: "gitkraken-tips") with
        {
            CanonicalUrl = "https://a.example.com/blog/gitkraken-tips"
        });
        _sut.PostSlug = "gitkraken-tips";

        // Act
        await _sut.OnGetAsync();

        // Assert
        Assert.Equal("https://a.example.com/blog/gitkraken-tips", _sut.CanonicalUrl);
    }

    [Fact]
    public async Task CanonicalUrl_WhenApiCanonicalUrlIsNull_FallsBackToHostComputedUrl()
    {
        // Arrange — existing behavior: no API-provided canonical, so the host computes it
        SetupPost(CreateDetail(slug: "gitkraken-tips"));
        _sut.PostSlug = "gitkraken-tips";

        // Act
        await _sut.OnGetAsync();

        // Assert
        var expected = PostnomicRouteBuilder.BuildPost(_sut.BasePath, _sut.RouteStyle, lang: null, _sut.PostSlug);
        Assert.Equal(expected, _sut.CanonicalUrl);
    }
}

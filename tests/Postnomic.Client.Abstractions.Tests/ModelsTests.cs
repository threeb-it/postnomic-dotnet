using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.Abstractions.Tests;

/// <summary>
/// Tests for all model record types in <c>Postnomic.Client.Abstractions.Models</c>.
/// Verifies that records can be instantiated with expected property values, that
/// collection properties default to empty (never null), and that with-expressions
/// produce correctly mutated copies.
/// </summary>
public class ModelsTests
{
    // ── PostnomicBlogInfo ─────────────────────────────────────────────────────

    [Fact]
    public void PostnomicBlogInfo_Instantiation_SetsRequiredProperties()
    {
        // Arrange & Act
        var blog = new PostnomicBlogInfo
        {
            Name = "My Blog",
            Slug = "my-blog",
            Description = "A short description"
        };

        // Assert
        Assert.Equal("My Blog", blog.Name);
        Assert.Equal("my-blog", blog.Slug);
        Assert.Equal("A short description", blog.Description);
    }

    [Fact]
    public void PostnomicBlogInfo_Description_DefaultsToNull()
    {
        // Arrange & Act
        var blog = new PostnomicBlogInfo { Name = "Blog", Slug = "blog" };

        // Assert
        Assert.Null(blog.Description);
    }

    [Fact]
    public void PostnomicBlogInfo_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicBlogInfo { Name = "Old Name", Slug = "old-slug" };

        // Act
        var updated = original with { Name = "New Name" };

        // Assert
        Assert.Equal("New Name", updated.Name);
        Assert.Equal("old-slug", updated.Slug);
        Assert.Equal("Old Name", original.Name);
    }

    // ── PostnomicTag ──────────────────────────────────────────────────────────

    [Fact]
    public void PostnomicTag_Instantiation_SetsAllProperties()
    {
        // Arrange & Act
        var tag = new PostnomicTag { Name = "C# Tips", Slug = "csharp-tips", PostCount = 7 };

        // Assert
        Assert.Equal("C# Tips", tag.Name);
        Assert.Equal("csharp-tips", tag.Slug);
        Assert.Equal(7, tag.PostCount);
    }

    [Fact]
    public void PostnomicTag_PostCount_DefaultsToZero()
    {
        // Arrange & Act
        var tag = new PostnomicTag { Name = "Tag", Slug = "tag" };

        // Assert
        Assert.Equal(0, tag.PostCount);
    }

    [Fact]
    public void PostnomicTag_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicTag { Name = "Tag", Slug = "tag", PostCount = 3 };

        // Act
        var updated = original with { PostCount = 10 };

        // Assert
        Assert.Equal(10, updated.PostCount);
        Assert.Equal("Tag", updated.Name);
        Assert.Equal(3, original.PostCount);
    }

    // ── PostnomicCategory ─────────────────────────────────────────────────────

    [Fact]
    public void PostnomicCategory_Instantiation_SetsAllProperties()
    {
        // Arrange & Act
        var category = new PostnomicCategory
        {
            Name = "Tutorials",
            Slug = "tutorials",
            PostCount = 12
        };

        // Assert
        Assert.Equal("Tutorials", category.Name);
        Assert.Equal("tutorials", category.Slug);
        Assert.Equal(12, category.PostCount);
    }

    [Fact]
    public void PostnomicCategory_PostCount_DefaultsToZero()
    {
        // Arrange & Act
        var category = new PostnomicCategory { Name = "Cat", Slug = "cat" };

        // Assert
        Assert.Equal(0, category.PostCount);
    }

    [Fact]
    public void PostnomicCategory_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicCategory { Name = "Cat", Slug = "cat", PostCount = 5 };

        // Act
        var updated = original with { Slug = "new-cat" };

        // Assert
        Assert.Equal("new-cat", updated.Slug);
        Assert.Equal("cat", original.Slug);
    }

    // ── PostnomicAuthor ───────────────────────────────────────────────────────

    [Fact]
    public void PostnomicAuthor_Instantiation_SetsAllProperties()
    {
        // Arrange & Act
        var author = new PostnomicAuthor { Name = "Jane Doe", PostCount = 4 };

        // Assert
        Assert.Equal("Jane Doe", author.Name);
        Assert.Equal(4, author.PostCount);
    }

    [Fact]
    public void PostnomicAuthor_PostCount_DefaultsToZero()
    {
        // Arrange & Act
        var author = new PostnomicAuthor { Name = "Anonymous" };

        // Assert
        Assert.Equal(0, author.PostCount);
    }

    [Fact]
    public void PostnomicAuthor_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicAuthor { Name = "Jane", PostCount = 2 };

        // Act
        var updated = original with { Name = "John" };

        // Assert
        Assert.Equal("John", updated.Name);
        Assert.Equal(2, updated.PostCount);
        Assert.Equal("Jane", original.Name);
    }

    // ── PostnomicPostSummary ──────────────────────────────────────────────────

    [Fact]
    public void PostnomicPostSummary_Instantiation_SetsRequiredProperties()
    {
        // Arrange & Act
        var summary = new PostnomicPostSummary
        {
            Slug = "my-first-post",
            Title = "My First Post",
            AuthorName = "Jane Doe",
            PublishedAt = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        };

        // Assert
        Assert.Equal("my-first-post", summary.Slug);
        Assert.Equal("My First Post", summary.Title);
        Assert.Equal("Jane Doe", summary.AuthorName);
        Assert.Equal(new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc), summary.PublishedAt);
    }

    [Fact]
    public void PostnomicPostSummary_OptionalProperties_DefaultToNull()
    {
        // Arrange & Act
        var summary = new PostnomicPostSummary
        {
            Slug = "slug",
            Title = "Title",
            AuthorName = "Author"
        };

        // Assert
        Assert.Null(summary.Excerpt);
        Assert.Null(summary.ThumbnailImageUrl);
    }

    [Fact]
    public void PostnomicPostSummary_Tags_DefaultsToEmptyCollection()
    {
        // Arrange & Act
        var summary = new PostnomicPostSummary
        {
            Slug = "slug",
            Title = "Title",
            AuthorName = "Author"
        };

        // Assert
        Assert.NotNull(summary.Tags);
        Assert.Empty(summary.Tags);
    }

    [Fact]
    public void PostnomicPostSummary_Categories_DefaultsToEmptyCollection()
    {
        // Arrange & Act
        var summary = new PostnomicPostSummary
        {
            Slug = "slug",
            Title = "Title",
            AuthorName = "Author"
        };

        // Assert
        Assert.NotNull(summary.Categories);
        Assert.Empty(summary.Categories);
    }

    [Fact]
    public void PostnomicPostSummary_CommentCount_DefaultsToZero()
    {
        // Arrange & Act
        var summary = new PostnomicPostSummary
        {
            Slug = "slug",
            Title = "Title",
            AuthorName = "Author"
        };

        // Assert
        Assert.Equal(0, summary.CommentCount);
    }

    [Fact]
    public void PostnomicPostSummary_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicPostSummary
        {
            Slug = "original",
            Title = "Original Title",
            AuthorName = "Author",
            CommentCount = 3
        };

        // Act
        var updated = original with { Title = "Updated Title", CommentCount = 5 };

        // Assert
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal(5, updated.CommentCount);
        Assert.Equal("original", updated.Slug);
        Assert.Equal("Original Title", original.Title);
    }

    // ── PostnomicPostDetail ───────────────────────────────────────────────────

    [Fact]
    public void PostnomicPostDetail_Instantiation_SetsRequiredProperties()
    {
        // Arrange & Act
        var detail = new PostnomicPostDetail
        {
            Slug = "detail-post",
            Title = "Detail Post",
            AuthorName = "John Smith",
            PublishedAt = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Assert
        Assert.Equal("detail-post", detail.Slug);
        Assert.Equal("Detail Post", detail.Title);
        Assert.Equal("John Smith", detail.AuthorName);
    }

    [Fact]
    public void PostnomicPostDetail_OptionalProperties_DefaultToNull()
    {
        // Arrange & Act
        var detail = new PostnomicPostDetail
        {
            Slug = "slug",
            Title = "Title",
            AuthorName = "Author"
        };

        // Assert
        Assert.Null(detail.Content);
        Assert.Null(detail.Excerpt);
        Assert.Null(detail.CoverImageUrl);
    }

    [Fact]
    public void PostnomicPostDetail_BoolProperties_DefaultToFalse()
    {
        // Arrange & Act
        var detail = new PostnomicPostDetail
        {
            Slug = "slug",
            Title = "Title",
            AuthorName = "Author"
        };

        // Assert
        Assert.False(detail.CommentsEnabled);
        Assert.False(detail.CommentRequireModeration);
        Assert.False(detail.CommentRequireFirstname);
        Assert.False(detail.CommentRequireLastname);
        Assert.False(detail.CommentRequireEmail);
        Assert.False(detail.CommentRequirePhone);
        Assert.False(detail.CommentRequireSubject);
    }

    [Fact]
    public void PostnomicPostDetail_Tags_DefaultsToEmptyCollection()
    {
        // Arrange & Act
        var detail = new PostnomicPostDetail
        {
            Slug = "slug",
            Title = "Title",
            AuthorName = "Author"
        };

        // Assert
        Assert.NotNull(detail.Tags);
        Assert.Empty(detail.Tags);
    }

    [Fact]
    public void PostnomicPostDetail_Categories_DefaultsToEmptyCollection()
    {
        // Arrange & Act
        var detail = new PostnomicPostDetail
        {
            Slug = "slug",
            Title = "Title",
            AuthorName = "Author"
        };

        // Assert
        Assert.NotNull(detail.Categories);
        Assert.Empty(detail.Categories);
    }

    [Fact]
    public void PostnomicPostDetail_Comments_DefaultsToEmptyCollection()
    {
        // Arrange & Act
        var detail = new PostnomicPostDetail
        {
            Slug = "slug",
            Title = "Title",
            AuthorName = "Author"
        };

        // Assert
        Assert.NotNull(detail.Comments);
        Assert.Empty(detail.Comments);
    }

    [Fact]
    public void PostnomicPostDetail_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicPostDetail
        {
            Slug = "slug",
            Title = "Title",
            AuthorName = "Author",
            CommentsEnabled = false
        };

        // Act
        var updated = original with { CommentsEnabled = true, Content = "<p>Hello</p>" };

        // Assert
        Assert.True(updated.CommentsEnabled);
        Assert.Equal("<p>Hello</p>", updated.Content);
        Assert.False(original.CommentsEnabled);
    }

    // ── PostnomicComment ──────────────────────────────────────────────────────

    [Fact]
    public void PostnomicComment_Instantiation_SetsRequiredProperties()
    {
        // Arrange & Act
        var comment = new PostnomicComment
        {
            PublicId = "abc-123",
            Body = "Great post!",
            CreatedAt = new DateTime(2025, 2, 20, 9, 0, 0, DateTimeKind.Utc)
        };

        // Assert
        Assert.Equal("abc-123", comment.PublicId);
        Assert.Equal("Great post!", comment.Body);
        Assert.Equal(new DateTime(2025, 2, 20, 9, 0, 0, DateTimeKind.Utc), comment.CreatedAt);
    }

    [Fact]
    public void PostnomicComment_OptionalProperties_DefaultToNull()
    {
        // Arrange & Act
        var comment = new PostnomicComment { PublicId = "id", Body = "Body" };

        // Assert
        Assert.Null(comment.Subject);
        Assert.Null(comment.AuthorName);
    }

    [Fact]
    public void PostnomicComment_Replies_DefaultsToEmptyCollection()
    {
        // Arrange & Act
        var comment = new PostnomicComment { PublicId = "id", Body = "Body" };

        // Assert
        Assert.NotNull(comment.Replies);
        Assert.Empty(comment.Replies);
    }

    [Fact]
    public void PostnomicComment_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicComment { PublicId = "id", Body = "Original body" };

        // Act
        var updated = original with { Body = "Updated body", AuthorName = "Jane" };

        // Assert
        Assert.Equal("Updated body", updated.Body);
        Assert.Equal("Jane", updated.AuthorName);
        Assert.Equal("Original body", original.Body);
    }

    // ── PostnomicPopularPost ──────────────────────────────────────────────────

    [Fact]
    public void PostnomicPopularPost_Instantiation_SetsAllProperties()
    {
        // Arrange & Act
        var post = new PostnomicPopularPost
        {
            Slug = "popular-post",
            Title = "Popular Post",
            Count = 42
        };

        // Assert
        Assert.Equal("popular-post", post.Slug);
        Assert.Equal("Popular Post", post.Title);
        Assert.Equal(42, post.Count);
    }

    [Fact]
    public void PostnomicPopularPost_Count_DefaultsToZero()
    {
        // Arrange & Act
        var post = new PostnomicPopularPost { Slug = "slug", Title = "Title" };

        // Assert
        Assert.Equal(0, post.Count);
    }

    [Fact]
    public void PostnomicPopularPost_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicPopularPost { Slug = "slug", Title = "Title", Count = 5 };

        // Act
        var updated = original with { Count = 99 };

        // Assert
        Assert.Equal(99, updated.Count);
        Assert.Equal(5, original.Count);
    }

    // ── PostnomicPagedResult<T> ───────────────────────────────────────────────

    [Fact]
    public void PostnomicPagedResult_Instantiation_SetsAllProperties()
    {
        // Arrange
        var items = new List<PostnomicPostSummary>
        {
            new() { Slug = "a", Title = "A", AuthorName = "Author" }
        };

        // Act
        var result = new PostnomicPagedResult<PostnomicPostSummary>
        {
            Items = items,
            Page = 2,
            PageSize = 10,
            TotalCount = 21,
            TotalPages = 3
        };

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(21, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void PostnomicPagedResult_NumericProperties_DefaultToZero()
    {
        // Arrange & Act
        var result = new PostnomicPagedResult<PostnomicTag> { Items = [] };

        // Assert
        Assert.Equal(0, result.Page);
        Assert.Equal(0, result.PageSize);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void PostnomicPagedResult_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicPagedResult<PostnomicTag>
        {
            Items = [],
            Page = 1,
            PageSize = 5,
            TotalCount = 0,
            TotalPages = 0
        };

        // Act
        var updated = original with { Page = 2, TotalCount = 7, TotalPages = 2 };

        // Assert
        Assert.Equal(2, updated.Page);
        Assert.Equal(7, updated.TotalCount);
        Assert.Equal(2, updated.TotalPages);
        Assert.Equal(1, original.Page);
    }

    // ── PostnomicCreateCommentRequest ─────────────────────────────────────────

    [Fact]
    public void PostnomicCreateCommentRequest_Instantiation_SetsRequiredBody()
    {
        // Arrange & Act
        var request = new PostnomicCreateCommentRequest { Body = "This is my comment." };

        // Assert
        Assert.Equal("This is my comment.", request.Body);
    }

    [Fact]
    public void PostnomicCreateCommentRequest_OptionalProperties_DefaultToNull()
    {
        // Arrange & Act
        var request = new PostnomicCreateCommentRequest { Body = "Body" };

        // Assert
        Assert.Null(request.ParentCommentPublicId);
        Assert.Null(request.Subject);
        Assert.Null(request.AuthorFirstname);
        Assert.Null(request.AuthorLastname);
        Assert.Null(request.AuthorEmail);
        Assert.Null(request.AuthorPhone);
    }

    [Fact]
    public void PostnomicCreateCommentRequest_AllProperties_CanBeSet()
    {
        // Arrange & Act
        var request = new PostnomicCreateCommentRequest
        {
            ParentCommentPublicId = "parent-id",
            Subject = "Re: Great post",
            Body = "Thanks for the article!",
            AuthorFirstname = "Jane",
            AuthorLastname = "Doe",
            AuthorEmail = "jane@example.com",
            AuthorPhone = "+1-555-0100"
        };

        // Assert
        Assert.Equal("parent-id", request.ParentCommentPublicId);
        Assert.Equal("Re: Great post", request.Subject);
        Assert.Equal("Thanks for the article!", request.Body);
        Assert.Equal("Jane", request.AuthorFirstname);
        Assert.Equal("Doe", request.AuthorLastname);
        Assert.Equal("jane@example.com", request.AuthorEmail);
        Assert.Equal("+1-555-0100", request.AuthorPhone);
    }

    [Fact]
    public void PostnomicCreateCommentRequest_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicCreateCommentRequest
        {
            Body = "Original body",
            AuthorFirstname = "Jane"
        };

        // Act
        var updated = original with { AuthorFirstname = "John", AuthorLastname = "Doe" };

        // Assert
        Assert.Equal("John", updated.AuthorFirstname);
        Assert.Equal("Doe", updated.AuthorLastname);
        Assert.Equal("Original body", updated.Body);
        Assert.Equal("Jane", original.AuthorFirstname);
    }
}

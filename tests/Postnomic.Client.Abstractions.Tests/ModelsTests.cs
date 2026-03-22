using FluentAssertions;
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
        blog.Name.Should().Be("My Blog");
        blog.Slug.Should().Be("my-blog");
        blog.Description.Should().Be("A short description");
    }

    [Fact]
    public void PostnomicBlogInfo_Description_DefaultsToNull()
    {
        // Arrange & Act
        var blog = new PostnomicBlogInfo { Name = "Blog", Slug = "blog" };

        // Assert
        blog.Description.Should().BeNull();
    }

    [Fact]
    public void PostnomicBlogInfo_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicBlogInfo { Name = "Old Name", Slug = "old-slug" };

        // Act
        var updated = original with { Name = "New Name" };

        // Assert
        updated.Name.Should().Be("New Name");
        updated.Slug.Should().Be("old-slug");
        original.Name.Should().Be("Old Name");
    }

    // ── PostnomicTag ──────────────────────────────────────────────────────────

    [Fact]
    public void PostnomicTag_Instantiation_SetsAllProperties()
    {
        // Arrange & Act
        var tag = new PostnomicTag { Name = "C# Tips", Slug = "csharp-tips", PostCount = 7 };

        // Assert
        tag.Name.Should().Be("C# Tips");
        tag.Slug.Should().Be("csharp-tips");
        tag.PostCount.Should().Be(7);
    }

    [Fact]
    public void PostnomicTag_PostCount_DefaultsToZero()
    {
        // Arrange & Act
        var tag = new PostnomicTag { Name = "Tag", Slug = "tag" };

        // Assert
        tag.PostCount.Should().Be(0);
    }

    [Fact]
    public void PostnomicTag_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicTag { Name = "Tag", Slug = "tag", PostCount = 3 };

        // Act
        var updated = original with { PostCount = 10 };

        // Assert
        updated.PostCount.Should().Be(10);
        updated.Name.Should().Be("Tag");
        original.PostCount.Should().Be(3);
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
        category.Name.Should().Be("Tutorials");
        category.Slug.Should().Be("tutorials");
        category.PostCount.Should().Be(12);
    }

    [Fact]
    public void PostnomicCategory_PostCount_DefaultsToZero()
    {
        // Arrange & Act
        var category = new PostnomicCategory { Name = "Cat", Slug = "cat" };

        // Assert
        category.PostCount.Should().Be(0);
    }

    [Fact]
    public void PostnomicCategory_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicCategory { Name = "Cat", Slug = "cat", PostCount = 5 };

        // Act
        var updated = original with { Slug = "new-cat" };

        // Assert
        updated.Slug.Should().Be("new-cat");
        original.Slug.Should().Be("cat");
    }

    // ── PostnomicAuthor ───────────────────────────────────────────────────────

    [Fact]
    public void PostnomicAuthor_Instantiation_SetsAllProperties()
    {
        // Arrange & Act
        var author = new PostnomicAuthor { Name = "Jane Doe", PostCount = 4 };

        // Assert
        author.Name.Should().Be("Jane Doe");
        author.PostCount.Should().Be(4);
    }

    [Fact]
    public void PostnomicAuthor_PostCount_DefaultsToZero()
    {
        // Arrange & Act
        var author = new PostnomicAuthor { Name = "Anonymous" };

        // Assert
        author.PostCount.Should().Be(0);
    }

    [Fact]
    public void PostnomicAuthor_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicAuthor { Name = "Jane", PostCount = 2 };

        // Act
        var updated = original with { Name = "John" };

        // Assert
        updated.Name.Should().Be("John");
        updated.PostCount.Should().Be(2);
        original.Name.Should().Be("Jane");
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
        summary.Slug.Should().Be("my-first-post");
        summary.Title.Should().Be("My First Post");
        summary.AuthorName.Should().Be("Jane Doe");
        summary.PublishedAt.Should().Be(new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc));
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
        summary.Excerpt.Should().BeNull();
        summary.ThumbnailImageUrl.Should().BeNull();
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
        summary.Tags.Should().NotBeNull();
        summary.Tags.Should().BeEmpty();
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
        summary.Categories.Should().NotBeNull();
        summary.Categories.Should().BeEmpty();
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
        summary.CommentCount.Should().Be(0);
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
        updated.Title.Should().Be("Updated Title");
        updated.CommentCount.Should().Be(5);
        updated.Slug.Should().Be("original");
        original.Title.Should().Be("Original Title");
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
        detail.Slug.Should().Be("detail-post");
        detail.Title.Should().Be("Detail Post");
        detail.AuthorName.Should().Be("John Smith");
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
        detail.Content.Should().BeNull();
        detail.Excerpt.Should().BeNull();
        detail.CoverImageUrl.Should().BeNull();
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
        detail.CommentsEnabled.Should().BeFalse();
        detail.CommentRequireModeration.Should().BeFalse();
        detail.CommentRequireFirstname.Should().BeFalse();
        detail.CommentRequireLastname.Should().BeFalse();
        detail.CommentRequireEmail.Should().BeFalse();
        detail.CommentRequirePhone.Should().BeFalse();
        detail.CommentRequireSubject.Should().BeFalse();
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
        detail.Tags.Should().NotBeNull();
        detail.Tags.Should().BeEmpty();
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
        detail.Categories.Should().NotBeNull();
        detail.Categories.Should().BeEmpty();
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
        detail.Comments.Should().NotBeNull();
        detail.Comments.Should().BeEmpty();
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
        updated.CommentsEnabled.Should().BeTrue();
        updated.Content.Should().Be("<p>Hello</p>");
        original.CommentsEnabled.Should().BeFalse();
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
        comment.PublicId.Should().Be("abc-123");
        comment.Body.Should().Be("Great post!");
        comment.CreatedAt.Should().Be(new DateTime(2025, 2, 20, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void PostnomicComment_OptionalProperties_DefaultToNull()
    {
        // Arrange & Act
        var comment = new PostnomicComment { PublicId = "id", Body = "Body" };

        // Assert
        comment.Subject.Should().BeNull();
        comment.AuthorName.Should().BeNull();
    }

    [Fact]
    public void PostnomicComment_Replies_DefaultsToEmptyCollection()
    {
        // Arrange & Act
        var comment = new PostnomicComment { PublicId = "id", Body = "Body" };

        // Assert
        comment.Replies.Should().NotBeNull();
        comment.Replies.Should().BeEmpty();
    }

    [Fact]
    public void PostnomicComment_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicComment { PublicId = "id", Body = "Original body" };

        // Act
        var updated = original with { Body = "Updated body", AuthorName = "Jane" };

        // Assert
        updated.Body.Should().Be("Updated body");
        updated.AuthorName.Should().Be("Jane");
        original.Body.Should().Be("Original body");
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
        post.Slug.Should().Be("popular-post");
        post.Title.Should().Be("Popular Post");
        post.Count.Should().Be(42);
    }

    [Fact]
    public void PostnomicPopularPost_Count_DefaultsToZero()
    {
        // Arrange & Act
        var post = new PostnomicPopularPost { Slug = "slug", Title = "Title" };

        // Assert
        post.Count.Should().Be(0);
    }

    [Fact]
    public void PostnomicPopularPost_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicPopularPost { Slug = "slug", Title = "Title", Count = 5 };

        // Act
        var updated = original with { Count = 99 };

        // Assert
        updated.Count.Should().Be(99);
        original.Count.Should().Be(5);
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
        result.Items.Should().HaveCount(1);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(21);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public void PostnomicPagedResult_NumericProperties_DefaultToZero()
    {
        // Arrange & Act
        var result = new PostnomicPagedResult<PostnomicTag> { Items = [] };

        // Assert
        result.Page.Should().Be(0);
        result.PageSize.Should().Be(0);
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
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
        updated.Page.Should().Be(2);
        updated.TotalCount.Should().Be(7);
        updated.TotalPages.Should().Be(2);
        original.Page.Should().Be(1);
    }

    // ── PostnomicCreateCommentRequest ─────────────────────────────────────────

    [Fact]
    public void PostnomicCreateCommentRequest_Instantiation_SetsRequiredBody()
    {
        // Arrange & Act
        var request = new PostnomicCreateCommentRequest { Body = "This is my comment." };

        // Assert
        request.Body.Should().Be("This is my comment.");
    }

    [Fact]
    public void PostnomicCreateCommentRequest_OptionalProperties_DefaultToNull()
    {
        // Arrange & Act
        var request = new PostnomicCreateCommentRequest { Body = "Body" };

        // Assert
        request.ParentCommentPublicId.Should().BeNull();
        request.Subject.Should().BeNull();
        request.AuthorFirstname.Should().BeNull();
        request.AuthorLastname.Should().BeNull();
        request.AuthorEmail.Should().BeNull();
        request.AuthorPhone.Should().BeNull();
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
        request.ParentCommentPublicId.Should().Be("parent-id");
        request.Subject.Should().Be("Re: Great post");
        request.Body.Should().Be("Thanks for the article!");
        request.AuthorFirstname.Should().Be("Jane");
        request.AuthorLastname.Should().Be("Doe");
        request.AuthorEmail.Should().Be("jane@example.com");
        request.AuthorPhone.Should().Be("+1-555-0100");
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
        updated.AuthorFirstname.Should().Be("John");
        updated.AuthorLastname.Should().Be("Doe");
        updated.Body.Should().Be("Original body");
        original.AuthorFirstname.Should().Be("Jane");
    }
}

using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.Abstractions.Tests;

/// <summary>
/// Tests for the authoring model record types added alongside <see cref="IPostnomicAuthoringService"/>
/// in <c>Postnomic.Client.Abstractions.Models</c>. Verifies instantiation, default values, and
/// with-expression copy semantics, mirroring the conventions in <see cref="ModelsTests"/>.
/// </summary>
public class AuthoringModelsTests
{
    // ── PostnomicPost ─────────────────────────────────────────────────────────

    [Fact]
    public void PostnomicPost_Instantiation_SetsRequiredProperties()
    {
        // Arrange & Act
        var post = new PostnomicPost
        {
            PublicId = "post-1",
            Title = "My Post",
            Slug = "my-post",
            Status = PostnomicPostStatus.Draft,
            AuthorIdentityUserId = "auth0|abc",
            PrimaryBlogPublicId = "blog-1",
            CanonicalUrl = "https://example.com/my-post"
        };

        // Assert
        Assert.Equal("post-1", post.PublicId);
        Assert.Equal("My Post", post.Title);
        Assert.Equal("my-post", post.Slug);
        Assert.Equal(PostnomicPostStatus.Draft, post.Status);
    }

    [Fact]
    public void PostnomicPost_OptionalProperties_DefaultToNull()
    {
        // Arrange & Act
        var post = new PostnomicPost
        {
            PublicId = "id",
            Title = "T",
            Slug = "s",
            Status = PostnomicPostStatus.Draft,
            AuthorIdentityUserId = "auth0|abc",
            PrimaryBlogPublicId = "blog-1",
            CanonicalUrl = "https://example.com/s"
        };

        // Assert
        Assert.Null(post.Content);
        Assert.Null(post.Excerpt);
        Assert.Null(post.CoverImageUrl);
        Assert.Null(post.PublishedAt);
        Assert.Null(post.UpdatedAt);
    }

    [Fact]
    public void PostnomicPost_TagsAndCategories_DefaultToEmptyCollections()
    {
        // Arrange & Act
        var post = new PostnomicPost
        {
            PublicId = "id",
            Title = "T",
            Slug = "s",
            Status = PostnomicPostStatus.Draft,
            AuthorIdentityUserId = "auth0|abc",
            PrimaryBlogPublicId = "blog-1",
            CanonicalUrl = "https://example.com/s"
        };

        // Assert
        Assert.NotNull(post.Tags);
        Assert.Empty(post.Tags);
        Assert.NotNull(post.Categories);
        Assert.Empty(post.Categories);
    }

    [Fact]
    public void PostnomicPost_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicPost
        {
            PublicId = "id",
            Title = "Original",
            Slug = "s",
            Status = PostnomicPostStatus.Draft,
            AuthorIdentityUserId = "auth0|abc",
            PrimaryBlogPublicId = "blog-1",
            CanonicalUrl = "https://example.com/s"
        };

        // Act
        var updated = original with { Status = PostnomicPostStatus.Published, Title = "Updated" };

        // Assert
        Assert.Equal(PostnomicPostStatus.Published, updated.Status);
        Assert.Equal("Updated", updated.Title);
        Assert.Equal(PostnomicPostStatus.Draft, original.Status);
        Assert.Equal("Original", original.Title);
    }

    // ── PostnomicCreatePostRequest ────────────────────────────────────────────

    [Fact]
    public void PostnomicCreatePostRequest_Instantiation_SetsRequiredProperties()
    {
        // Arrange & Act
        var request = new PostnomicCreatePostRequest { Title = "Hello", Slug = "hello" };

        // Assert
        Assert.Equal("Hello", request.Title);
        Assert.Equal("hello", request.Slug);
    }

    [Fact]
    public void PostnomicCreatePostRequest_PublishImmediately_DefaultsToFalse()
    {
        // Arrange & Act
        var request = new PostnomicCreatePostRequest { Title = "Hello", Slug = "hello" };

        // Assert
        Assert.False(request.PublishImmediately);
    }

    [Fact]
    public void PostnomicCreatePostRequest_TagAndCategorySlugs_DefaultToEmptyCollections()
    {
        // Arrange & Act
        var request = new PostnomicCreatePostRequest { Title = "Hello", Slug = "hello" };

        // Assert
        Assert.NotNull(request.TagSlugs);
        Assert.Empty(request.TagSlugs);
        Assert.NotNull(request.CategorySlugs);
        Assert.Empty(request.CategorySlugs);
    }

    [Fact]
    public void PostnomicCreatePostRequest_AllProperties_CanBeSet()
    {
        // Arrange & Act
        var request = new PostnomicCreatePostRequest
        {
            Title = "Hello",
            Slug = "hello",
            Content = "<p>Hi</p>",
            Excerpt = "An excerpt",
            CoverImageUrl = "/media/blob/cover.jpg",
            ThumbnailImageUrl = "/media/blob/thumb.jpg",
            ShareImageUrl = "/media/blob/share.jpg",
            AuthorIdentityUserId = "auth0|abc",
            ReviewRequired = true,
            CommentsEnabled = true,
            TagSlugs = ["csharp"],
            CategorySlugs = ["tutorials"],
            PublishImmediately = true
        };

        // Assert
        Assert.Equal("<p>Hi</p>", request.Content);
        Assert.True(request.ReviewRequired);
        Assert.True(request.CommentsEnabled);
        Assert.Contains("csharp", request.TagSlugs);
        Assert.Contains("tutorials", request.CategorySlugs);
        Assert.True(request.PublishImmediately);
    }

    // ── PostnomicUpdatePostRequest ────────────────────────────────────────────

    [Fact]
    public void PostnomicUpdatePostRequest_Instantiation_SetsRequiredProperties()
    {
        // Arrange & Act
        var request = new PostnomicUpdatePostRequest { Title = "Updated", Slug = "updated" };

        // Assert
        Assert.Equal("Updated", request.Title);
        Assert.Equal("updated", request.Slug);
    }

    [Fact]
    public void PostnomicUpdatePostRequest_TagAndCategorySlugs_DefaultToEmptyCollections()
    {
        // Arrange & Act
        var request = new PostnomicUpdatePostRequest { Title = "T", Slug = "s" };

        // Assert
        Assert.NotNull(request.TagSlugs);
        Assert.Empty(request.TagSlugs);
        Assert.NotNull(request.CategorySlugs);
        Assert.Empty(request.CategorySlugs);
    }

    [Fact]
    public void PostnomicUpdatePostRequest_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicUpdatePostRequest { Title = "Original", Slug = "s" };

        // Act
        var updated = original with { Title = "Changed" };

        // Assert
        Assert.Equal("Changed", updated.Title);
        Assert.Equal("Original", original.Title);
    }

    // ── PostnomicMediaItem ────────────────────────────────────────────────────

    [Fact]
    public void PostnomicMediaItem_Instantiation_SetsRequiredProperties()
    {
        // Arrange & Act
        var item = new PostnomicMediaItem { Name = "cover.jpg", Path = "cover.jpg" };

        // Assert
        Assert.Equal("cover.jpg", item.Name);
        Assert.Equal("cover.jpg", item.Path);
        Assert.False(item.IsFolder);
    }

    [Fact]
    public void PostnomicMediaItem_OptionalProperties_DefaultToNull()
    {
        // Arrange & Act
        var item = new PostnomicMediaItem { Name = "n", Path = "p" };

        // Assert
        Assert.Null(item.Size);
        Assert.Null(item.ContentType);
        Assert.Null(item.LastModified);
        Assert.Null(item.Url);
    }

    [Fact]
    public void PostnomicMediaItem_WithExpression_ProducesCorrectCopy()
    {
        // Arrange
        var original = new PostnomicMediaItem { Name = "a.jpg", Path = "a.jpg", Url = "/media/blob/a.jpg" };

        // Act
        var updated = original with { Url = "/media/blob/renamed.jpg" };

        // Assert
        Assert.Equal("/media/blob/renamed.jpg", updated.Url);
        Assert.Equal("/media/blob/a.jpg", original.Url);
    }

    // ── PostnomicPostStatus ───────────────────────────────────────────────────

    [Fact]
    public void PostnomicPostStatus_HasExpectedMembers()
    {
        // Assert — mirrors the API's PostStatus enum member-for-member
        Assert.Equal(6, Enum.GetValues<PostnomicPostStatus>().Length);
        Assert.True(Enum.IsDefined(PostnomicPostStatus.Draft));
        Assert.True(Enum.IsDefined(PostnomicPostStatus.InReview));
        Assert.True(Enum.IsDefined(PostnomicPostStatus.Scheduled));
        Assert.True(Enum.IsDefined(PostnomicPostStatus.Published));
        Assert.True(Enum.IsDefined(PostnomicPostStatus.Unpublished));
        Assert.True(Enum.IsDefined(PostnomicPostStatus.Archived));
    }
}

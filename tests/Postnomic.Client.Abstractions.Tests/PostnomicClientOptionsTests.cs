
namespace Postnomic.Client.Abstractions.Tests;

/// <summary>
/// Tests for <see cref="PostnomicClientOptions"/>.
/// Verifies that default property values are empty strings and that all properties
/// can be freely set.
/// </summary>
public class PostnomicClientOptionsTests
{
    [Fact]
    public void PostnomicClientOptions_BaseUrl_DefaultsToEmptyString()
    {
        // Arrange & Act
        var options = new PostnomicClientOptions();

        // Assert
        Assert.Equal(string.Empty, options.BaseUrl);
    }

    [Fact]
    public void PostnomicClientOptions_ApiKey_DefaultsToEmptyString()
    {
        // Arrange & Act
        var options = new PostnomicClientOptions();

        // Assert
        Assert.Equal(string.Empty, options.ApiKey);
    }

    [Fact]
    public void PostnomicClientOptions_BlogSlug_DefaultsToEmptyString()
    {
        // Arrange & Act
        var options = new PostnomicClientOptions();

        // Assert
        Assert.Equal(string.Empty, options.BlogSlug);
    }

    [Fact]
    public void PostnomicClientOptions_AllDefaults_AreNonNull()
    {
        // Arrange & Act
        var options = new PostnomicClientOptions();

        // Assert — empty string, not null
        Assert.NotNull(options.BaseUrl);
        Assert.NotNull(options.ApiKey);
        Assert.NotNull(options.BlogSlug);
    }

    [Fact]
    public void PostnomicClientOptions_PersonalAccessToken_DefaultsToNull()
    {
        // Arrange & Act
        var options = new PostnomicClientOptions();

        // Assert
        Assert.Null(options.PersonalAccessToken);
    }

    [Fact]
    public void PostnomicClientOptions_BlogId_DefaultsToNull()
    {
        // Arrange & Act
        var options = new PostnomicClientOptions();

        // Assert
        Assert.Null(options.BlogId);
    }

    [Fact]
    public void PostnomicClientOptions_PersonalAccessTokenAndBlogId_CanBeSet()
    {
        // Arrange
        var options = new PostnomicClientOptions();

        // Act
        options.PersonalAccessToken = "pnp_abc123";
        options.BlogId = "3f2a1c9e-guid";

        // Assert
        Assert.Equal("pnp_abc123", options.PersonalAccessToken);
        Assert.Equal("3f2a1c9e-guid", options.BlogId);
    }

    [Fact]
    public void PostnomicClientOptions_BaseUrl_CanBeSet()
    {
        // Arrange
        var options = new PostnomicClientOptions();

        // Act
        options.BaseUrl = "https://api.postnomic.com";

        // Assert
        Assert.Equal("https://api.postnomic.com", options.BaseUrl);
    }

    [Fact]
    public void PostnomicClientOptions_ApiKey_CanBeSet()
    {
        // Arrange
        var options = new PostnomicClientOptions();

        // Act
        options.ApiKey = "my-secret-key";

        // Assert
        Assert.Equal("my-secret-key", options.ApiKey);
    }

    [Fact]
    public void PostnomicClientOptions_BlogSlug_CanBeSet()
    {
        // Arrange
        var options = new PostnomicClientOptions();

        // Act
        options.BlogSlug = "my-blog";

        // Assert
        Assert.Equal("my-blog", options.BlogSlug);
    }

    [Fact]
    public void PostnomicClientOptions_AllProperties_CanBeSetTogether()
    {
        // Arrange & Act
        var options = new PostnomicClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = "key-abc",
            BlogSlug = "tech-blog"
        };

        // Assert
        Assert.Equal("https://api.example.com", options.BaseUrl);
        Assert.Equal("key-abc", options.ApiKey);
        Assert.Equal("tech-blog", options.BlogSlug);
    }

    [Fact]
    public void PostnomicClientOptions_Properties_CanBeReassigned()
    {
        // Arrange
        var options = new PostnomicClientOptions
        {
            BaseUrl = "https://old-url.com",
            ApiKey = "old-key",
            BlogSlug = "old-slug"
        };

        // Act
        options.BaseUrl = "https://new-url.com";
        options.ApiKey = "new-key";
        options.BlogSlug = "new-slug";

        // Assert
        Assert.Equal("https://new-url.com", options.BaseUrl);
        Assert.Equal("new-key", options.ApiKey);
        Assert.Equal("new-slug", options.BlogSlug);
    }
}

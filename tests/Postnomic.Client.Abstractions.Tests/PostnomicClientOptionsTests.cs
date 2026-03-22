using FluentAssertions;

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
        options.BaseUrl.Should().Be(string.Empty);
    }

    [Fact]
    public void PostnomicClientOptions_ApiKey_DefaultsToEmptyString()
    {
        // Arrange & Act
        var options = new PostnomicClientOptions();

        // Assert
        options.ApiKey.Should().Be(string.Empty);
    }

    [Fact]
    public void PostnomicClientOptions_BlogSlug_DefaultsToEmptyString()
    {
        // Arrange & Act
        var options = new PostnomicClientOptions();

        // Assert
        options.BlogSlug.Should().Be(string.Empty);
    }

    [Fact]
    public void PostnomicClientOptions_AllDefaults_AreNonNull()
    {
        // Arrange & Act
        var options = new PostnomicClientOptions();

        // Assert — empty string, not null
        options.BaseUrl.Should().NotBeNull();
        options.ApiKey.Should().NotBeNull();
        options.BlogSlug.Should().NotBeNull();
    }

    [Fact]
    public void PostnomicClientOptions_BaseUrl_CanBeSet()
    {
        // Arrange
        var options = new PostnomicClientOptions();

        // Act
        options.BaseUrl = "https://api.postnomic.com";

        // Assert
        options.BaseUrl.Should().Be("https://api.postnomic.com");
    }

    [Fact]
    public void PostnomicClientOptions_ApiKey_CanBeSet()
    {
        // Arrange
        var options = new PostnomicClientOptions();

        // Act
        options.ApiKey = "my-secret-key";

        // Assert
        options.ApiKey.Should().Be("my-secret-key");
    }

    [Fact]
    public void PostnomicClientOptions_BlogSlug_CanBeSet()
    {
        // Arrange
        var options = new PostnomicClientOptions();

        // Act
        options.BlogSlug = "my-blog";

        // Assert
        options.BlogSlug.Should().Be("my-blog");
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
        options.BaseUrl.Should().Be("https://api.example.com");
        options.ApiKey.Should().Be("key-abc");
        options.BlogSlug.Should().Be("tech-blog");
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
        options.BaseUrl.Should().Be("https://new-url.com");
        options.ApiKey.Should().Be("new-key");
        options.BlogSlug.Should().Be("new-slug");
    }
}

using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.Abstractions.Tests;

public class PostnomicPostLanguageFieldsTests
{
    [Fact]
    public void Summary_HasLanguageFields_WithDefaults()
    {
        var dto = new PostnomicPostSummary { Slug = "s", Title = "t", AuthorName = "a" };
        Assert.Equal("en", dto.Language);
        Assert.Empty(dto.AvailableLanguages);
    }

    [Fact]
    public void Summary_LanguageFields_CanBeSet()
    {
        var dto = new PostnomicPostSummary { Slug = "s", Title = "t", AuthorName = "a", Language = "de", AvailableLanguages = new[] { "en", "de" } };
        Assert.Equal("de", dto.Language);
        Assert.Equal(new[] { "en", "de" }, dto.AvailableLanguages);
    }

    [Fact]
    public void Detail_HasLanguageFields_WithDefaults()
    {
        var dto = new PostnomicPostDetail { Slug = "s", Title = "t", AuthorName = "a" };
        Assert.Equal("en", dto.Language);
        Assert.Empty(dto.AvailableLanguages);
    }

    [Fact]
    public void Detail_LanguageFields_CanBeSet()
    {
        var dto = new PostnomicPostDetail { Slug = "s", Title = "t", AuthorName = "a", Language = "de", AvailableLanguages = new[] { "en", "de" } };
        Assert.Equal("de", dto.Language);
        Assert.Equal(new[] { "en", "de" }, dto.AvailableLanguages);
    }
}

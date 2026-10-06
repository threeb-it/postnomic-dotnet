namespace Postnomic.Client.Abstractions.Tests;

/// <summary>
/// Tests for <see cref="PostnomicLinkRel.ForFilterLink"/> and the
/// <see cref="PostnomicClientOptions.FilterLinkRel"/> default.
/// </summary>
public class PostnomicLinkRelTests
{
    [Fact]
    public void FilterLinkRel_DefaultsToNull()
    {
        Assert.Null(new PostnomicClientOptions().FilterLinkRel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForFilterLink_WithoutOptionOrExistingRel_ReturnsNull(string? filterLinkRel)
    {
        // Null makes Razor and Blazor omit the attribute entirely.
        Assert.Null(PostnomicLinkRel.ForFilterLink(filterLinkRel));
        Assert.Null(PostnomicLinkRel.ForFilterLink(filterLinkRel, "  "));
    }

    [Fact]
    public void ForFilterLink_WithoutOption_LeavesAnExistingRelUntouched()
    {
        Assert.Equal("noopener  noreferrer", PostnomicLinkRel.ForFilterLink(null, "noopener  noreferrer"));
    }

    [Theory]
    [InlineData("nofollow", null, "nofollow")]
    [InlineData("  nofollow  ", null, "nofollow")]
    [InlineData("nofollow ugc", null, "nofollow ugc")]
    [InlineData("nofollow", "noopener noreferrer", "noopener noreferrer nofollow")]
    [InlineData("nofollow", "nofollow", "nofollow")]
    [InlineData("NOFOLLOW noopener", "noopener nofollow", "noopener nofollow")]
    [InlineData("nofollow\tnofollow", "", "nofollow")]
    public void ForFilterLink_MergesTokensWithoutDuplicates(string filterLinkRel, string? existing, string expected)
    {
        Assert.Equal(expected, PostnomicLinkRel.ForFilterLink(filterLinkRel, existing));
    }
}
